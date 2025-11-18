namespace StringWeaver.Tests;

public sealed class UnsafeStringWeaverTests
{
    [Fact]
    public void DefaultConstructor_InitializesWithDefaultCapacity()
    {
        var weaver = new UnsafeStringWeaver();

        Assert.Equal(0, weaver.End);
        Assert.True(weaver.Capacity >= 1024);
    }

    [Fact]
    public void CapacityConstructor_InitializesWithSpecifiedCapacity()
    {
        var weaver = new UnsafeStringWeaver(2048);

        Assert.Equal(0, weaver.End);
        Assert.True(weaver.Capacity >= 2048);
    }

    [Fact]
    public void StringConstructor_CopiesContent()
    {
        var initial = "Hello World";
        var weaver = new UnsafeStringWeaver(initial);

        Assert.Equal(initial.Length, weaver.End);
        Assert.Equal(initial, weaver.ToString());
    }

    [Fact]
    public void StringConstructor_WithEmptyString_InitializesEmpty()
    {
        var weaver = new UnsafeStringWeaver(string.Empty);

        Assert.Equal(0, weaver.End);
        Assert.True(weaver.Capacity >= 1024);
    }

    [Fact]
    public void StringCapacityConstructor_CopiesContentWithCapacity()
    {
        var initial = "Test";
        var weaver = new UnsafeStringWeaver(initial, 2048);

        Assert.Equal(initial.Length, weaver.End);
        Assert.Equal(initial, weaver.ToString());
        Assert.True(weaver.Capacity >= 2048);
    }

    [Fact]
    public void StringCapacityConstructor_ThrowsWhenCapacityTooSmall() => Assert.Throws<ArgumentOutOfRangeException>(() => new UnsafeStringWeaver("Hello", 2));

    [Fact]
    public void SpanConstructor_CopiesContent()
    {
        var initial = "Span Content".AsSpan();
        var weaver = new UnsafeStringWeaver(initial);

        Assert.Equal(initial.Length, weaver.End);
        Assert.Equal("Span Content", weaver.ToString());
    }

    [Fact]
    public void SpanConstructor_WithEmptySpan_InitializesEmpty()
    {
        var weaver = new UnsafeStringWeaver([]);

        Assert.Equal(0, weaver.End);
        Assert.True(weaver.Capacity >= 1024);
    }

    [Fact]
    public void SpanCapacityConstructor_CopiesContentWithCapacity()
    {
        var initial = "Test".AsSpan();
        var weaver = new UnsafeStringWeaver(initial, 2048);

        Assert.Equal(initial.Length, weaver.End);
        Assert.Equal("Test", weaver.ToString());
        Assert.True(weaver.Capacity >= 2048);
    }

    [Fact]
    public void SpanCapacityConstructor_ThrowsWhenCapacityTooSmall() => Assert.Throws<ArgumentOutOfRangeException>(() =>
    {
        var span = "Hello".AsSpan();
        new UnsafeStringWeaver(span, 2);
    });

    [Fact]
    public void SpanCapacityConstructor_UsesDefaultCapacityWhenSmaller()
    {
        var span = "Hi".AsSpan();
        var weaver = new UnsafeStringWeaver(span, 512);

        Assert.Equal(2, weaver.End);
        Assert.True(weaver.Capacity >= 1024);
    }

    [Fact]
    public void CopyConstructor_CreatesIndependentCopy()
    {
        var original = new UnsafeStringWeaver("Original");
        var weaver = new UnsafeStringWeaver(original);

        Assert.Equal(original.End, weaver.End);
        Assert.Equal(original.ToString(), weaver.ToString());
        Assert.NotEqual(original.PointerValue, weaver.PointerValue);

        original.Dispose();
    }

    [Fact]
    public void CopyConstructor_ThrowsWhenOtherIsNull() => Assert.Throws<ArgumentNullException>(() => new UnsafeStringWeaver((UnsafeStringWeaver)null!));

    [Fact]
    public void PointerValue_ReturnsNonZero()
    {
        var weaver = new UnsafeStringWeaver();

        Assert.NotEqual(IntPtr.Zero, weaver.PointerValue);
    }

    [Fact]
    public unsafe void Pointer_ReturnsValidPointer()
    {
        var weaver = new UnsafeStringWeaver("Test");

        Assert.NotEqual((nint)(char*)0, (nint)weaver.Pointer);
        Assert.Equal('T', *weaver.Pointer);
    }

    [Fact]
    public void FullMemory_ReturnsMemoryOverEntireBuffer()
    {
        var weaver = new UnsafeStringWeaver("Test", 2048);

        var memory = weaver.UsableMemory;
        Assert.True(memory.Length >= 2048);
    }

    [Fact]
    public void Grow_IncreasesCapacity()
    {
        var weaver = new UnsafeStringWeaver(100);
        var initialCapacity = weaver.Capacity;

        for (var i = 0; i < initialCapacity + 1; i++)
        {
            weaver.Append('X');
        }

        Assert.True(weaver.Capacity > initialCapacity);
        Assert.Equal(initialCapacity + 1, weaver.End);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var weaver = new UnsafeStringWeaver("Test");

        weaver.Dispose();
        weaver.Dispose();
    }

    [Fact]
    public void SpanCapacityConstructor_HandlesExactDefaultCapacity()
    {
        var span = "Hi".AsSpan();
        var weaver = new UnsafeStringWeaver(span, 1024);

        Assert.Equal(2, weaver.End);
        Assert.True(weaver.Capacity >= 1024);
    }

    [Fact]
    public void SpanCapacityConstructor_HandlesCapacityEqualToContentLength()
    {
        var span = "TestContent".AsSpan();
        var weaver = new UnsafeStringWeaver(span, span.Length);

        Assert.Equal(span.Length, weaver.End);
        Assert.Equal("TestContent", weaver.ToString());
    }

    [Fact]
    public void SpanCapacityConstructor_HandlesLargeCapacity()
    {
        var span = "Small".AsSpan();
        var weaver = new UnsafeStringWeaver(span, 10000);

        Assert.Equal(5, weaver.End);
        Assert.True(weaver.Capacity >= 10000);
    }

    [Fact]
    public void CopyConstructor_CopiesEmptyWeaver()
    {
        var original = new UnsafeStringWeaver();
        var weaver = new UnsafeStringWeaver(original);

        Assert.Equal(0, weaver.End);
        original.Dispose();
    }

    [Fact]
    public void CopyConstructor_CopiesCapacity()
    {
        var original = new UnsafeStringWeaver("Test", 4096);
        var weaver = new UnsafeStringWeaver(original);

        Assert.True(weaver.Capacity >= 4096);
        original.Dispose();
    }
}