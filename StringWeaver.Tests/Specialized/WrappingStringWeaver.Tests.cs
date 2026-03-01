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

    #region Constructor Tests - Memory<char>

    [Fact]
    public void Constructor_Memory_WithValidParameters_InitializesCorrectly()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);
        var usedLength = 50;

        using var weaver = new WrappingStringWeaver(memory, usedLength);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(buffer.Length, weaver.Capacity);
    }

    [Fact]
    public void Constructor_Memory_WithPin_InitializesCorrectly()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);
        var usedLength = 50;

        using var weaver = new WrappingStringWeaver(memory, usedLength, pin: true);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(buffer.Length, weaver.Capacity);
    }

    [Fact]
    public void Constructor_Memory_WithIndexAndLength_InitializesCorrectly()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);
        var index = 10;
        var length = 50;
        var usedLength = 25;

        using var weaver = new WrappingStringWeaver(memory, index, length, usedLength);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(length, weaver.Capacity);
    }

    [Fact]
    public void Constructor_Memory_WithZeroUsedLength_InitializesCorrectly()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);

        using var weaver = new WrappingStringWeaver(memory, usedLength: 0);

        Assert.Equal(0, weaver.Length);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Constructor_Memory_WithNegativeUsedLength_ThrowsArgumentOutOfRangeException(int usedLength)
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WrappingStringWeaver(memory, usedLength));
    }

    [Fact]
    public void Constructor_Memory_WithUsedLengthGreaterThanCapacity_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);
        var usedLength = 150;

        Assert.Throws<ArgumentOutOfRangeException>(() => new WrappingStringWeaver(memory, usedLength));
    }

    [Fact]
    public void Constructor_Memory_WithUsedLengthGreaterThanSliceLength_ThrowsArgumentOutOfRangeException()
    {
        // Regression: ValidateRangeForZeroBasedLength compared usedLength against totalLength (the
        // full memory.Length) instead of the slice length, so this silently passed when usedLength
        // exceeded the slice but not the total buffer.
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WrappingStringWeaver(memory, index: 90, length: 5, usedLength: 6));
    }

    [Fact]
    public void Constructor_Memory_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WrappingStringWeaver(memory, index: -1, usedLength: 10));
    }

    [Fact]
    public void Constructor_Memory_WithZeroLength_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WrappingStringWeaver(memory, index: 0, length: 0, usedLength: 0));
    }

    [Fact]
    public void Constructor_Memory_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WrappingStringWeaver(memory, index: 0, length: -1, usedLength: 0));
    }

    [Fact]
    public void Constructor_Memory_WithIndexPlusLengthExceedingCapacity_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new char[100];
        var memory = new Memory<char>(buffer);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WrappingStringWeaver(memory, index: 50, length: 60, usedLength: 10));
    }

    #endregion

    #region Constructor Tests - Span<char>

    [Fact]
    public void Constructor_Span_WithValidParameters_InitializesCorrectly()
    {
        Span<char> buffer = stackalloc char[100];
        var usedLength = 50;

        using var weaver = new WrappingStringWeaver(buffer, usedLength);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(buffer.Length, weaver.Capacity);
    }

    [Fact]
    public void Constructor_Span_WithIndexAndLength_InitializesCorrectly()
    {
        Span<char> buffer = stackalloc char[100];
        var index = 10;
        var length = 50;
        var usedLength = 25;

        using var weaver = new WrappingStringWeaver(buffer, index, length, usedLength);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(length, weaver.Capacity);
    }

    [Fact]
    public void Constructor_Span_WithNegativeUsedLength_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Span<char> buffer = stackalloc char[100];
            return new WrappingStringWeaver(buffer, usedLength: -1);
        });
    }

    [Fact]
    public void Constructor_Span_WithUsedLengthGreaterThanCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Span<char> buffer = stackalloc char[100];
            return new WrappingStringWeaver(buffer, usedLength: 150);
        });
    }

    #endregion

    #region Constructor Tests - char[]

    [Fact]
    public void Constructor_Array_WithValidParameters_InitializesCorrectly()
    {
        var buffer = new char[100];
        var usedLength = 50;

        using var weaver = new WrappingStringWeaver(buffer, usedLength);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(buffer.Length, weaver.Capacity);
    }

    [Fact]
    public void Constructor_Array_WithPin_InitializesCorrectly()
    {
        var buffer = new char[100];
        var usedLength = 50;

        using var weaver = new WrappingStringWeaver(buffer, usedLength, pin: true);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(buffer.Length, weaver.Capacity);
    }

    [Fact]
    public void Constructor_Array_WithIndexAndLength_InitializesCorrectly()
    {
        var buffer = new char[100];
        var index = 10;
        var length = 50;
        var usedLength = 25;

        using var weaver = new WrappingStringWeaver(buffer, index, length, usedLength);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(length, weaver.Capacity);
    }

    [Fact]
    public void Constructor_Array_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new char[100];

        Assert.Throws<ArgumentOutOfRangeException>(() => new WrappingStringWeaver(buffer, index: -1, usedLength: 10));
    }

    #endregion

    #region Constructor Tests - Pointer

    [Fact]
    public unsafe void Constructor_Pointer_WithValidParameters_InitializesCorrectly()
    {
        var buffer = new char[100];
        var length = 100;
        var usedLength = 50;

        fixed (char* ptr = buffer)
        {
            using var weaver = new WrappingStringWeaver(ptr, length, usedLength);

            Assert.Equal(usedLength, weaver.Length);
            Assert.Equal(length, weaver.Capacity);
        }
    }

    [Fact]
    public unsafe void Constructor_Pointer_WithZeroLength_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var buffer = new char[100];
            fixed (char* ptr = buffer)
            {
                return new WrappingStringWeaver(ptr, length: 0, usedLength: 0);
            }
        });
    }

    [Fact]
    public unsafe void Constructor_Pointer_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var buffer = new char[100];
            fixed (char* ptr = buffer)
            {
                return new WrappingStringWeaver(ptr, length: -1, usedLength: 0);
            }
        });
    }

    [Fact]
    public void Constructor_RefChar_WithValidParameters_InitializesCorrectly()
    {
        var buffer = new char[100];
        var length = 100;
        var usedLength = 50;

        using var weaver = new WrappingStringWeaver(ref buffer[0], length, usedLength);

        Assert.Equal(usedLength, weaver.Length);
        Assert.Equal(length, weaver.Capacity);
    }

    #endregion

    #region Functionality Tests

    [Fact]
    public void Append_WithinCapacity_WorksCorrectly()
    {
        var buffer = new char[100];
        using var weaver = new WrappingStringWeaver(buffer, usedLength: 0);

        weaver.Append("Hello");
        weaver.Append(' ');
        weaver.Append("World");

        Assert.Equal("Hello World", weaver.ToString());
        Assert.Equal(11, weaver.Length);
    }

    [Fact]
    public void Append_ExceedingCapacity_ThrowsNotSupportedException()
    {
        var buffer = new char[10];
        using var weaver = new WrappingStringWeaver(buffer, usedLength: 0);

        var exception = Assert.Throws<NotSupportedException>(() => weaver.Append("This string is way too long for the buffer"));
        Assert.Contains("WrappingStringWeaver", exception.Message);
    }

    [Fact]
    public void PrePopulatedBuffer_ReadsExistingContent()
    {
        var buffer = "Hello World".ToCharArray();
        var usedLength = "Hello".Length;

        using var weaver = new WrappingStringWeaver(buffer, usedLength);

        Assert.Equal("Hello", weaver.ToString());
        Assert.Equal(5, weaver.Length);
    }

    [Fact]
    public void PrePopulatedBuffer_CanAppend()
    {
        var buffer = new char[100];
        "Hello".AsSpan().CopyTo(buffer);
        using var weaver = new WrappingStringWeaver(buffer, usedLength: 5);

        weaver.Append(" World");

        Assert.Equal("Hello World", weaver.ToString());
        Assert.Equal(11, weaver.Length);
    }

    [Fact]
    public void Clear_ResetsLength()
    {
        var buffer = new char[100];
        using var weaver = new WrappingStringWeaver(buffer, usedLength: 0);
        weaver.Append("Hello");

        weaver.Clear();

        Assert.Equal(0, weaver.Length);
        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void ToSpan_ReturnsCorrectContent()
    {
        var buffer = new char[100];
        using var weaver = new WrappingStringWeaver(buffer, usedLength: 0);
        weaver.Append("Test");

        var span = weaver.Span;

        Assert.Equal(4, span.Length);
        Assert.Equal("Test", new string(span));
    }

    [Fact]
    public void FullMemory_ReturnsEntireBuffer()
    {
        var buffer = new char[100];
        using var weaver = new WrappingStringWeaver(buffer, usedLength: 10);

        var memory = weaver.FullMemory;

        Assert.Equal(100, memory.Length);
    }

    #endregion

    #region Pinning Tests

    [Fact]
    public void Constructor_WithPinTrue_BufferIsPinned()
    {
        var buffer = new char[100];
        buffer[0] = 'A';
        buffer[1] = 'B';

        using var weaver = new WrappingStringWeaver(buffer, usedLength: 2, pin: true);

        Assert.Equal("AB", weaver.ToString());
    }

    [Fact]
    public void Constructor_WithPinFalse_WorksWithManagedMemory()
    {
        var buffer = new char[100];
        buffer[0] = 'X';
        buffer[1] = 'Y';

        using var weaver = new WrappingStringWeaver(buffer, usedLength: 2, pin: false);

        Assert.Equal("XY", weaver.ToString());
    }

    #endregion

    #region Slice Tests

    [Fact]
    public void Constructor_WithSlicedMemory_UsesCorrectPortion()
    {
        var buffer = new char[100];
        "0123456789ABCDEFGHIJ".AsSpan().CopyTo(buffer);
        var memory = new Memory<char>(buffer);

        using var weaver = new WrappingStringWeaver(memory, index: 10, length: 10, usedLength: 5);

        Assert.Equal("ABCDE", weaver.ToString());
        Assert.Equal(10, weaver.Capacity);
    }

    [Fact]
    public void Constructor_WithSlicedArray_UsesCorrectPortion()
    {
        var buffer = "0123456789ABCDEFGHIJ".ToCharArray();

        using var weaver = new WrappingStringWeaver(buffer, index: 5, length: 10, usedLength: 3);

        Assert.Equal("567", weaver.ToString());
        Assert.Equal(10, weaver.Capacity);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var buffer = new char[100];
        var weaver = new WrappingStringWeaver(buffer, usedLength: 0);

        weaver.Dispose();
        weaver.Dispose();
    }

    [Fact]
    public void Dispose_WithPinnedMemory_ReleasesHandle()
    {
        var buffer = new char[100];
        var weaver = new WrappingStringWeaver(buffer, usedLength: 0, pin: true);

        weaver.Dispose();
    }

    [Fact]
    public void Finalizer_DoesNotThrow()
    {
        static void CreateAndAbandon()
        {
            var buffer = new char[100];
            var weaver = new WrappingStringWeaver(buffer, usedLength: 0, pin: true);
        }

        CreateAndAbandon();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        Assert.True(true);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_WithMinimumCapacity_WorksCorrectly()
    {
        var buffer = new char[1];

        using var weaver = new WrappingStringWeaver(buffer, usedLength: 0);

        Assert.Equal(0, weaver.Length);
        Assert.Equal(1, weaver.Capacity);
    }

    [Fact]
    public void Constructor_WithUsedLengthEqualToCapacity_WorksCorrectly()
    {
        var buffer = "Full".ToCharArray();

        using var weaver = new WrappingStringWeaver(buffer, usedLength: 4);

        Assert.Equal("Full", weaver.ToString());
        Assert.Equal(4, weaver.Length);
        Assert.Equal(4, weaver.Capacity);
    }

    [Fact]
    public void Append_SingleCharacterAtCapacity_ThrowsNotSupportedException()
    {
        var buffer = new char[5];
        using var weaver = new WrappingStringWeaver(buffer, usedLength: 0);
        weaver.Append("Hello");

        Assert.Throws<NotSupportedException>(() => weaver.Append('!'));
    }

    [Fact]
    public unsafe void Constructor_Pointer_WithStackAllocatedBuffer_WorksCorrectly()
    {
        Span<char> buffer = stackalloc char[50];
        "StackBuffer".AsSpan().CopyTo(buffer);

        fixed (char* ptr = buffer)
        {
            using var weaver = new WrappingStringWeaver(ptr, length: 50, usedLength: 11);

            Assert.Equal("StackBuffer", weaver.ToString());
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IntegrationTest_ComplexScenario_WithMultipleOperations()
    {
        var buffer = new char[200];
        using var weaver = new WrappingStringWeaver(buffer, usedLength: 0);

        weaver.Append("Line 1");
        weaver.Append('\n');
        weaver.Append("Line 2");
        var length1 = weaver.Length;

        weaver.Clear();
        weaver.Append("Reset");
        var length2 = weaver.Length;

        Assert.Equal(13, length1);
        Assert.Equal(5, length2);
        Assert.Equal("Reset", weaver.ToString());
    }

    [Fact]
    public void IntegrationTest_ReusingBuffer_AcrossMultipleWeavers()
    {
        var buffer = new char[100];

        using (var weaver1 = new WrappingStringWeaver(buffer, usedLength: 0))
        {
            weaver1.Append("First");
            Assert.Equal("First", weaver1.ToString());
        }

        using (var weaver2 = new WrappingStringWeaver(buffer, usedLength: 5))
        {
            Assert.Equal("First", weaver2.ToString());
            weaver2.Append(" Second");
            Assert.Equal("First Second", weaver2.ToString());
        }
    }

    #endregion
}
