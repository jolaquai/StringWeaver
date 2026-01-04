using SW = StringWeaver.StringWeaver;

namespace StringWeaver.Tests;

public class StringWeaverTests
{
    #region One-offs
    [Theory]
    [InlineData("B")]
    [InlineData("CC")]
    [InlineData("DDD")]
    public void ReplaceAll_LiteralOrRegex_ProduceSameResult(string replacement)
    {
        const string InitialContent = "bbAAAA";

        var swLiteral = new SW(InitialContent);
        var swRegex = new SW(InitialContent);
        var swPcre = new SW(InitialContent);

        swLiteral.ReplaceAll("AA", replacement, 0, 5);
        swRegex.ReplaceAll(new Regex("AA"), replacement, 0, 5);
        swPcre.ReplaceAll(new PcreRegex("AA"), replacement, 0, 5);

        var expected = $"bb{replacement}AA";
        Assert.Equal(expected, swLiteral.Span);
        Assert.Equal(expected, swRegex.Span);
        Assert.Equal(expected, swPcre.Span);
    }
    #endregion

    #region Constructor Tests
    [Fact]
    public void Constructor_DefaultCapacity_InitializesEmpty()
    {
        var sw = new SW();
        Assert.Equal(0, sw.Length);
        Assert.True(sw.Capacity >= SW.DefaultCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Constructor_WithCapacity_SetsCorrectCapacity(int capacity)
    {
        var sw = new SW(capacity);
        Assert.Equal(0, sw.Length);
        Assert.True(sw.Capacity >= capacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Hello World")]
    [InlineData("This is a longer string to test initialization")]
    public void Constructor_WithString_CopiesContent(string initial)
    {
        var sw = new SW(initial);
        Assert.Equal(initial, sw.ToString());
        Assert.Equal(initial.Length, sw.Length);
    }

    [Fact]
    public void Constructor_StringCapacity_ThrowsWhenCapacityTooSmall() => Assert.Throws<ArgumentOutOfRangeException>(() => new SW("test", 2));

    [Fact]
    public void Constructor_Copy_CreatesIndependentCopy()
    {
        var original = new SW("original");
        var copy = new SW(original);

        Assert.Equal(original.ToString(), copy.ToString());
        original.Append("modified");
        Assert.NotEqual(original.ToString(), copy.ToString());
    }

    [Fact]
    public void Constructor_NullCopy_Throws() => Assert.Throws<ArgumentNullException>(() => new SW((SW)null));

    [Fact]
    public void Constructor_ReadOnlySpan_CreatesCorrectly()
    {
        var span = "test content".AsSpan();
        var sw = new SW(span);
        Assert.Equal("test content", sw.ToString());
    }

    [Fact]
    public void Constructor_ReadOnlySpanWithCapacity_CreatesCorrectly()
    {
        var span = "test".AsSpan();
        var sw = new SW(span, 100);
        Assert.Equal("test", sw.ToString());
        Assert.True(sw.Capacity >= 100);
    }

    [Fact]
    public void Constructor_Utf8ByteArray_DecodesCorrectly()
    {
        var utf8Bytes = new byte[] { 0xE2, 0x9C, 0x93 }; // Check mark character
        var sw = new SW(utf8Bytes);
        Assert.Equal("✓", sw.ToString());
    }
    [Fact]
    public void Constructor_Utf8Literal_DecodesCorrectly()
    {
        var sw = new SW("✓"u8);
        Assert.Equal("✓", sw.ToString());
    }
    [Fact]
    public void Constructor_WithEncoding_DecodesCorrectly()
    {
        // ASCII, cyrillic, and accented characters
        var bytes = Encoding.UTF32.GetBytes("café привет");

        var sw = new SW(bytes, Encoding.UTF32);
        Assert.Equal("café привет", sw.Span);
    }
    [Fact]
    public void Constructor_WithEncoding_RespectsCapacity_ThrowsOnTooSmall()
    {
        var bytes = Encoding.UTF32.GetBytes("café привет");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new SW(bytes, Encoding.UTF32, 10); // 1 too small
        });
    }
    #endregion

    #region Indexer Tests
    [Fact]
    public void Indexer_Get_ReturnsCorrectChar()
    {
        var sw = new SW("abcdef");
        Assert.Equal('a', sw[0]);
        Assert.Equal('f', sw[^1]);
        Assert.Equal('c', sw[2]);
    }

    [Fact]
    public void Indexer_Set_ModifiesChar()
    {
        var sw = new SW("test");
        sw[0] = 'b';
        Assert.Equal("best", sw.ToString());
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw[10]);
        Assert.Throws<ArgumentOutOfRangeException>(() => sw[10] = 'x');
    }

    [Fact]
    public void Indexer_FromEnd_WorksCorrectly()
    {
        var sw = new SW("hello");
        Assert.Equal('o', sw[^1]);
        Assert.Equal('l', sw[^2]);
        sw[^1] = 'x';
        Assert.Equal("hellx", sw.ToString());
    }
    #endregion

    #region Append Tests
    [Fact]
    public void Append_MultipleChars_BuildsString()
    {
        var sw = new SW();
        for (var i = 0; i < 100; i++)
        {
            sw.Append('a');
        }

        Assert.Equal(100, sw.Length);
        Assert.Equal(new string('a', 100), sw.ToString());
    }

    [Fact]
    public void Append_CharArray_WithOffset()
    {
        var sw = new SW();
        var chars = "0123456789".ToCharArray();
        sw.Append(chars, 2, 5);
        Assert.Equal("23456", sw.ToString());
    }

    [Fact]
    public void Append_NullArray_Throws()
    {
        var sw = new SW();
        Assert.Throws<ArgumentNullException>(() => sw.Append((char[])null, 0, 1));
    }

    [Fact]
    public void Append_InvalidRange_Throws()
    {
        var sw = new SW();
        var chars = new char[5];
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Append(chars, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Append(chars, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Append(chars, 3, 4));
    }

    [Fact]
    public unsafe void Append_UnsafePointer_AppendsCorrectly()
    {
        var sw = new SW();
        var str = "test";
        fixed (char* ptr = str)
        {
            sw.Append(ptr, str.Length);
        }
        Assert.Equal("test", sw.ToString());
    }

    [Fact]
    public unsafe void Append_NullPointer_Throws()
    {
        var sw = new SW();
        Assert.Throws<ArgumentNullException>(() => sw.Append((char*)null, 5));
    }

    [Fact]
    public void AppendLine_Empty_AddsNewLine()
    {
        var sw = new SW();
        sw.AppendLine();
        Assert.Equal(Environment.NewLine, sw.ToString());
    }

    [Fact]
    public void AppendLine_WithContent_AddsContentAndNewLine()
    {
        var sw = new SW();
        sw.AppendLine("test");
        Assert.Equal("test" + Environment.NewLine, sw.ToString());
    }

    [Fact]
    public void AppendLine_Char_AddsCharAndNewLine()
    {
        var sw = new SW();
        sw.AppendLine('x');
        Assert.Equal("x" + Environment.NewLine, sw.ToString());
    }

    [Fact]
    public void AppendLine_Span_AddsSpanAndNewLine()
    {
        var sw = new SW();
        sw.AppendLine("test".AsSpan());
        Assert.Equal("test" + Environment.NewLine, sw.ToString());
    }

    [Fact]
    public void AppendLine_CharArray_AddsArrayAndNewLine()
    {
        var sw = new SW();
        sw.AppendLine(['t', 'e', 's', 't'], 0, 4);
        Assert.Equal("test" + Environment.NewLine, sw.ToString());
    }

    [Fact]
    public void Append_Object_AppendsToString()
    {
        var sw = new SW();
        sw.Append(123);
        Assert.Equal("123", sw.ToString());
    }

    [Fact]
    public void AppendLine_Object_AppendsToStringWithNewLine()
    {
        var sw = new SW();
        sw.AppendLine(456);
        Assert.Equal("456" + Environment.NewLine, sw.ToString());
    }
    #endregion

    #region IndexOf Tests
    [Fact]
    public void IndexOf_Char_FindsAllOccurrences()
    {
        var sw = new SW("abcabc");
        Assert.Equal(0, sw.IndexOf('a'));
        Assert.Equal(1, sw.IndexOf('b', 0));
        Assert.Equal(4, sw.IndexOf('b', 2));
        Assert.Equal(-1, sw.IndexOf('z'));
    }

    [Fact]
    public void IndexOf_Span_FindsCorrectIndex()
    {
        var sw = new SW("hello world");
        Assert.Equal(6, sw.IndexOf("world".AsSpan()));
        Assert.Equal(0, sw.IndexOf("hello".AsSpan()));
        Assert.Equal(-1, sw.IndexOf("xyz".AsSpan()));
    }

    [Fact]
    public void IndexOf_WithRange_SearchesCorrectly()
    {
        var sw = new SW("0123456789");
        Assert.Equal(5, sw.IndexOf('5', 0, 10));
        Assert.Equal(-1, sw.IndexOf('5', 0, 4));
        Assert.Equal(-1, sw.IndexOf('5', 6, 4));
    }

    [Fact]
    public void IndexOf_EmptySpan_ReturnsZero()
    {
        var sw = new SW("test");
        Assert.Equal(0, sw.IndexOf([]));
    }

    [Fact]
    public void IndexOfAny_FindsFirstMatch()
    {
        var sw = new SW("hello world");
        Assert.Equal(2, sw.IndexOfAny("lo".AsSpan()));
        Assert.Equal(0, sw.IndexOfAny("he".AsSpan()));
    }

    [Fact]
    public void IndexOfAny_WithRange_FindsCorrectly()
    {
        var sw = new SW("0123456789");
        Assert.Equal(5, sw.IndexOfAny("56".AsSpan(), 0, 10));
        Assert.Equal(-1, sw.IndexOfAny("56".AsSpan(), 0, 4));
    }

    [Fact]
    public void IndexOfAnyExcept_FindsFirstNonMatch()
    {
        var sw = new SW("aaabbbccc");
        Assert.Equal(3, sw.IndexOfAnyExcept("a".AsSpan()));
        Assert.Equal(6, sw.IndexOfAnyExcept("ab".AsSpan()));
    }

    [Fact]
    public void IndexOfAnyExcept_WithRange_FindsCorrectly()
    {
        var sw = new SW("aaabbbccc");
        Assert.Equal(3, sw.IndexOfAnyExcept("a".AsSpan(), 0, 9));
        Assert.Equal(-1, sw.IndexOfAnyExcept("a".AsSpan(), 0, 2));
    }

    [Fact]
    public void IndexOfAnyInRange_FindsInRange()
    {
        var sw = new SW("abc123def");
        Assert.Equal(3, sw.IndexOfAnyInRange('0', '9'));
        Assert.Equal(0, sw.IndexOfAnyInRange('a', 'z'));
    }

    [Fact]
    public void IndexOfAnyInRange_WithRange_FindsCorrectly()
    {
        var sw = new SW("abc123def");
        Assert.Equal(3, sw.IndexOfAnyInRange('0', '9', 0, 9));
        Assert.Equal(-1, sw.IndexOfAnyInRange('0', '9', 0, 2));
    }

    [Fact]
    public void IndexOfAnyExceptInRange_FindsOutsideRange()
    {
        var sw = new SW("123abc");
        Assert.Equal(3, sw.IndexOfAnyExceptInRange('0', '9'));
        Assert.Equal(0, sw.IndexOfAnyExceptInRange('a', 'z'));
    }

    [Fact]
    public void IndexOfAnyExceptInRange_WithRange_FindsCorrectly()
    {
        var sw = new SW("123abc");
        Assert.Equal(3, sw.IndexOfAnyExceptInRange('0', '9', 0, 6));
        Assert.Equal(-1, sw.IndexOfAnyExceptInRange('0', '9', 0, 2));
    }

    [Fact]
    public void EnumerateIndicesOf_Char_EnumeratesCorrectly()
    {
        var sw = new SW("ababab");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf('a'))
        {
            indices.Add(idx);
        }

        Assert.Collection(indices,
            i => Assert.Equal(0, i),
            i => Assert.Equal(2, i),
            i => Assert.Equal(4, i)
        );
    }

    [Fact]
    public void EnumerateIndicesOf_Span_EnumeratesCorrectly()
    {
        var sw = new SW("test test test");
        var indexEnumerator = sw.EnumerateIndicesOf("test".AsSpan());
        var indices = new List<int>();
        foreach (var index in indexEnumerator)
        {
            indices.Add(index);
        }
        Assert.Collection(indices,
            i => Assert.Equal(0, i),
            i => Assert.Equal(5, i),
            i => Assert.Equal(10, i)
        );
    }

    [Fact]
    public void EnumerateIndicesOf_ModifiedDuringEnumeration_Throws()
    {
        var sw = new SW("test");
        var enumerator = sw.EnumerateIndicesOf('t').GetEnumerator();
        enumerator.MoveNext();
        sw.Append('x');
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_AllowsModification()
    {
        var sw = new SW("test");
        var count = 0;
        foreach (var _ in sw.EnumerateIndicesOfUnsafe('t'))
        {
            count++;
            sw.Append('x');
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_Span_EnumeratesCorrectly()
    {
        var sw = new SW("abcabcabc");
        var indexEnumerator = sw.EnumerateIndicesOfUnsafe("abc".AsSpan());
        var indices = new List<int>();
        foreach (var index in indexEnumerator)
        {
            indices.Add(index);
        }
        Assert.Collection(indices,
            i => Assert.Equal(0, i),
            i => Assert.Equal(3, i),
            i => Assert.Equal(6, i)
        );
    }
    #endregion

    #region Replace Tests
    [Fact]
    public void Replace_Char_ReplacesFirstOccurrence()
    {
        var sw = new SW("hello");
        sw.Replace('l', 'x');
        Assert.Equal("hexlo", sw.ToString());
    }

    [Fact]
    public void Replace_Char_WithRange_ReplacesCorrectly()
    {
        var sw = new SW("hello");
        sw.Replace('l', 'x', 0, 3);
        Assert.Equal("hexlo", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_Char_ReplacesAllOccurrences()
    {
        var sw = new SW("hello");
        sw.ReplaceAll('l', 'x');
        Assert.Equal("hexxo", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_Char_WithRange_ReplacesAllInRange()
    {
        var sw = new SW("lllhellolll");
        sw.ReplaceAll('l', 'x', 3, 5);
        Assert.Equal("lllhexxolll", sw.ToString());
    }

    [Fact]
    public void Replace_Span_ReplacesCorrectly()
    {
        var sw = new SW("hello world");
        sw.Replace("world".AsSpan(), "universe".AsSpan());
        Assert.Equal("hello universe", sw.ToString());
    }

    [Fact]
    public void Replace_Span_SameLength_ReplacesInPlace()
    {
        var sw = new SW("test");
        sw.Replace("test".AsSpan(), "pass".AsSpan());
        Assert.Equal("pass", sw.ToString());
    }

    [Theory]
    [InlineData("test TEST test", "ABC", "test ABC test", 4)]
    [InlineData("test TEST test", "ABCD", "test ABCD test", 4)]
    [InlineData("test TEST test", "ABCDE", "test ABCDE test", 4)]
    [InlineData("test TEST TEST test", "ABC", "test ABC ABC test", 9)]
    [InlineData("test TEST TEST test", "ABCD", "test ABCD ABCD test", 9)]
    [InlineData("test TEST TEST test", "ABCDE", "test ABCDE ABCDE test", 9)]
    public void ReplaceAll_Span_DifferentLengthReplacements_Work(string seed, string replacement, string expected, int length)
    {
        var sw = new SW(seed);
        sw.ReplaceAll("TEST", replacement, 5, length);
        Assert.Equal(expected, sw.Span);
    }

    [Theory]
    [InlineData("test TEST test", "ABC", "test ABC test", 4)]
    [InlineData("test TEST test", "ABCD", "test ABCD test", 4)]
    [InlineData("test TEST test", "ABCDE", "test ABCDE test", 4)]
    [InlineData("test TEST TEST test", "ABC", "test ABC TEST test", 9)]
    [InlineData("test TEST TEST test", "ABCD", "test ABCD TEST test", 9)]
    [InlineData("test TEST TEST test", "ABCDE", "test ABCDE TEST test", 9)]
    public void Replace_Span_DifferentLengthReplacements_Work(string seed, string replacement, string expected, int length)
    {
        var sw = new SW(seed);
        sw.Replace("TEST", replacement, 5, length);
        Assert.Equal(expected, sw.Span);
    }

    [Fact]
    public void ReplaceAll_Span_LargePossibleMatchCount()
    {
        var sw = new SW();
        sw.Append('b');
        sw.Append('A', 260);
        sw.Append('b');
        sw.ReplaceAll("A", default, 2);
        Assert.Equal("bAb", sw.ToString());
    }

    [Fact]
    public void Replace_EmptySpan_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentException>(() => sw.Replace([], "x".AsSpan()));
    }

    [Fact]
    public void Replace_Range_ReplacesCorrectly()
    {
        var sw = new SW("0123456789");
        sw.Replace(2, 5, "XXX".AsSpan());
        Assert.Equal("01XXX789", sw.ToString());
    }

    [Fact]
    public void Replace_ShorterReplacement_AdjustsLength()
    {
        var sw = new SW("hello world");
        sw.Replace("world".AsSpan(), "x".AsSpan());
        Assert.Equal("hello x", sw.ToString());
    }

    [Fact]
    public void Replace_LongerReplacement_GrowsBuffer()
    {
        var sw = new SW("hi");
        sw.Replace("hi".AsSpan(), "hello world".AsSpan());
        Assert.Equal("hello world", sw.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_ReplacesMatch()
    {
        var sw = new SW("test123");
        sw.Replace(new PcreRegex(@"\d+"), "456".AsSpan());
        Assert.Equal("test456", sw.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_WithRange_ReplacesInRange()
    {
        var sw = new SW("123test456");
        sw.Replace(new PcreRegex(@"\d+"), "X".AsSpan(), 3, 7);
        Assert.Equal("123testX", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_PcreRegex_ReplacesAllMatches()
    {
        var sw = new SW("a1b2c3");
        sw.ReplaceAll(new PcreRegex(@"\d"), "X".AsSpan());
        Assert.Equal("aXbXcX", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_PcreRegex_WithRange_ReplacesInRange()
    {
        var sw = new SW("1a2b3c4");
        sw.ReplaceAll(new PcreRegex(@"\d"), "X".AsSpan(), 2, 5);
        Assert.Equal("1aXbXcX", sw.ToString());
    }

    [Fact]
    public void Replace_SystemRegex_Works()
    {
        var sw = new SW("test123");
        sw.Replace(new Regex(@"\d+"), "456".AsSpan());
        Assert.Equal("test456", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_SystemRegex_ReplacesAllMatches()
    {
        var sw = new SW("a1b2c3");
        sw.ReplaceAll(new Regex(@"\d"), "X".AsSpan());
        Assert.Equal("aXbXcX", sw.ToString());
    }
    [Theory]
    [InlineData("test TEST TEST test", "ABC", "test ABC ABC test")]
    [InlineData("test TEST TEST test", "ABCD", "test ABCD ABCD test")]
    [InlineData("test TEST TEST test", "ABCDE", "test ABCDE ABCDE test")]
    public void ReplaceAll_SystemRegex_Paths(string seed, string replacement, string expected)
    {
        var sw = new SW(seed);
        sw.ReplaceAll(new Regex("TEST"), replacement);
        Assert.Equal(expected, sw.Span);
    }
    #endregion

    #region Remove Tests
    [Fact]
    public void Remove_Range_RemovesCorrectly()
    {
        var sw = new SW("0123456789");
        sw.Remove(2, 5);
        Assert.Equal("01789", sw.ToString());
    }

    [Fact]
    public void Remove_CharInRange_RemovesOccurrences()
    {
        var sw = new SW("abcabc");
        sw.Remove('a', 0, 6);
        Assert.Equal("bcbc", sw.ToString());
    }

    [Fact]
    public void Remove_AtStart_AdjustsCorrectly()
    {
        var sw = new SW("hello");
        sw.Remove(0, 2);
        Assert.Equal("llo", sw.ToString());
    }

    [Fact]
    public void Remove_AtEnd_AdjustsCorrectly()
    {
        var sw = new SW("hello");
        sw.Remove(3, 2);
        Assert.Equal("hel", sw.ToString());
    }
    #endregion

    #region Trim Tests
    [Fact]
    public void Trim_Char_TrimsFromBothEnds()
    {
        var sw = new SW("xxxhelloxxx");
        sw.Trim('x');
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void TrimStart_Char_TrimsFromStart()
    {
        var sw = new SW("xxxhello");
        sw.TrimStart('x');
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void TrimEnd_Char_TrimsFromEnd()
    {
        var sw = new SW("helloxxx");
        sw.TrimEnd('x');
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void Trim_Span_TrimsAnyChar()
    {
        var sw = new SW("xyzhelloabc");
        sw.Trim("xyzabc".AsSpan());
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void TrimStart_Span_TrimsFromStart()
    {
        var sw = new SW("xyzhello");
        sw.TrimStart("xyz".AsSpan());
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void TrimEnd_Span_TrimsFromEnd()
    {
        var sw = new SW("helloabc");
        sw.TrimEnd("abc".AsSpan());
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void TrimSequence_TrimsEntireSequence()
    {
        var sw = new SW("abababtest");
        sw.TrimSequenceStart("ab".AsSpan());
        Assert.Equal("test", sw.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_TrimsEntireSequence()
    {
        var sw = new SW("testabab");
        sw.TrimSequenceEnd("ab".AsSpan());
        Assert.Equal("test", sw.ToString());
    }

    [Fact]
    public void TrimSequence_BothEnds_TrimsCorrectly()
    {
        var sw = new SW("ababtestabab");
        sw.TrimSequence("ab".AsSpan());
        Assert.Equal("test", sw.ToString());
    }

    [Fact]
    public void Trim_EmptyBuffer_NoOp()
    {
        var sw = new SW();
        sw.Trim('x');
        Assert.Equal("", sw.ToString());
    }
    #endregion

    #region Length Modification Tests
    [Fact]
    public void Truncate_ReducesLength()
    {
        var sw = new SW("hello world");
        sw.Truncate(5);
        Assert.Equal("hello", sw.ToString());
        Assert.Equal(5, sw.Length);
    }

    [Fact]
    public void Truncate_InvalidLength_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Truncate(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Truncate(10));
    }

    [Fact]
    public void Trim_Count_ReducesFromEnd()
    {
        var sw = new SW("hello");
        sw.Trim(2);
        Assert.Equal("hel", sw.ToString());
    }

    [Fact]
    public void Trim_ExceedsLength_Clears()
    {
        var sw = new SW("test");
        sw.Trim(100);
        Assert.Equal(0, sw.Length);
    }

    [Fact]
    public void Expand_IncreasesLength()
    {
        var sw = new SW("test");
        var span = sw.GetWritableSpan(5);
        "12345".AsSpan().CopyTo(span);
        sw.Expand(5);
        Assert.Equal("test12345", sw.ToString());
    }

    [Fact]
    public void Expand_BeyondCapacity_Throws()
    {
        var sw = new SW(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Expand(sw.Capacity + 1));
    }

    [Fact]
    public void EnsureCapacity_GrowsIfNeeded()
    {
        var sw = new SW(10);
        sw.EnsureCapacity(100);
        Assert.True(sw.Capacity >= 100);
    }

    [Fact]
    public void EnsureCapacity_ThrowsWhenTooLarge()
    {
        var sw = new SW(10);
        Assert.Throws<InvalidOperationException>(() => sw.EnsureCapacity(int.MaxValue));
    }
    #endregion

    #region Clear Tests
    [Fact]
    public void Clear_ResetsLength()
    {
        var sw = new SW("test");
        sw.Clear();
        Assert.Equal(0, sw.Length);
        Assert.Equal("", sw.ToString());
    }

    [Fact]
    public void Clear_WithWipe_ZeroesMemory()
    {
        var sw = new SW("test");
        sw.Clear(true);
        Assert.Equal(0, sw.Length);
    }
    #endregion

    #region Copy Tests
    [Fact]
    public void CopyTo_Span_CopiesCorrectly()
    {
        var sw = new SW("hello");
        var dest = new char[5];
        sw.CopyTo(dest.AsSpan());
        Assert.Equal("hello", new string(dest));
    }

    [Fact]
    public void CopyTo_Array_CopiesCorrectly()
    {
        var sw = new SW("hello");
        var dest = new char[10];
        sw.CopyTo(dest, 2);
        Assert.Equal("hello", new string(dest, 2, 5));
    }

    [Fact]
    public void CopyBlock_CopiesSubsection()
    {
        var sw = new SW("hello world");
        var dest = new char[5];
        sw.CopyBlock(6, 5, dest.AsSpan());
        Assert.Equal("world", new string(dest));
    }

    [Fact]
    public void CopyBlock_InvalidRange_Throws()
    {
        var sw = new SW("test");
        var dest = new char[10];
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.CopyBlock(-1, 2, dest.AsSpan()));
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.CopyBlock(0, 10, dest.AsSpan()));
    }

    [Fact]
    public void CopyTo_IBufferWriter_Writes()
    {
        var sw = new SW("test");
        var writer = new ArrayBufferWriter<char>();
        sw.CopyTo(writer);
        Assert.Equal("test", new string(writer.WrittenSpan));
    }

    [Fact]
    public void CopyBlock_IBufferWriter_WritesCorrectly()
    {
        var sw = new SW("hello world");
        var writer = new ArrayBufferWriter<char>();
        sw.CopyBlock(6, 5, writer);
        Assert.Equal("world", new string(writer.WrittenSpan));
    }
    #endregion

    #region IBufferWriter Tests
    [Fact]
    public void IBufferWriter_GetSpan_ReturnsWritableSpan()
    {
        IBufferWriter<char> sw = new SW();
        var span = sw.GetSpan(10);
        Assert.True(span.Length >= 10);
    }

    [Fact]
    public void IBufferWriter_Advance_IncreasesLength()
    {
        IBufferWriter<char> sw = new SW();
        var span = sw.GetSpan(5);
        "hello".AsSpan().CopyTo(span);
        sw.Advance(5);
        Assert.Equal(5, ((SW)sw).Length);
    }

    [Fact]
    public void IBufferWriter_GetMemory_ReturnsWritableMemory()
    {
        IBufferWriter<char> sw = new SW();
        var mem = sw.GetMemory(10);
        Assert.True(mem.Length >= 10);
    }
    #endregion

    #region Drain Tests
    [Fact]
    public void Drain_ReturnsStringAndClears()
    {
        var sw = new SW("test");
        var result = sw.Drain();
        Assert.Equal("test", result);
        Assert.Equal(0, sw.Length);
    }

    [Fact]
    public void Drain_WithWipe_ClearsAndWipes()
    {
        var sw = new SW("test");
        var result = sw.Drain(true);
        Assert.Equal("test", result);
        Assert.Equal(0, sw.Length);
    }
    #endregion

    #region Edge Cases
    [Fact]
    public void Replace_IdenticalSpans_NoOp()
    {
        var sw = new SW("test");
        sw.Replace("test".AsSpan(), "test".AsSpan());
        Assert.Equal("test", sw.ToString());
    }

    [Fact]
    public void Replace_OverlappingSpans_HandledCorrectly()
    {
        var sw = new SW("test");
        var span = sw.Span;
        sw.Replace(span, span);
        Assert.Equal("test", sw.ToString());
    }

    [Fact]
    public void LargeCapacity_HandlesCorrectly()
    {
        var sw = new SW(100000);
        Assert.True(sw.Capacity >= 100000);
        sw.Append('x');
        Assert.Equal(1, sw.Length);
    }

    [Fact]
    public void Version_IncrementsOnModification()
    {
        var sw = new SW("test");
        var v1 = sw.Version;
        sw.Append('x');
        Assert.True(sw.Version > v1);
    }

    [Fact]
    public void ValidateRange_InvalidRange_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.IndexOf('x', -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.IndexOf('x', 0, 10));
    }

    [Fact]
    public void GetWritableMemory_ReturnsCorrectMemory()
    {
        var sw = new SW("test");
        var mem = sw.GetWritableMemory(5);
        Assert.True(mem.Length >= 5);
    }

    [Fact]
    public void GetWritableMemory_BeyondFreeCapacity_Grows()
    {
        var sw = new SW(10);
        var initialCap = sw.Capacity;
        sw.GetWritableMemory(initialCap + 1);
        Assert.True(sw.Capacity > initialCap);
    }

    [Fact]
    public void Grow_CallsEnsureZeroAligned_WhenPossible()
    {
        var sw = new SW();
        var initialCapacity = sw.Capacity;
        sw.Append('a', (initialCapacity / 2) + 1);
        sw.Append('b');
        sw.Trim('a');

        Assert.NotEqual(0, sw.Start);
        Assert.Equal(initialCapacity, sw.FullMemory.Length);

        sw.EnsureCapacity(initialCapacity);

        Assert.Equal(0, sw.Start);
        // Ensure we didn't grow the buffer
        Assert.Equal(initialCapacity, sw.FullMemory.Length);
    }
    #endregion

    #region Regex Writer Tests
    [Fact]
    public void Replace_WithWriter_CallsCorrectly()
    {
        var sw = new SW("test123");
        sw.Replace(new PcreRegex(@"\d+"), 10, (buf, match) =>
        {
            "ABC".AsSpan().CopyTo(buf);
            buf[3] = '\0';
        });
        Assert.Equal("testABC", sw.ToString());
    }

    [Fact]
    public void ReplaceExact_WithWriter_UsesExactLength()
    {
        var sw = new SW("test123");
        sw.ReplaceExact(new PcreRegex(@"\d+"), 5, (buf, match) => "ABCDE".AsSpan().CopyTo(buf));
        Assert.Equal("testABCDE", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_WithWriter_ReplacesAllMatches()
    {
        var sw = new SW("a1b2c3");
        sw.ReplaceAll(new PcreRegex(@"\d"), 1, (buf, match) => buf[0] = 'X');
        Assert.Equal("aXbXcX", sw.ToString());
    }
    #endregion

    #region Memory and Span Properties Tests
    [Fact]
    public void FullMemory_ReturnsCorrectMemory()
    {
        var sw = new SW("test");
        var mem = sw.FullMemory;
        Assert.True(mem.Length >= sw.Capacity);
    }

    [Fact]
    public void UsableMemory_ReturnsCorrectMemory()
    {
        var sw = new SW("test");
        var mem = sw.UsableMemory;
        Assert.Equal(sw.Capacity, mem.Length);
    }

    [Fact]
    public void UsableSpan_ReturnsCorrectSpan()
    {
        var sw = new SW("test");
        var span = sw.UsableSpan;
        Assert.Equal(sw.Capacity, span.Length);
    }

    [Fact]
    public void Memory_ReturnsCorrectContent()
    {
        var sw = new SW("test");
        var mem = sw.Memory;
        Assert.Equal("test", new string(mem.Span));
    }

    [Fact]
    public void Span_ReturnsCorrectContent()
    {
        var sw = new SW("test");
        var span = sw.Span;
        Assert.Equal("test", new string(span));
    }
    #endregion

    #region Additional Coverage Tests for Uncovered Paths
    [Fact]
    public void Append_RefChar_InvalidLength_Throws()
    {
        var sw = new SW();
        var chars = "test".ToCharArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Append(in chars[0], -1));
    }

    [Fact]
    public void Append_RefChar_AppendsCorrectly()
    {
        var sw = new SW();
        var chars = "hello".ToCharArray();
        sw.Append(in chars[0], chars.Length);
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public unsafe void AppendLine_UnsafePointer_AppendsWithNewLine()
    {
        var sw = new SW();
        var str = "test";
        fixed (char* ptr = str)
        {
            sw.AppendLine(ptr, str.Length);
        }
        Assert.Equal("test" + Environment.NewLine, sw.ToString());
    }

    [Fact]
    public void AppendLine_RefChar_AppendsWithNewLine()
    {
        var sw = new SW();
        var chars = "test".ToCharArray();
        sw.AppendLine(in chars[0], chars.Length);
        Assert.Equal("test" + Environment.NewLine, sw.ToString());
    }

    [Fact]
    public void Replace_Char_WithStart_ReplacesFirstOccurrence()
    {
        var sw = new SW("hello world hello");
        sw.Replace('l', 'x', 6);
        Assert.Equal("hello worxd hello", sw.ToString());
    }

    [Fact]
    public void Replace_Char_NotFound_NoChange()
    {
        var sw = new SW("hello");
        sw.Replace('z', 'x', 0, 5);
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_Char_WithStart_ReplacesFromStart()
    {
        var sw = new SW("hello hello");
        sw.ReplaceAll('l', 'x', 6);
        Assert.Equal("hello hexxo", sw.ToString());
    }

    [Fact]
    public void Replace_Span_WithStart_ReplacesFirstOccurrence()
    {
        var sw = new SW("test test test");
        sw.Replace("test".AsSpan(), "pass".AsSpan(), 5);
        Assert.Equal("test pass test", sw.ToString());
    }

    [Fact]
    public void Replace_Span_NotFound_NoChange()
    {
        var sw = new SW("hello");
        sw.Replace("world".AsSpan(), "universe".AsSpan(), 0, 5);
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_Span_WithStart_ReplacesFromStart()
    {
        var sw = new SW("test test test");
        sw.ReplaceAll("test".AsSpan(), "pass".AsSpan(), 5);
        Assert.Equal("test pass pass", sw.ToString());
    }

    [Fact]
    public void Replace_Range_UsingSystemRange_ReplacesCorrectly()
    {
        var sw = new SW("hello world");
        sw.Replace(6..11, "earth".AsSpan());
        Assert.Equal("hello earth", sw.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_WithStart_ReplacesFromStart()
    {
        var sw = new SW("test123test456");
        sw.Replace(new PcreRegex(@"\d+"), "XXX".AsSpan(), 7);
        Assert.Equal("test123testXXX", sw.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_NullRegex_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentNullException>(() => sw.Replace((PcreRegex)null, "x".AsSpan()));
    }

    [Fact]
    public void ReplaceAll_PcreRegex_WithStart_ReplacesFromStart()
    {
        var sw = new SW("1test2test3");
        sw.ReplaceAll(new PcreRegex(@"\d"), "X".AsSpan(), 5);
        Assert.Equal("1testXtestX", sw.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_WithWriter_InvalidBufferSize_Throws()
    {
        var sw = new SW("test123");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sw.Replace(new PcreRegex(@"\d+"), -1, (buf, match) => { }));
    }

    [Fact]
    public void Replace_PcreRegex_WithWriter_NoMatch_NoChange()
    {
        var sw = new SW("test");
        sw.Replace(new PcreRegex(@"\d+"), 10, (buf, match) => "XXX".AsSpan().CopyTo(buf));
        Assert.Equal("test", sw.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_WithWriter_WithStart_ReplacesFromStart()
    {
        var sw = new SW("test123test456");
        sw.Replace(new PcreRegex(@"\d+"), 10, (buf, match) =>
        {
            "XXX".AsSpan().CopyTo(buf);
            buf[3] = '\0';
        }, 7);
        Assert.Equal("test123testXXX", sw.ToString());
    }

    [Theory]
    [InlineData("test TEST test", "ABC", "test ABC test")]
    [InlineData("test TEST test", "ABCD", "test ABCD test")]
    [InlineData("test TEST test", "ABCDE", "test ABCDE test")]
    [InlineData("test TEST TEST test", "ABC", "test ABC ABC test")]
    [InlineData("test TEST TEST test", "ABCD", "test ABCD ABCD test")]
    [InlineData("test TEST TEST test", "ABCDE", "test ABCDE ABCDE test")]
    public void ReplaceAll_PcreRegex_WithWriter_WithStart_ReplacesFromStart(string seed, string replacement, string expected)
    {
        var sw = new SW(seed);
        sw.ReplaceAll(new PcreRegex("TEST"), 5, (buf, match) =>
        {
            buf.Clear();
            replacement.CopyTo(buf);
        }, 5);
        Assert.Equal(expected, sw.Span);
    }

    [Fact]
    public void ReplaceExact_PcreRegex_InvalidLength_Throws()
    {
        var sw = new SW("test123");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sw.ReplaceExact(new PcreRegex(@"\d+"), -1, (buf, match) => { }));
    }

    [Fact]
    public void ReplaceExact_PcreRegex_WithStart_ReplacesFromStart()
    {
        var sw = new SW("test123test456");
        sw.ReplaceExact(new PcreRegex(@"\d+"), 5, (buf, match) => "XXXXX".AsSpan().CopyTo(buf), 7);
        Assert.Equal("test123testXXXXX", sw.ToString());
    }

    [Fact]
    public void ReplaceAllExact_PcreRegex_WithWriter_ReplacesAll()
    {
        var sw = new SW("a1b2c3");
        sw.ReplaceAllExact(new PcreRegex(@"\d"), 3, (buf, match) => "XXX".AsSpan().CopyTo(buf));
        Assert.Equal("aXXXbXXXcXXX", sw.ToString());
    }

    [Fact]
    public void ReplaceAllExact_PcreRegex_WithStart_ReplacesFromStart()
    {
        var sw = new SW("1test2test3");
        sw.ReplaceAllExact(new PcreRegex(@"\d"), 1, (buf, match) => buf[0] = 'X', 5);
        Assert.Equal("1testXtestX", sw.ToString());
    }

    [Fact]
    public void Replace_SystemRegex_WithStart_ReplacesFromStart()
    {
        var sw = new SW("test123test456");
        sw.Replace(new Regex(@"\d+"), "XXX".AsSpan(), 7);
        Assert.Equal("test123testXXX", sw.ToString());
    }

    [Fact]
    public void Replace_SystemRegex_NullRegex_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentNullException>(() => sw.Replace((Regex)null, "x".AsSpan()));
    }

    [Theory]
    [InlineData("test TEST test", "ABC", "test ABC test")]
    [InlineData("test TEST test", "ABCD", "test ABCD test")]
    [InlineData("test TEST test", "ABCDE", "test ABCDE test")]
    [InlineData("test TEST TEST test", "ABC", "test ABC ABC test")]
    [InlineData("test TEST TEST test", "ABCD", "test ABCD ABCD test")]
    [InlineData("test TEST TEST test", "ABCDE", "test ABCDE ABCDE test")]
    public void ReplaceAll_SystemRegex_WithStart_ReplacesFromStart(string seed, string replacement, string expected)
    {
        var sw = new SW(seed);
        sw.ReplaceAll(new Regex("TEST"), replacement, 5);
        Assert.Equal(expected, sw.Span);
    }

    [Fact]
    public void Replace_SystemRegex_WithWriter_CallsCorrectly()
    {
        var sw = new SW("test123");
        sw.Replace(new Regex(@"\d+"), 10, (buf, match) =>
        {
            "ABC".AsSpan().CopyTo(buf);
            buf[3] = '\0';
        });
        Assert.Equal("testABC", sw.ToString());
    }

    [Fact]
    public void Replace_SystemRegex_WithWriter_WithStart_ReplacesFromStart()
    {
        var sw = new SW("test123test456");
        sw.Replace(new Regex(@"\d+"), 10, (buf, match) =>
        {
            "XXX".AsSpan().CopyTo(buf);
            buf[3] = '\0';
        }, 7);
        Assert.Equal("test123testXXX", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_SystemRegex_WithWriter_ReplacesAll()
    {
        var sw = new SW("a1b2c3");
        sw.ReplaceAll(new Regex(@"\d"), 1, (buf, match) => buf[0] = 'X');
        Assert.Equal("aXbXcX", sw.ToString());
    }

    [Theory]
    [InlineData("test TEST test", "ABC", "test ABC test")]
    [InlineData("test TEST test", "ABCD", "test ABCD test")]
    [InlineData("test TEST test", "ABCDE", "test ABCDE test")]
    [InlineData("test TEST TEST test", "ABC", "test ABC ABC test")]
    [InlineData("test TEST TEST test", "ABCD", "test ABCD ABCD test")]
    [InlineData("test TEST TEST test", "ABCDE", "test ABCDE ABCDE test")]
    public void ReplaceAll_SystemRegex_WithWriter_WithStart_ReplacesFromStart(string seed, string replacement, string expected)
    {
        var sw = new SW(seed);
        sw.ReplaceAll(new Regex("TEST"), 5, (buf, match) =>
        {
            buf.Clear();
            replacement.CopyTo(buf);
        }, 5);
        Assert.Equal(expected, sw.Span);
    }

    [Fact]
    public void ReplaceExact_SystemRegex_UsesExactLength()
    {
        var sw = new SW("test123");
        sw.ReplaceExact(new Regex(@"\d+"), 5, (buf, match) => "ABCDE".AsSpan().CopyTo(buf));
        Assert.Equal("testABCDE", sw.ToString());
    }

    [Fact]
    public void ReplaceExact_SystemRegex_WithStart_ReplacesFromStart()
    {
        var sw = new SW("test123test456");
        sw.ReplaceExact(new Regex(@"\d+"), 5, (buf, match) => "XXXXX".AsSpan().CopyTo(buf), 7);
        Assert.Equal("test123testXXXXX", sw.ToString());
    }

    [Fact]
    public void ReplaceAllExact_SystemRegex_ReplacesAll()
    {
        var sw = new SW("a1b2c3");
        sw.ReplaceAllExact(new Regex(@"\d"), 3, (buf, match) => "XXX".AsSpan().CopyTo(buf));
        Assert.Equal("aXXXbXXXcXXX", sw.ToString());
    }

    [Fact]
    public void ReplaceAllExact_SystemRegex_WithStart_ReplacesFromStart()
    {
        var sw = new SW("1test2test3");
        sw.ReplaceAllExact(new Regex(@"\d"), 1, (buf, match) => buf[0] = 'X', 5);
        Assert.Equal("1testXtestX", sw.ToString());
    }

    [Fact]
    public void IndexOf_SystemRegex_FindsMatch()
    {
        var sw = new SW("test123");
        var index = sw.IndexOf(new Regex(@"\d+"));
        Assert.Equal(4, index);
    }

    [Fact]
    public void IndexOf_SystemRegex_NullRegex_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentNullException>(() => sw.IndexOf((Regex)null));
    }

    [Fact]
    public void IndexOf_SystemRegex_WithStart_FindsFromStart()
    {
        var sw = new SW("test123test456");
        var index = sw.IndexOf(new Regex(@"\d+"), 7);
        Assert.Equal(11, index);
    }

    [Fact]
    public void IndexOf_SystemRegex_WithRange_FindsInRange()
    {
        var sw = new SW("test123test456");
        var index = sw.IndexOf(new Regex(@"\d+"), 0, 10);
        Assert.Equal(4, index);
    }

    [Fact]
    public void IndexOf_PcreRegex_FindsMatch()
    {
        var sw = new SW("test123");
        var index = sw.IndexOf(new PcreRegex(@"\d+"));
        Assert.Equal(4, index);
    }

    [Fact]
    public void IndexOf_PcreRegex_NullRegex_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentNullException>(() => sw.IndexOf((PcreRegex)null));
    }

    [Fact]
    public void IndexOf_PcreRegex_WithStart_FindsFromStart()
    {
        var sw = new SW("test123test456");
        var index = sw.IndexOf(new PcreRegex(@"\d+"), 7);
        Assert.Equal(11, index);
    }

    [Fact]
    public void IndexOf_PcreRegex_WithRange_FindsInRange()
    {
        var sw = new SW("test123test456");
        var index = sw.IndexOf(new PcreRegex(@"\d+"), 0, 10);
        Assert.Equal(4, index);
    }

    [Fact]
    public void IndexOf_PcreRegex_NotFound_ReturnsMinusOne()
    {
        var sw = new SW("test");
        var index = sw.IndexOf(new PcreRegex(@"\d+"));
        Assert.Equal(-1, index);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_Char_WithStart_EnumeratesFromStart()
    {
        var sw = new SW("ababab");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOfUnsafe('a', 2))
        {
            indices.Add(idx);
        }

        Assert.Equal([2, 4], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_Char_WithRange_EnumeratesInRange()
    {
        var sw = new SW("ababab");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOfUnsafe('a', 0, 4))
        {
            indices.Add(idx);
        }

        Assert.Equal([0, 2], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_Char_WithStart_EnumeratesFromStart()
    {
        var sw = new SW("ababab");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf('a', 2))
        {
            indices.Add(idx);
        }

        Assert.Equal([2, 4], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_Char_WithRange_EnumeratesInRange()
    {
        var sw = new SW("ababab");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf('a', 0, 4))
        {
            indices.Add(idx);
        }

        Assert.Equal([0, 2], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_Span_WithStart_EnumeratesFromStart()
    {
        var sw = new SW("test test test");
        var indices = new List<int>();
        foreach (var index in sw.EnumerateIndicesOfUnsafe("test".AsSpan(), 5))
        {
            indices.Add(index);
        }
        Assert.Equal([5, 10], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_Span_WithStart_EnumeratesFromStart()
    {
        var sw = new SW("test test test");
        var indices = new List<int>();
        foreach (var index in sw.EnumerateIndicesOf("test".AsSpan(), 5))
        {
            indices.Add(index);
        }
        Assert.Equal([5, 10], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_Span_WithRange_EnumeratesInRange()
    {
        var sw = new SW("test test test");
        var indices = new List<int>();
        foreach (var index in sw.EnumerateIndicesOf("test".AsSpan(), 0, 9))
        {
            indices.Add(index);
        }
        Assert.Equal([0, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_PcreRegex_EnumeratesMatches()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOfUnsafe(new PcreRegex(@"\d")))
        {
            indices.Add(idx);
        }

        Assert.Equal([1, 3, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_PcreRegex_WithStart_EnumeratesFromStart()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOfUnsafe(new PcreRegex(@"\d"), 2))
        {
            indices.Add(idx);
        }

        Assert.Equal([3, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_PcreRegex_WithRange_EnumeratesInRange()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOfUnsafe(new PcreRegex(@"\d"), 0, 4))
        {
            indices.Add(idx);
        }

        Assert.Equal([1, 3], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_PcreRegex_EnumeratesMatches()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf(new PcreRegex(@"\d")))
        {
            indices.Add(idx);
        }

        Assert.Equal([1, 3, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_PcreRegex_WithStart_EnumeratesFromStart()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf(new PcreRegex(@"\d"), 2))
        {
            indices.Add(idx);
        }

        Assert.Equal([3, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_PcreRegex_WithRange_EnumeratesInRange()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf(new PcreRegex(@"\d"), 0, 4))
        {
            indices.Add(idx);
        }

        Assert.Equal([1, 3], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_SystemRegex_EnumeratesMatches()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOfUnsafe(new Regex(@"\d")))
        {
            indices.Add(idx);
        }

        Assert.Equal([1, 3, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_SystemRegex_WithStart_EnumeratesFromStart()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOfUnsafe(new Regex(@"\d"), 2))
        {
            indices.Add(idx);
        }

        Assert.Equal([3, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_SystemRegex_WithRange_EnumeratesInRange()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOfUnsafe(new Regex(@"\d"), 0, 4))
        {
            indices.Add(idx);
        }

        Assert.Equal([1, 3], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_SystemRegex_EnumeratesMatches()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf(new Regex(@"\d")))
        {
            indices.Add(idx);
        }

        Assert.Equal([1, 3, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_SystemRegex_WithStart_EnumeratesFromStart()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf(new Regex(@"\d"), 2))
        {
            indices.Add(idx);
        }

        Assert.Equal([3, 5], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_SystemRegex_WithRange_EnumeratesInRange()
    {
        var sw = new SW("a1b2c3");
        var indices = new List<int>();
        foreach (var idx in sw.EnumerateIndicesOf(new Regex(@"\d"), 0, 4))
        {
            indices.Add(idx);
        }

        Assert.Equal([1, 3], indices);
    }

    [Fact]
    public void IndexOfAny_SearchValues_FindsFirstMatch()
    {
        var sw = new SW("hello world");
        var searchValues = SearchValues.Create("aeiou".AsSpan());
        Assert.Equal(1, sw.IndexOfAny(searchValues));
    }

    [Fact]
    public void IndexOfAny_SearchValues_WithStart_FindsFromStart()
    {
        var sw = new SW("hello world");
        var searchValues = SearchValues.Create("aeiou".AsSpan());
        Assert.Equal(4, sw.IndexOfAny(searchValues, 2));
    }

    [Fact]
    public void IndexOfAny_SearchValues_WithRange_FindsInRange()
    {
        var sw = new SW("hello world");
        var searchValues = SearchValues.Create("aeiou".AsSpan());
        Assert.Equal(1, sw.IndexOfAny(searchValues, 0, 3));
    }

    [Fact]
    public void IndexOfAnyExcept_SearchValues_FindsFirstNonMatch()
    {
        var sw = new SW("aaaabbbb");
        var searchValues = SearchValues.Create("a".AsSpan());
        Assert.Equal(4, sw.IndexOfAnyExcept(searchValues));
    }

    [Fact]
    public void IndexOfAnyExcept_SearchValues_WithStart_FindsFromStart()
    {
        var sw = new SW("aaaabbbb");
        var searchValues = SearchValues.Create("ab".AsSpan());
        Assert.Equal(-1, sw.IndexOfAnyExcept(searchValues, 0));
    }

    [Fact]
    public void IndexOfAnyExcept_SearchValues_WithRange_FindsInRange()
    {
        var sw = new SW("aaaabbbb");
        var searchValues = SearchValues.Create("a".AsSpan());
        Assert.Equal(4, sw.IndexOfAnyExcept(searchValues, 0, 8));
    }

    [Fact]
    public void IndexOfAny_SearchValues_NotFound_ReturnsMinusOne()
    {
        var sw = new SW("hello");
        var searchValues = SearchValues.Create("xyz".AsSpan());
        Assert.Equal(-1, sw.IndexOfAny(searchValues));
    }

    [Fact]
    public void IndexOfAnyInRange_WithStart_FindsFromStart()
    {
        var sw = new SW("abc123abc");
        Assert.Equal(6, sw.IndexOfAnyInRange('a', 'z', 5));
    }

    [Fact]
    public void IndexOfAnyExceptInRange_WithStart_FindsFromStart()
    {
        var sw = new SW("123abc123");
        Assert.Equal(6, sw.IndexOfAnyExceptInRange('a', 'z', 5));
    }

    [Fact]
    public void IndexOfAny_WithStart_InvalidRange_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.IndexOfAny("x".AsSpan(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.IndexOfAny("x".AsSpan(), 10));
    }

    [Fact]
    public void IndexOfAnyExcept_WithStart_InvalidRange_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.IndexOfAnyExcept("x".AsSpan(), -1));
    }

    [Fact]
    public void IndexOfAnyInRange_WithStart_InvalidRange_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.IndexOfAnyInRange('a', 'z', -1));
    }

    [Fact]
    public void IndexOfAnyExceptInRange_WithStart_InvalidRange_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.IndexOfAnyExceptInRange('a', 'z', -1));
    }

    [Fact]
    public void Remove_UsingRange_RemovesCorrectly()
    {
        var sw = new SW("hello world");
        sw.Remove(6..11);
        Assert.Equal("hello ", sw.ToString());
    }

    [Fact]
    public void Remove_CharValue_WithRange_RemovesOccurrences()
    {
        var sw = new SW("abcabc");
        sw.Remove('a', 0..6);
        Assert.Equal("bcbc", sw.ToString());
    }

    [Fact]
    public void CopyTo_TextWriter_CopiesCorrectly()
    {
        var sw = new SW("hello world");
        using var writer = new StringWriter();
        sw.CopyTo(writer);
        Assert.Equal("hello world", writer.ToString());
    }

    [Fact]
    public void CopyTo_TextWriter_Null_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentNullException>(() => sw.CopyTo((TextWriter)null));
    }

    [Fact]
    public void CopyBlock_TextWriter_CopiesCorrectly()
    {
        var sw = new SW("hello world");
        using var writer = new StringWriter();
        sw.CopyBlock(6, 5, writer);
        Assert.Equal("world", writer.ToString());
    }

    [Fact]
    public void CopyBlock_TextWriter_Null_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentNullException>(() => sw.CopyBlock(0, 4, (TextWriter)null));
    }

    [Fact]
    public void CopyTo_IBufferWriter_Null_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentNullException>(() => sw.CopyTo((IBufferWriter<char>)null));
    }

    [Fact]
    public void CopyBlock_IBufferWriter_Null_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentNullException>(() => sw.CopyBlock(0, 4, (IBufferWriter<char>)null));
    }

    [Fact]
    public unsafe void CopyTo_RefChar_CopiesCorrectly()
    {
        var sw = new SW("hello");
        var dest = new char[5];
        sw.CopyTo(ref dest[0]);
        Assert.Equal("hello", new string(dest));
    }

    [Fact]
    public unsafe void CopyBlock_RefChar_CopiesCorrectly()
    {
        var sw = new SW("hello world");
        var dest = new char[5];
        sw.CopyBlock(6, 5, ref dest[0]);
        Assert.Equal("world", new string(dest));
    }

    [Fact]
    public unsafe void CopyBlock_UnsafePointer_CopiesCorrectly()
    {
        var sw = new SW("hello world");
        var dest = new char[5];
        fixed (char* ptr = dest)
        {
            sw.CopyBlock(6, 5, ptr);
        }
        Assert.Equal("world", new string(dest));
    }

    [Fact]
    public void CopyBlock_Array_CopiesCorrectly()
    {
        var sw = new SW("hello world");
        var dest = new char[10];
        sw.CopyBlock(6, 5, dest, 2);
        Assert.Equal("world", new string(dest, 2, 5));
    }

    [Fact]
    public void CopyBlock_Array_InvalidIndex_Throws()
    {
        var sw = new SW("hello");
        var dest = new char[10];
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.CopyBlock(0, 5, dest, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.CopyBlock(0, 5, dest, 11));
    }

    [Fact]
    public void CopyBlock_Array_InsufficientSpace_Throws()
    {
        var sw = new SW("hello");
        var dest = new char[3];
        Assert.Throws<ArgumentException>(() => sw.CopyBlock(0, 5, dest, 0));
    }

    [Fact]
    public void CopyBlock_Memory_CopiesCorrectly()
    {
        var sw = new SW("hello world");
        var dest = new Memory<char>(new char[5]);
        sw.CopyBlock(6, 5, dest);
        Assert.Equal("world", new string(dest.Span));
    }

    [Fact]
    public void GetTextWriter_WritesToStringWeaver()
    {
        var sw = new SW();
        var writer = sw.GetTextWriter();
        writer.Write("hello");
        writer.Write(" ");
        writer.Write("world");
        Assert.Equal("hello world", sw.ToString());
    }

    [Fact]
    public void GetStream_WritesToStringWeaver()
    {
        var sw = new SW();
        using var stream = sw.GetStream(Encoding.UTF8);
        var bytes = Encoding.UTF8.GetBytes("hello world");
        stream.Write(bytes, 0, bytes.Length);
        Assert.Equal("hello world", sw.ToString());
    }

    [Fact]
    public void GetStream_DefaultEncoding_WritesToStringWeaver()
    {
        var sw = new SW();
        using var stream = sw.GetStream();
        var bytes = Encoding.Default.GetBytes("test");
        stream.Write(bytes, 0, bytes.Length);
        Assert.Equal("test", sw.ToString());
    }

    [Fact]
    public void GetStream_CannotRead_ThrowsNotSupported()
    {
        var sw = new SW();
        using var stream = sw.GetStream();
        var buffer = new byte[10];
        Assert.Throws<NotSupportedException>(() => stream.Read(buffer, 0, 10));
    }

    [Fact]
    public void GetStream_CannotSeek_ThrowsNotSupported()
    {
        var sw = new SW();
        using var stream = sw.GetStream();
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void GetStream_CannotSetLength_ThrowsNotSupported()
    {
        var sw = new SW();
        using var stream = sw.GetStream();
        Assert.Throws<NotSupportedException>(() => stream.SetLength(10));
    }

    [Fact]
    public void GetStream_Write_NullBuffer_Throws()
    {
        var sw = new SW();
        using var stream = sw.GetStream();
        Assert.Throws<ArgumentNullException>(() => stream.Write(null, 0, 0));
    }

    [Fact]
    public void GetStream_Write_InvalidOffset_Throws()
    {
        var sw = new SW();
        using var stream = sw.GetStream();
        var buffer = new byte[10];
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, -1, 5));
    }

    [Fact]
    public void GetStream_Write_InvalidCount_Throws()
    {
        var sw = new SW();
        using var stream = sw.GetStream();
        var buffer = new byte[10];
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, 0, 11));
    }

    [Fact]
    public void Expand_Negative_Throws()
    {
        var sw = new SW();
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Expand(-1));
    }

    [Fact]
    public void EnsureCapacity_Negative_Throws()
    {
        var sw = new SW();
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.EnsureCapacity(-1));
    }

    [Fact]
    public void ValidateRange_InvalidStart_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Replace('x', 'y', -1, 1));
    }

    [Fact]
    public void ValidateRange_InvalidLength_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Replace('x', 'y', 0, 100));
    }

    [Fact]
    public void RangeIndexer_Get_OutOfRange_Throws()
    {
        var sw = new SW("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = sw[10..20]; });
    }

    [Fact]
    public void GetWritableSpan_NegativeSize_ReturnsSpan()
    {
        var sw = new SW();
        var span = sw.GetWritableSpan(-10);
        Assert.True(span.Length > 0);
    }

    [Fact]
    public void GetWritableMemory_NegativeSize_ReturnsMemory()
    {
        var sw = new SW();
        var mem = sw.GetWritableMemory(-10);
        Assert.True(mem.Length > 0);
    }
    #endregion
}