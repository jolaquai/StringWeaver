namespace StringWeaver.Helpers;

// This entire thing is unsafe because netstandard2.0 doesn't support ref fields, meaning I can't store a reference to the first T as a managed pointer
internal sealed unsafe class NativeBuffer<T> : MemoryManager<T> where T : unmanaged
{
    #region const
    private const int AutoPressureSizeBytes = 10240;
    private static readonly unsafe int _sizeOfT = sizeof(T);
    private static readonly T* _nullPtr = (T*)0;
    #endregion

    #region Instance fields
    private readonly bool _wipeOnDispose;
    private readonly bool? _pressure;
    private bool lastGrowReportedPressure;
    internal T* pointer = _nullPtr;

    /// <summary>
    /// Gets an internal version key that can be used to detect changes to the underlying memory block.
    /// This is essentially an implementation detail. Consumers must never rely on this value being stable of introduce behavioral dependencies based on this value.
    /// </summary>
    public uint Version { get; private set; }
    #endregion

    #region Properties/Indexers
    /// <summary>
    /// Gets the numeric <see cref="nint"/> value of <see cref="Pointer"/>;
    /// </summary>
    public nint PointerValue
    {
        get
        {
            EnsureUsable();
            return (nint)Pointer;
        }
    }

    /// <summary>
    /// Gets a pointer to the first element of the memory block.
    /// </summary>
    public T* Pointer
    {
        get
        {
            EnsureUsable();
            return pointer;
        }
        private set
        {
            EnsureUsable();
            pointer = value;
        }
    }
    /// <summary>
    /// Gets the number of elements that fit in the memory block.
    /// </summary>
    public int Capacity
    {
        get
        {
            EnsureUsable();
            return field;
        }
        private set
        {
            EnsureUsable();
            field = value;
        }
    }
    /// <summary>
    /// Gets the raw size of the memory block in <see langword="byte"/>s.
    /// </summary>
    public long CapacityBytes
    {
        get
        {
            EnsureUsable();
            return (long)Capacity * _sizeOfT;
        }
    }
    #endregion

    /// <summary>
    /// Initializes a new <see cref="NativeBuffer{T}"/> with the specified initial capacity and a value that indicates whether the memory should be wiped on disposal of this wrapper.
    /// </summary>
    /// <param name="count">The initial capacity in number of <typeparamref name="T"/> elements.</param>
    /// <param name="wipeOnDispose">Whether to wipe the memory block on disposal of this wrapper.</param>
    /// <param name="pressure">Whether to inform the <see cref="GC"/> about unmanaged memory pressure incurred by allocations made by this instance. If <see langword="null"/>, this is decided dynamically based on the size of allocations.</param>
    public NativeBuffer(int count, bool wipeOnDispose = false, bool? pressure = null)
    {
        // This is safe to do since ReAllocHGlobal just delegates to AllocHGlobal when passed a nullptr for the "previous" pointer
        Grow(count);
        _wipeOnDispose = wipeOnDispose;
        _pressure = pressure;
    }

    #region Grow
    /// <summary>
    /// Grows the underlying memory block if <paramref name="requiredCapacity"/> exceeds the current capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GrowIfNeeded(int requiredCapacity)
    {
        if (requiredCapacity > Capacity)
        {
            Grow(requiredCapacity);
        }
    }
    /// <summary>
    /// Reallocates the underlying memory block unconditionally, ensuring at least twice the previous capacity (if possible).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Grow() => Grow(Capacity + 1);
    /// <summary>
    /// Reallocates the underlying memory block unconditionally, ensuring it can accommodate at least <paramref name="requiredCapacity"/> characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Grow(int requiredCapacity)
    {
        Version++;

        var previousSize = CapacityBytes;

        var newSize = Pow2.NextPowerOf2(requiredCapacity * (long)_sizeOfT);
        Pointer = (T*)Marshal.ReAllocHGlobal(PointerValue, (nint)newSize);

        Capacity = (int)(newSize / _sizeOfT);

        var allocDiff = newSize - previousSize;
        switch (_pressure)
        {
            case true:
            {
                GC.AddMemoryPressure(allocDiff);
                break;
            }
            case null:
            {
                if (lastGrowReportedPressure)
                {
                    if (newSize >= AutoPressureSizeBytes)
                    {
                        GC.AddMemoryPressure(allocDiff);
                    }
                }
                else
                {
                    if (newSize >= AutoPressureSizeBytes)
                    {
                        GC.AddMemoryPressure(newSize);
                        lastGrowReportedPressure = true;
                    }
                }

                break;
            }
        }
    }
    #endregion

    #region Clear
    public void Wipe() => Unsafe.InitBlockUnaligned(Pointer, 0, (uint)CapacityBytes);
    #endregion

    #region MemoryManager<T>
    internal bool disposed;
    internal volatile int pinCount;
    internal bool freePending;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureUsable()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(NativeBuffer<>));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Span<T> GetSpan()
    {
        EnsureUsable();
        return Capacity > 0 ? new Span<T>(Pointer, Capacity) : [];
    }
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        EnsureUsable();
        ValidateIndexTwise(elementIndex);

        Interlocked.Increment(ref pinCount);
        // Native memory blocks don't need to be pinned, BUT we have to do some ref counting to prevent this wrapper from being disposed while "pins" are outstanding
        return new MemoryHandle(Pointer + elementIndex, default, this);
    }
    public override void Unpin()
    {
        if (Interlocked.Decrement(ref pinCount) < 0)
        {
            Debug.Fail("Unbalanced Unpin() call detected.");
            Interlocked.Exchange(ref pinCount, 0);
        }

        if (freePending && pinCount == 0)
        {
            Marshal.FreeHGlobal(PointerValue);
            freePending = false;
            disposed = true;
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }
        freePending = true;
        disposed = true;

        if (_wipeOnDispose)
        {
            Wipe();
        }

        var ptr = PointerValue;
        Pointer = _nullPtr;

        if (pinCount == 0)
        {
            Marshal.FreeHGlobal(ptr);
        }
        // If we didn't dispose, the last Unpin call is now responsible for that
    }
    protected override bool TryGetArray(out ArraySegment<T> segment)
    {
        // We have no array to expose
        segment = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateIndexTwise(int index)
    {
        if (index < 0 || index >= Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Element index must be within the bounds of the memory block.");
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateIndexBytewise(int index)
    {
        if (index < 0 || index >= CapacityBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Byte index must be within the bounds of the memory block.");
        }
    }
    #endregion
}