namespace StringWeaver.Tests.IO;

public class WeaverStreamTests
{
    [Fact]
    public void GetStream_ReturnsStreamWithDefaultEncoding()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();

        Assert.NotNull(stream);
        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
    }

    [Fact]
    public void GetStream_ReturnsStreamWithSpecifiedEncoding()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.UTF8);

        Assert.NotNull(stream);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void Write_ByteArray_AppendsDecodedText()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.UTF8);
        var data = Encoding.UTF8.GetBytes("Hello");
        stream.Write(data, 0, data.Length);
        Assert.Equal("Hello", weaver.ToString());
    }

    [Fact]
    public void Write_ByteArray_WithOffsetAndCount_WritesSubset()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.UTF8);
        var data = Encoding.UTF8.GetBytes("ABCDEFG");
        stream.Write(data, 2, 3); // CDE
        Assert.Equal("CDE", weaver.ToString());
    }

    [Fact]
    public void Write_ByteArray_ZeroCount_DoesNotChangeWeaver()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.UTF8);
        var before = weaver.ToString();
        var data = Encoding.UTF8.GetBytes("XYZ");
        stream.Write(data, 0, 0);
        Assert.Equal(before, weaver.ToString());
    }

    [Fact]
    public void Write_Span_MultiByteCharacters()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.UTF8);
        var text = "Hello 😀 Привет";
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes.AsSpan());
        Assert.Equal(text, weaver.ToString());
    }

    [Fact]
    public void Write_ByteArray_NullBuffer_Throws()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();
        Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(5, 10)]
    [InlineData(3, 3)] // offset + count == length works only if not exceeding; here length set smaller
    public void Write_ByteArray_InvalidRange_Throws(int offset, int count)
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();
        var buffer = new byte[5];
        if (offset == 3 && count == 3)
        {
            // Make total length less than offset+count to force error
            buffer = new byte[5]; // offset+count = 6 > 5
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, offset, count));
    }

    [Fact]
    public void Flush_DoesNotThrow()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();
        stream.Flush();
    }

    [Fact]
    public void Read_NotSupported_Throws()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[10], 0, 10));
    }

    [Fact]
    public void Length_NotSupported_Throws()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();
        Assert.Throws<NotSupportedException>(() => { var _ = stream.Length; });
    }

    [Fact]
    public void PositionGet_NotSupported_Throws()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();
        Assert.Throws<NotSupportedException>(() => { var _ = stream.Position; });
    }

    [Fact]
    public void Seek_NotSupported_Throws()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, System.IO.SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }

    [Fact]
    public void SetLength_NotSupported_Throws()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();
        Assert.Throws<NotSupportedException>(() => stream.SetLength(10));
    }

    [Fact]
    public void Write_UnicodeEncoding_PreservesCharacters()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.Unicode);
        var text = "ÅβÇД";
        var bytes = Encoding.Unicode.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
        Assert.Equal(text, weaver.ToString());
    }

    [Fact]
    public void DisposedStream_Throws()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.Unicode);

        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stream.Write([1, 2, 3]));
    }
    [Fact]
    public async System.Threading.Tasks.Task AsyncDisposedStream_Throws()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.Unicode);

        await stream.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => stream.Write([1, 2, 3]));
    }
}
