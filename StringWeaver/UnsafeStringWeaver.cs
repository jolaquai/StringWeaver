#pragma warning disable CA1510 // Prevents further fragmentation of code paths between TargetFrameworks for no reason

using System.Buffers;
using System.Runtime.CompilerServices;

namespace StringWeaver;

/// <summary>
/// [Experimental] Sibling implementation of <see cref="StringWeaver"/> that sources all backing storage from unmanaged memory to avoid GC pressure for very large buffers.
/// </summary>
internal sealed class UnsafeStringWeaver : StringWeaver, IDisposable
{
    #region const
    private const int DefaultCapacity = 1024;
    #endregion

    #region Instance fields
    private readonly NativeBuffer<char> _buffer;
    #endregion

    #region Properties/Indexers
    /// <summary>
    /// Gets the numeric <see cref="nint"/> value of <see cref="Pointer"/>;
    /// </summary>
    public nint PointerValue => _buffer.PointerValue;
    /// <summary>
    /// Gets a pointer to the first element of the memory block.
    /// </summary>
    public unsafe char* Pointer => _buffer.Pointer;

    /// <summary>
    /// Internal use only. Returns a mutable <see cref="Memory{T}"/> over the entire buffer (including unused space).
    /// <see cref="_buffer"/> must be definitely assigned.
    /// </summary>
    protected internal override Memory<char> FullMemory => _buffer.Memory;
    #endregion

    #region .ctors
    /// <summary>
    /// Initializes a new <see cref="UnsafeStringWeaver"/> with the default capacity of 256.
    /// </summary>
    public UnsafeStringWeaver() : this([], DefaultCapacity) { }
    /// <summary>
    /// Initializes a new <see cref="UnsafeStringWeaver"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the buffer's backing memory.</param>
    public UnsafeStringWeaver(int capacity) : this([], capacity) { }
    /// <summary>
    /// Initializes a new <see cref="UnsafeStringWeaver"/> with the specified initial content.
    /// </summary>
    /// <param name="initialContent">A <see langword="string"/> that will be copied into the buffer.</param>
    public UnsafeStringWeaver(string initialContent) : this(initialContent.AsSpan(), initialContent.Length) { }
    /// <summary>
    /// Initializes a new <see cref="UnsafeStringWeaver"/> with the specified initial content and capacity.
    /// </summary>
    /// <param name="initialContent">A <see langword="string"/> that will be copied into the buffer.</param>
    /// <param name="capacity">The initial capacity of the buffer's backing memory. Must not be less than the length of <paramref name="initialContent"/>.</param>
    public UnsafeStringWeaver(string initialContent, int capacity) : this(initialContent.AsSpan(), capacity) { }
    /// <summary>
    /// Initializes a new <see cref="UnsafeStringWeaver"/> with the specified initial content.
    /// </summary>
    /// <param name="initialContent">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> that will be copied into the buffer.</param>
    public UnsafeStringWeaver(ReadOnlySpan<char> initialContent) : this(initialContent, initialContent.Length) { }
    /// <summary>
    /// Initializes a new <see cref="UnsafeStringWeaver"/> with the specified initial content and capacity.
    /// </summary>
    /// <param name="initialContent">The initial content to copy into the buffer.</param>
    /// <param name="capacity">The initial capacity of the buffer's backing memory. Must not be less than the length of <paramref name="initialContent"/>.</param>
    public UnsafeStringWeaver(ReadOnlySpan<char> initialContent, int capacity)
    {
        if (capacity < initialContent.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must not be less than the length of the initial content.");
        }

        if (capacity <= DefaultCapacity)
        {
            capacity = initialContent.Length < DefaultCapacity ? DefaultCapacity : initialContent.Length;
        }

        _buffer = new NativeBuffer<char>(capacity);
        Length = initialContent.Length;

        if (initialContent.Length > 0)
        {
            initialContent.CopyTo(Span);
        }
    }
    /// <summary>
    /// Initializes a new <see cref="UnsafeStringWeaver"/> as an independent copy of another <see cref="UnsafeStringWeaver"/>.
    /// </summary>
    /// <param name="other">The <see cref="UnsafeStringWeaver"/> to copy from.</param>
    public UnsafeStringWeaver(UnsafeStringWeaver other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        var span = other.Span;

        _buffer = new NativeBuffer<char>(other.Capacity);
        Length = span.Length;

        // More efficient than non-generic Array.Copy plus constrained to the occupied length
        other.Span.CopyTo(Span);
    }
    #endregion

