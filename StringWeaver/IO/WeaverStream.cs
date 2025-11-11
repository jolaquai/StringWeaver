namespace StringWeaver.IO;

internal sealed class WeaverStream(StringWeaver weaver, Encoding encoding) : Stream
{
    private readonly Decoder _decoder = encoding.GetDecoder();

    public override bool CanRead
    {
        get
        {
            EnsureUsable();
            return false;
        }
    }
    public override bool CanSeek
    {
        get
        {
            EnsureUsable();
            return false;
        }
    }
    public override bool CanWrite
    {
        get
        {
            EnsureUsable();
            return true;
        }
    }
    public override long Length
    {
        get
        {
            EnsureUsable();
            throw new NotSupportedException($"Cannot read from a {nameof(StringWeaver)} stream.");
        }
    }

    public override long Position
    {
        get
        {
            EnsureUsable();
            throw new NotSupportedException($"Cannot read from a {nameof(StringWeaver)} stream.");
        }
        set
        {
            EnsureUsable();
            throw new NotSupportedException($"Cannot seek a {nameof(StringWeaver)} stream.");
        }
    }

    public override void Flush() => EnsureUsable();
    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureUsable();
        throw new NotSupportedException($"Cannot read from a {nameof(StringWeaver)} stream.");
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureUsable();
        throw new NotSupportedException($"Cannot seek a {nameof(StringWeaver)} stream.");
    }
    public override void SetLength(long value)
    {
        EnsureUsable();
        throw new NotSupportedException($"Cannot set length of a {nameof(StringWeaver)} stream.");
    }
    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureUsable();
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)} and {nameof(count)} must specify a valid range in the buffer.");
        }

        var source = buffer.AsSpan(offset, count);
        Write(source);
    }
    public
#if !NETSTANDARD2_0
        override
#endif
        unsafe void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureUsable();
        if (buffer.Length == 0)
        {
            return;
        }

        var srcPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(buffer));

        var charCount = _decoder.GetCharCount(srcPtr, buffer.Length, false);
        var destination = weaver.GetWritableSpan(charCount);
        Debug.Assert(destination.Length != 0, "destination buffer returned must never be 0-length");
        var destPtr = (char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(destination));

        var writtenTotal = 0;
        while (writtenTotal < charCount)
        {
            // Decided to do this for the general path instead of #if'ing for Span<T> overloads
            var written = _decoder.GetChars(srcPtr + writtenTotal, buffer.Length - writtenTotal, destPtr + writtenTotal, charCount - writtenTotal, true);
            writtenTotal += written;
            weaver.Expand(written);
        }
    }

    private volatile int disposed;
    private void EnsureUsable()
    {
        if (disposed == 1)
        {
            throw new ObjectDisposedException(nameof(StringWeaver), "The Stream has been disposed.");
        }
    }
    protected override void Dispose(bool disposing)
    {
        // This is a nop, but we'll flag as being disposed for consistency
        _ = Interlocked.Exchange(ref disposed, 1);
        base.Dispose(true);
    }
#if !NETSTANDARD2_0
    public override ValueTask DisposeAsync()
    {
        _ = Interlocked.Exchange(ref disposed, 1);
        return base.DisposeAsync();
    }
#endif
}