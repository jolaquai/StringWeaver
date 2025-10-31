using StringWeaver.Helpers;

namespace StringWeaver.Tests.Helpers;

public unsafe class NativeBufferTests
{
    [Fact]
    public void Constructor_AllocatesMemory()
    {
        using var buffer = new NativeBuffer<int>(10);

        Assert.NotEqual(nint.Zero, buffer.PointerValue);
        Assert.True(buffer.Capacity >= 10);
    }

    [Fact]
    public void Constructor_RoundsCapacityToPowerOf2()
    {
        using var buffer = new NativeBuffer<int>(10);

        Assert.Equal(16, buffer.Capacity);
    }

    [Fact]
    public void Capacity_ReflectsAllocatedElements()
    {
        using var buffer = new NativeBuffer<byte>(100);

        var expectedCapacity = Pow2.NextPowerOf2(100);
        Assert.Equal(expectedCapacity, buffer.Capacity);
    }

    [Fact]
    public void CapacityBytes_CalculatesCorrectSize()
    {
        using var buffer = new NativeBuffer<int>(10);

        Assert.Equal((long)buffer.Capacity * sizeof(int), buffer.CapacityBytes);
    }

    [Fact]
    public void Grow_IncreasesCapacity()
    {
        using var buffer = new NativeBuffer<int>(4);
        var initialCapacity = buffer.Capacity;

        buffer.Grow(100);

        Assert.True(buffer.Capacity > initialCapacity);
        Assert.True(buffer.Capacity >= 100);
    }

    [Fact]
    public void Grow_IncrementsVersion()
    {
        using var buffer = new NativeBuffer<int>(4);
        var initialVersion = buffer.Version;

        buffer.Grow(100);

        Assert.Equal(initialVersion + 1, buffer.Version);
    }

    [Fact]
    public void Grow_NoParameters_DoublesCapacity()
    {
        using var buffer = new NativeBuffer<int>(8);
        var initialCapacity = buffer.Capacity;

        buffer.Grow();

        Assert.True(buffer.Capacity > initialCapacity);
    }

    [Fact]
    public void GrowIfNeeded_GrowsWhenRequired()
    {
        using var buffer = new NativeBuffer<int>(4);
        var initialCapacity = buffer.Capacity;

        buffer.GrowIfNeeded(100);

        Assert.True(buffer.Capacity > initialCapacity);
    }

    [Fact]
    public void GrowIfNeeded_DoesNotGrowWhenNotRequired()
    {
        using var buffer = new NativeBuffer<int>(100);
        var initialCapacity = buffer.Capacity;
        var initialVersion = buffer.Version;

        buffer.GrowIfNeeded(10);

        Assert.Equal(initialCapacity, buffer.Capacity);
        Assert.Equal(initialVersion, buffer.Version);
    }

    [Fact]
    public void Wipe_ZeroesMemory()
    {
        using var buffer = new NativeBuffer<int>(10);
        var span = buffer.GetSpan();

        for (var i = 0; i < span.Length; i++)
        {
            span[i] = i + 1;
        }

        buffer.Wipe();

        for (var i = 0; i < span.Length; i++)
        {
            Assert.Equal(0, span[i]);
        }
    }

    [Fact]
    public void GetSpan_ReturnsCorrectLength()
    {
        using var buffer = new NativeBuffer<int>(10);

        var span = buffer.GetSpan();

        Assert.Equal(buffer.Capacity, span.Length);
    }

    [Fact]
    public void GetSpan_PointsToSameMemory()
    {
        using var buffer = new NativeBuffer<int>(10);
        var span = buffer.GetSpan();

        span[5] = 42;

        Assert.Equal(42, buffer.Pointer[5]);
    }

