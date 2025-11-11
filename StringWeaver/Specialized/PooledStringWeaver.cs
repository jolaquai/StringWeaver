namespace StringWeaver.Specialized;

/// <summary>
/// Sibling implementation of <see cref="StringWeaver"/> that sources all backing storage from an <see cref="ArrayPool{T}"/> of <see langword="char"/>.
/// </summary>
public sealed class PooledStringWeaver : StringWeaver, IDisposable
{
    private readonly ArrayPool<char> _pool;
    private char[] buffer;

    #region .ctors
    /// <summary>
    /// Initializes a new <see cref="PooledStringWeaver"/>.
    /// </summary>
    /// <param name="capacity">The initial capacity of the <see cref="PooledStringWeaver"/>.</param>
    /// <param name="charArrayPool">The <see cref="ArrayPool{T}"/> of <see langword="char"/> to use. If <see langword="null"/>, the shared pool will be used.</param>
    public PooledStringWeaver(int capacity, ArrayPool<char> charArrayPool)
    {
        _pool = charArrayPool;
        Grow(capacity);
    }
    #endregion

    /// <inheritdoc/>
    protected internal override Memory<char> FullMemory => buffer.AsMemory();
    /// <inheritdoc/>
    protected override void Grow(int requiredCapacity)
    {
        Version++;

        requiredCapacity = Pow2.NextPowerOf2(requiredCapacity);
        if (requiredCapacity == 0)
        {
            // We're at the limit, can't grow any more
            throw new InvalidOperationException("Maximum capacity reached, cannot grow further.");
        }

        var newBuffer = _pool.Rent(requiredCapacity);

        var oldBuffer = buffer;
        if (oldBuffer is not null)
        {
            oldBuffer.AsSpan(0, Length).CopyTo(newBuffer);
            _pool.Return(oldBuffer);
        }

        buffer = newBuffer;
    }

    #region Cleanup
    private volatile int disposed;
    /// <inheritdoc/>
    ~PooledStringWeaver() => Dispose(false);
    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }
        if (disposing)
        {
            _pool.Return(buffer);
        }
    }
    #endregion
}
