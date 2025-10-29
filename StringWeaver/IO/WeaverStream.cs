using System.Runtime.CompilerServices;
using System.Text;

namespace StringWeaver.IO;

internal sealed class WeaverStream(StringWeaver weaver, Encoding encoding, Action onDispose = null) : Stream
{
    private StringWeaver _weaver = weaver;
    private readonly Decoder _decoder = encoding.GetDecoder();

    private void EnsureUsable()
    {
        if (_weaver is null)
        {
            throw new ObjectDisposedException(nameof(StringWeaver), "The Stream has been disposed.");
        }
    }

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

        var source = _weaver.FullSpan.Slice(offset, count);
        var charCount = _decoder.GetCharCount(buffer, offset, count);
        var destination = _weaver.GetWritableSpan(charCount);
        Debug.Assert(destination.Length != 0, "destination buffer returned must never be 0-length");
        unsafe
        {
            var srcPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(source));
            var destPtr = (char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(destination));
            var written = _decoder.GetChars(srcPtr, source.Length, destPtr, charCount, true);
            _weaver.Expand(written);
        }
    }

    /// <summary>
    /// Releases the reference to the underlying <see cref="StringWeaver"/>.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            onDispose();
            _weaver = null;
        }
        base.Dispose(disposing);
    }
}