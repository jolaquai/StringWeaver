using System.Runtime.InteropServices;

using StringWeaver.Specialized;

namespace StringWeaver.Tests.Specialized;

public class WrappingStringWeaverTests
{
    [Fact]
    public void WSW_OverStackalloc_Works()
    {
        const string InitialContent = "Hello, World!";
        const string From = "World";
        const string To = "Universe";
        const string Expected = "Hello, Universe!";

        Span<char> buffer = stackalloc char[InitialContent.Length + To.Length - From.Length];

        InitialContent.AsSpan().CopyTo(buffer);
        var wsw = new WrappingStringWeaver(buffer, InitialContent.Length);

        wsw.ReplaceAll(From, To);
        Assert.Equal(Expected, wsw.Span);
    }
    [Fact]
    public unsafe void WSW_OverUnmanagedBuffer_Works()
    {
        const string InitialContent = "Hello, World!";
        const string From = "World";
        const string To = "Universe";
        const string Expected = "Hello, Universe!";
        var capacityBytes = (InitialContent.Length + To.Length - From.Length) * sizeof(char);
        var capacityChars = capacityBytes / sizeof(char);

        var hglobalBuffer = (char*)Marshal.AllocHGlobal(capacityBytes);
        try
        {
            InitialContent.AsSpan().CopyTo(new Span<char>(hglobalBuffer, InitialContent.Length));

            using var wsw = new WrappingStringWeaver(hglobalBuffer, capacityChars, InitialContent.Length);

            wsw.ReplaceAll(From, To);
            Assert.Equal(Expected, wsw.Span);
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)hglobalBuffer);
        }
    }
}
