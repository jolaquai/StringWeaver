using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace StringWeaver.Specialized;

/// <summary>
/// Implements a <see cref="StringWeaver"/> variant that wraps an existing consumer-provided buffer (which may be unmanaged).
/// The consumer is responsible for providing a buffer that is large enough to hold the expected content AND that the memory backing that buffer remains valid and accessible for the duration the <see cref="WrappingStringWeaver"/> is in use.
/// Resizing is not supported and will invariably throw <see cref="NotSupportedException"/>.
/// </summary>
public unsafe class WrappingStringWeaver : StringWeaver, IDisposable
{
    private enum MemorySource
    {
        /// <summary>
        /// A <see cref="Memory{T}"/> passed by the consumer is forwarded directly.
        /// </summary>
        MemoryForward,
        /// <summary>
        /// Some mechanism used to pin the memory and obtain a raw pointer to it allows using <see cref="MemoryViewProvider{T}"/> to be used, which then hands out <see cref="Memory{T}"/>s.
        /// </summary>
        Raw,
    }

#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    private static void ThrowResizeNotSupported() => throw new NotSupportedException($"Resizing is not supported by {nameof(WrappingStringWeaver)}.");
    private static void ValidateRangeForZeroBasedLength(int index, int length, int totalLength, int usedLength)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index ({index}) must be within the bounds of the provided memory.");
        }
        if (length <= 0)
        {
            // length == 0 is disallowed because it would give the caller a completely useless instance that just holds onto a pinned array for no reason
            throw new ArgumentOutOfRangeException(nameof(length), $"Length ({length}) must be positive.");
        }

        // Check totalLength AFTER length, otherwise the delegating ctor chains could throw misleading exceptions
        if (totalLength <= 0)
        {
            throw new ArgumentException("The provided memory must have a length greater than zero.", nameof(totalLength));
        }

        if (usedLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usedLength), $"Used Length ({usedLength}) must be non-negative.");
        }

        if (index + length > totalLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"Index + Length ({index} + {length} = {index + length}) must be within the bounds of the provided memory (Length: {totalLength}).");
        }

        if (usedLength > totalLength)
        {
            throw new ArgumentOutOfRangeException(nameof(usedLength), $"Used Length ({usedLength}) cannot be greater than the total length of the provided memory ({totalLength}).");
        }
    }

    #region Instance fields
    private readonly MemorySource _memorySource;

    private readonly MemoryViewProvider<char> _pinnedMemoryViewProvider;
    private readonly char* _pinnedPointer;
    private readonly Memory<char> _memory;
    private MemoryHandle _memoryHandle;
    #endregion

    /// <inheritdoc/>
    protected internal override Memory<char> FullMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _memorySource switch
        {
            MemorySource.MemoryForward => _memory,
            MemorySource.Raw => _pinnedMemoryViewProvider.Memory,
            _ => throw new InvalidOperationException("Unrecognized memory source."),
        };
    }

    #region .ctors
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using the entirety of the provided <see cref="Memory{T}"/> of <see langword="char"/> as the backing buffer, optionally pinning it in memory.
    /// </summary>
    /// <param name="memory">The <see cref="Memory{T}"/> of <see langword="char"/> to use as backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    /// <param name="pin"><see langword="true"/> to pin the <see cref="Memory{T}"/> of <see langword="char"/> in memory; otherwise, <see langword="false"/>.</param>
    public WrappingStringWeaver(Memory<char> memory, int usedLength, bool pin = false) : this(memory, 0, memory.Length, usedLength, pin) { }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using a section of the provided <see cref="Memory{T}"/> of <see langword="char"/> starting at <paramref name="index"/> as the backing buffer, optionally pinning it in memory.
    /// </summary>
    /// <param name="memory">The <see cref="Memory{T}"/> of <see langword="char"/> to use as backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    /// <param name="index">The starting index within <paramref name="memory"/> to use as the backing buffer.</param>
    /// <param name="pin"><see langword="true"/> to pin the <see cref="Memory{T}"/> of <see langword="char"/> in memory; otherwise, <see langword="false"/>.</param>
    public WrappingStringWeaver(Memory<char> memory, int index, int usedLength, bool pin = false) : this(memory, index, memory.Length - index, usedLength, pin) { }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using a section of the provided <see cref="Memory{T}"/> of <see langword="char"/> delimited by <paramref name="index"/> and <paramref name="length"/> as the backing buffer, optionally pinning it in memory.
    /// </summary>
    /// <param name="memory">The <see cref="Memory{T}"/> of <see langword="char"/> to use as backing buffer.</param>
    /// <param name="index">The starting index within <paramref name="memory"/> to use as the backing buffer.</param>
    /// <param name="length">The length of the section within <paramref name="memory"/> to use as the backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    /// <param name="pin"><see langword="true"/> to pin the <see cref="Memory{T}"/> of <see langword="char"/> in memory; otherwise, <see langword="false"/>.</param>
    public WrappingStringWeaver(Memory<char> memory, int index, int length, int usedLength, bool pin = false)
    {
        var memLength = memory.Length;
        ValidateRangeForZeroBasedLength(index, length, memLength, usedLength);

        End = usedLength;

        if (pin)
        {
            _memorySource = MemorySource.Raw;
            _memoryHandle = memory.Slice(index, length).Pin();
            _pinnedPointer = (char*)_memoryHandle.Pointer;
            _pinnedMemoryViewProvider = new MemoryViewProvider<char>(_pinnedPointer, length);
        }
        else
        {
            _memorySource = MemorySource.MemoryForward;
            _memory = memory.Slice(index, length);
        }
    }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using the entirety of the provided <see cref="Span{T}"/> of <see langword="char"/> as the backing buffer.
    /// The memory backing it is assumed to be pinned (for example, <see langword="stackalloc"/>s are safe to use here).
    /// </summary>
    /// <param name="span">The <see cref="Span{T}"/> of <see langword="char"/> to use as backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    public WrappingStringWeaver(Span<char> span, int usedLength) : this(span, 0, span.Length, usedLength) { }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using a section of the provided <see cref="Span{T}"/> of <see langword="char"/> starting at <paramref name="index"/> as the backing buffer.
    /// The memory backing it is assumed to be pinned.
    /// </summary>
    /// <param name="span">The <see cref="Span{T}"/> of <see langword="char"/> to use as backing buffer.</param>
    /// <param name="index">The starting index within <paramref name="span"/> to use as the backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    public WrappingStringWeaver(Span<char> span, int index, int usedLength) : this(span, index, span.Length - index, usedLength) { }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using a section of the provided <see cref="Span{T}"/> of <see langword="char"/> delimited by <paramref name="index"/> and <paramref name="length"/> as the backing buffer.
    /// The memory backing it is assumed to be pinned.
    /// </summary>
    /// <param name="span">The <see cref="Span{T}"/> of <see langword="char"/> to use as backing buffer.</param>
    /// <param name="index">The starting index within <paramref name="span"/> to use as the backing buffer.</param>
    /// <param name="length">The length of the section within <paramref name="span"/> to use as the backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    public WrappingStringWeaver(Span<char> span, int index, int length, int usedLength)
    {
        var spanLength = span.Length;
        ValidateRangeForZeroBasedLength(index, length, spanLength, usedLength);

        End = usedLength;

        span = span.Slice(index, length);
        ref var charRef = ref MemoryMarshal.GetReference(span);
        var charPtr = (char*)Unsafe.AsPointer(ref charRef);

        _memorySource = MemorySource.Raw;

        _pinnedPointer = charPtr;
        _pinnedMemoryViewProvider = new MemoryViewProvider<char>(charPtr, length);
    }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using the entirety of the provided <see langword="char"/> array as the backing buffer, optionally pinning it in memory.
    /// </summary>
    /// <param name="array">The array to use as backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    /// <param name="pin"><see langword="true"/> to pin the array in memory; otherwise, <see langword="false"/>.</param>
    public WrappingStringWeaver(char[] array, int usedLength, bool pin = false) : this(array, 0, array.Length, usedLength, pin) { }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using a section of the provided <see langword="char"/> array starting at <paramref name="index"/> as the backing buffer, optionally pinning it in memory.
    /// </summary>
    /// <param name="array">The array to use as backing buffer.</param>
    /// <param name="index">The starting index within <paramref name="array"/> to use as the backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    /// <param name="pin"><see langword="true"/> to pin the array in memory; otherwise, <see langword="false"/>.</param>
    public WrappingStringWeaver(char[] array, int index, int usedLength, bool pin = false) : this(array, index, array.Length - index, usedLength, pin) { }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using a section of the provided <see langword="char"/> array delimited by <paramref name="index"/> and <paramref name="length"/> as the backing buffer, optionally pinning it in memory.
    /// </summary>
    /// <param name="array">The array to use as backing buffer.</param>
    /// <param name="index">The starting index within <paramref name="array"/> to use as the backing buffer.</param>
    /// <param name="length">The length of the section within <paramref name="array"/> to use as the backing buffer.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    /// <param name="pin"><see langword="true"/> to pin the array in memory; otherwise, <see langword="false"/>.</param>
    public WrappingStringWeaver(char[] array, int index, int length, int usedLength, bool pin = false) : this(new Memory<char>(array), index, length, usedLength, pin) { }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using the provided unmanaged memory buffer as the backing buffer.
    /// It is assumed the location pointed to or into by <paramref name="pointer"/> is pinned.
    /// </summary>
    /// <param name="pointer">An unmanaged pointer to the start of the memory block to use as backing buffer.</param>
    /// <param name="length">The length of the memory block to use as backing buffer in <see langword="char"/> elements.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    public WrappingStringWeaver(scoped ref char pointer, int length, int usedLength) : this((char*)Unsafe.AsPointer(ref pointer), length, usedLength) { }
    /// <summary>
    /// Initializes a new <see cref="WrappingStringWeaver"/> using the provided unmanaged memory buffer as the backing buffer.
    /// It is assumed the location pointed to or into by <paramref name="pointer"/> is pinned (for example, using a <see cref="GCHandle"/> or through a <see langword="fixed"/> statement, or by nature if the target memory is unmanaged).
    /// </summary>
    /// <param name="pointer">A managed pointer to the start of the memory block to use as backing buffer.</param>
    /// <param name="length">The length of the memory block to use as backing buffer in <see langword="char"/> elements.</param>
    /// <param name="usedLength">The length of the used portion within the backing buffer.</param>
    public WrappingStringWeaver(char* pointer, int length, int usedLength) : this(new Span<char>(pointer, length), 0, length, usedLength) { }
    #endregion

    /// <summary>
    /// Invariably throws <see cref="NotSupportedException"/>; resizing is not supported for <see cref="WrappingStringWeaver"/>.
    /// </summary>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    protected override void GrowCore(int requiredCapacity) => ThrowResizeNotSupported();

    #region Cleanup
    /// <inheritdoc/>
    ~WrappingStringWeaver() => Dispose();
    /// <inheritdoc/>
    public void Dispose()
    {
        switch (_memorySource)
        {
            case MemorySource.Raw:
                ((IDisposable)_pinnedMemoryViewProvider).Dispose();
                _memoryHandle.Dispose();
                break;
        }

        GC.SuppressFinalize(this);
    }
    #endregion
}