    /// <summary>
    /// Delegates this call to <see cref="_buffer"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override void Grow(int requiredCapacity) => _buffer.Grow(requiredCapacity);

    #region Cleanup
    ~UnsafeStringWeaver()
    {
        Dispose();
        Debug.Fail("UnsafeStringWeaver had to be finalized.");
    }
    public void Dispose()
    {
        ((IDisposable)_buffer).Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion
}

// This entire thing is unsafe because netstandard2.0 doesn't support ref fields, meaning I can't store a reference to the first T as a managed pointer
internal unsafe sealed class NativeBuffer<T> : MemoryManager<T> where T : unmanaged
{
    #region const
    private static readonly unsafe int _sizeOfT = sizeof(T);
    private static readonly T* _nullPtr = (T*)0;
    #endregion

    #region Instance fields
    private readonly bool _wipeOnDispose;
    public uint Version { get; private set; }
    #endregion

    #region Properties/Indexers
    /// <summary>
    /// Gets the numeric <see cref="nint"/> value of <see cref="Pointer"/>;
    /// </summary>
    public nint PointerValue => (nint)Pointer;
    /// <summary>
    /// Gets a pointer to the first element of the memory block.
    /// </summary>
    public T* Pointer { get; private set; } = _nullPtr;
    /// <summary>
    /// Gets the number of elements that fit in the memory block.
    /// </summary>
    public int Capacity { get; private set; }
    /// <summary>
    /// Gets the raw size of the memory block in <see langword="byte"/>s.
    /// </summary>
    public long CapacityBytes => (long)Capacity * _sizeOfT;
    #endregion

    /// <summary>
    /// Initializes a new <see cref="NativeBuffer{T}"/> with the specified initial capacity and a value that indicates whether the memory should be wiped on disposal of this wrapper.
    /// </summary>
    /// <param name="count">The initial capacity in number of <typeparamref name="T"/> elements.</param>
    /// <param name="wipeOnDispose">Whether to wipe the memory block on disposal of this wrapper.</param>
    public NativeBuffer(int count, bool wipeOnDispose = false)
    {
        // This is safe to do since ReAllocHGlobal just delegates to AllocHGlobal when passed a nullptr for the "previous" pointer
        Capacity = Grow(count);
        _wipeOnDispose = wipeOnDispose;
    }

    #region Grow
    /// <summary>
    /// Grows the underlying memory block if <paramref name="requiredCapacity"/> exceeds the current capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GrowIfNeeded(int requiredCapacity)
    {
        if (requiredCapacity > Capacity)
        {
            return Grow(requiredCapacity);
        }
        return Capacity;
    }
    /// <summary>
    /// Reallocates the underlying memory block unconditionally, ensuring at least twice the previous capacity (if possible).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Grow() => Grow(Capacity + 1);
    /// <summary>
    /// Reallocates the underlying memory block unconditionally, ensuring it can accommodate at least <paramref name="requiredCapacity"/> characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Grow(int requiredCapacity)
    {
        Version++;

        var newSize = Helpers.NextPowerOf2(requiredCapacity);
        Pointer = (T*)Marshal.ReAllocHGlobal(PointerValue, (nint)newSize);
        return newSize;
    }
    #endregion

    #region Clear
    public void Wipe() => Unsafe.InitBlockUnaligned(Pointer, 0, (uint)CapacityBytes);
    #endregion

    #region MemoryManager<T>
    public override Span<T> GetSpan() => new Span<T>(Pointer, Capacity);
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex >= CapacityBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex), "Element index must be within the bounds of the memory block.");
        }

        // Native memory blocks don't need to be pinned...
        return new MemoryHandle(Pointer + elementIndex);
    }
    public override void Unpin() { }
    protected override void Dispose(bool disposing)
    {
        if (_wipeOnDispose)
        {
            Wipe();
        }
        Marshal.FreeHGlobal(PointerValue);
    }
    #endregion
}