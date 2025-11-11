namespace StringWeaver;

/// <summary>
/// [Experimental] Sibling implementation of <see cref="StringWeaver"/> that sources all backing storage from unmanaged memory to avoid GC _pressure for very large buffers.
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
    /// <param name="capacity">The initial capacity of the buffer's backing memory. Must not be less than the Length of <paramref name="initialContent"/>.</param>
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
    /// <param name="capacity">The initial capacity of the buffer's backing memory. Must not be less than the Length of <paramref name="initialContent"/>.</param>
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

        // More efficient than non-generic Array.Copy plus constrained to the occupied Length
        other.Span.CopyTo(Span);
    }
    #endregion

    /// <summary>
    /// Delegates this call to <see cref="_buffer"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override void Grow(int requiredCapacity) => _buffer.Grow(requiredCapacity);

    #region Cleanup
    ~UnsafeStringWeaver() => Dispose();
    public void Dispose()
    {
        ((IDisposable)_buffer)?.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion
}