    [Fact]
    public void GetSpan_ThrowsAfterDispose()
    {
        var buffer = new NativeBuffer<int>(10);
        ((IDisposable)buffer).Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.GetSpan());
    }

    [Fact]
    public void Pin_ReturnsValidHandle()
    {
        using var buffer = new NativeBuffer<int>(10);

        var handle = buffer.Pin();

        Assert.NotEqual(nint.Zero, (nint)handle.Pointer);
        handle.Dispose();
    }

    [Fact]
    public void Pin_WithIndex_OffsetsPointer()
    {
        using var buffer = new NativeBuffer<int>(10);

        var handle = buffer.Pin(5);

        Assert.Equal((nint)(buffer.Pointer + 5), (nint)handle.Pointer);
        handle.Dispose();
    }

    [Fact]
    public void Pin_ThrowsForNegativeIndex()
    {
        using var buffer = new NativeBuffer<int>(10);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Pin(-1));
    }

    [Fact]
    public void Pin_ThrowsForIndexBeyondCapacity()
    {
        using var buffer = new NativeBuffer<int>(10);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Pin(buffer.Capacity));
    }

    [Fact]
    public void Pin_ThrowsAfterDispose()
    {
        var buffer = new NativeBuffer<int>(10);
        ((IDisposable)buffer).Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.Pin());
    }

    [Fact]
    public void Unpin_AllowsMultipleCalls()
    {
        using var buffer = new NativeBuffer<int>(10);
        var handle1 = buffer.Pin();
        var handle2 = buffer.Pin();

        handle1.Dispose();
        handle2.Dispose();
    }

    [Fact]
    public void Dispose_FreesMemory()
    {
        var buffer = new NativeBuffer<int>(10);

        ((IDisposable)buffer).Dispose();

        // direct access to internal pointer for test purposes, should never be done, use Pointer or PointerValue instead
        Assert.Equal(nint.Zero, (nint)buffer.pointer);
    }

    [Fact]
    public void Dispose_WithWipeOnDispose_ZeroesMemory()
    {
        var buffer = new NativeBuffer<int>(10, wipeOnDispose: true);
        var span = buffer.GetSpan();

        for (var i = 0; i < span.Length; i++)
        {
            span[i] = i + 1;
        }

        ((IDisposable)buffer).Dispose();
    }

    [Fact]
    public void Dispose_MultipleCallsAreSafe()
    {
        var buffer = new NativeBuffer<int>(10);

        ((IDisposable)buffer).Dispose();
        ((IDisposable)buffer).Dispose();
    }

    [Fact]
    public void Dispose_OutstandingPins_WontLeak()
    {
        var buffer = new NativeBuffer<int>(10);
        var handle = buffer.Pin();

        Assert.False(buffer.disposed);
        Assert.False(buffer.freePending);
        Assert.Equal(1, buffer.pinCount);

        ((IDisposable)buffer).Dispose();

        Assert.True(buffer.disposed);
        Assert.True(buffer.freePending);
        Assert.Equal(1, buffer.pinCount);

        handle.Dispose();

        Assert.Equal(nint.Zero, buffer.PointerValue);
        Assert.True(buffer.disposed);
        Assert.False(buffer.freePending);
        Assert.Equal(0, buffer.pinCount);
    }

    [Fact]
    public void Constructor_WithPressureTrue_ReportsPressure()
    {
        using var buffer = new NativeBuffer<int>(1000, pressure: true);

        Assert.True(buffer.Capacity > 0);
    }

    [Fact]
    public void Constructor_WithPressureFalse_DoesNotReportPressure()
    {
        using var buffer = new NativeBuffer<int>(1000, pressure: false);

        Assert.True(buffer.Capacity > 0);
    }

    [Fact]
    public void Constructor_WithPressureNull_UsesAutomaticPressure()
    {
        using var buffer = new NativeBuffer<int>(1000, pressure: null);

        Assert.True(buffer.Capacity > 0);
    }

    [Fact]
    public void Grow_WithPressureTrue_ReportsPressureOnGrowth()
    {
        using var buffer = new NativeBuffer<int>(10, pressure: true);

        buffer.Grow(10000);

        Assert.True(buffer.Capacity >= 10000);
    }

    [Fact]
    public void Grow_WithPressureFalse_DoesNotReportPressure()
    {
        using var buffer = new NativeBuffer<int>(10, pressure: false);

        buffer.Grow(10000);

        Assert.True(buffer.Capacity >= 10000);
    }

    [Fact]
    public void Grow_WithPressureNull_LargeAllocation_ReportsPressure()
    {
        using var buffer = new NativeBuffer<byte>(10, pressure: null);

        buffer.Grow(20000);

        Assert.True(buffer.Capacity >= 20000);
    }

    [Fact]
    public void Grow_WithPressureNull_SmallAllocation_DoesNotReportPressure()
    {
        using var buffer = new NativeBuffer<byte>(10, pressure: null);

        buffer.Grow(100);

        Assert.True(buffer.Capacity >= 100);
    }

    [Fact]
    public void Grow_WithPressureNull_MultipleLargeAllocations_ReportsPressure()
    {
        using var buffer = new NativeBuffer<byte>(10, pressure: null);

        buffer.Grow(20000);
        buffer.Grow(40000);

        Assert.True(buffer.Capacity >= 40000);
    }

    [Fact]
    public void Pointer_UpdatesAfterGrow()
    {
        using var buffer = new NativeBuffer<int>(10);
        var initialPtr = buffer.Pointer;

        buffer.Grow(10000);

        Assert.NotEqual((nint)initialPtr, (nint)buffer.Pointer);
    }

    [Fact]
    public void Version_IncrementsOnEachGrow()
    {
        using var buffer = new NativeBuffer<int>(4);

        Assert.Equal(1u, buffer.Version);
        buffer.Grow(100);
        Assert.Equal(2u, buffer.Version);
        buffer.Grow(1000);
        Assert.Equal(3u, buffer.Version);
    }

    [Fact]
    public void GetSpan_ForZeroCapacity_ReturnsEmpty()
    {
        using var buffer = new NativeBuffer<int>(0);

        var span = buffer.GetSpan();

        Assert.True(span.IsEmpty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void Constructor_WithPowerOf2_DoesNotOverAllocate(int size)
    {
        using var buffer = new NativeBuffer<int>(size);

        Assert.Equal(size, buffer.Capacity);
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(5, 8)]
    [InlineData(9, 16)]
    [InlineData(17, 32)]
    public void Constructor_RoundsUpToPowerOf2(int requested, int expected)
    {
        using var buffer = new NativeBuffer<int>(requested);

        Assert.Equal(expected, buffer.Capacity);
    }

    [Fact]
    public void Grow_PreservesExistingData()
    {
        using var buffer = new NativeBuffer<int>(4);
        var span = buffer.GetSpan();

        for (var i = 0; i < span.Length; i++)
        {
            span[i] = i * 10;
        }

        var originalValues = span.ToArray();
        buffer.Grow(100);
        span = buffer.GetSpan();

        for (var i = 0; i < originalValues.Length; i++)
        {
            Assert.Equal(originalValues[i], span[i]);
        }
    }

    [Fact]
    public void MultipleBuffers_HaveIndependentMemory()
    {
        using var buffer1 = new NativeBuffer<int>(10);
        using var buffer2 = new NativeBuffer<int>(10);

        var span1 = buffer1.GetSpan();
        var span2 = buffer2.GetSpan();

        span1[0] = 42;
        span2[0] = 99;

        Assert.Equal(42, span1[0]);
        Assert.Equal(99, span2[0]);
    }
}