using SW = StringWeaver.StringWeaver;

namespace StringWeaver.Tests;

/// <summary>
/// Tests that verify correct behavior when <c>Start > 0</c>, which occurs after
/// <c>TrimStart</c>, <c>TrimSequenceStart</c>, or <c>ReplaceCore</c> operations
/// that bump the Start pointer forward.
/// </summary>
public class StartPointerTests
{
    #region Helpers
    /// <summary>
    /// Creates a <see cref="SW"/> with <c>Start > 0</c> by appending content and trimming leading chars.
    /// After calling this, <c>sw.Start > 0</c> and <c>sw.ToString() == remaining</c>.
    /// </summary>
    private static SW CreateWithStartOffset(string prefix, string remaining)
    {
        var sw = new SW(prefix + remaining);
        sw.TrimStart(prefix.AsSpan());
        Assert.True(sw.Start > 0, "Start should be > 0 after TrimStart");
        Assert.Equal(remaining, sw.ToString());
        return sw;
    }
    #endregion

    #region Append Tests (Bug #1: Append(char) used UsableSpan[End++] instead of FullMemory.Span[End++])
    [Fact]
    public void Append_Char_AfterTrimStart_AppendsCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        sw.Append('!');
        Assert.Equal("Hello!", sw.ToString());
    }

    [Fact]
    public void Append_MultipleChars_AfterTrimStart_AppendsCorrectly()
    {
        var sw = CreateWithStartOffset("XXX", "AB");
        sw.Append('C');
        sw.Append('D');
        sw.Append('E');
        Assert.Equal("ABCDE", sw.ToString());
    }

    [Fact]
    public void Append_String_AfterTrimStart_AppendsCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        sw.Append(" World");
        Assert.Equal("Hello World", sw.ToString());
    }
    #endregion

    #region Indexer Tests (Bug #6: Index used End instead of Length)
    [Fact]
    public void Indexer_Get_AfterTrimStart_ReturnsCorrectChar()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        Assert.Equal('H', sw[0]);
        Assert.Equal('o', sw[4]);
        Assert.Equal('o', sw[^1]);
    }

    [Fact]
    public void Indexer_Set_AfterTrimStart_SetsCorrectChar()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        sw[0] = 'J';
        Assert.Equal("Jello", sw.ToString());
    }

    [Fact]
    public void Indexer_FromEnd_AfterTrimStart_ReturnsCorrectChar()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        Assert.Equal('l', sw[^2]);
        Assert.Equal('H', sw[^5]);
    }

    [Fact]
    public void Indexer_OutOfRange_AfterTrimStart_Throws()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        Assert.Throws<ArgumentOutOfRangeException>(() => sw[5]);
        Assert.Throws<ArgumentOutOfRangeException>(() => sw[^6]);
    }
    #endregion

    #region Range Indexer Tests (Bug #7: Range used End instead of Length)
    [Fact]
    public void RangeIndexer_AfterTrimStart_ReturnsCorrectSpan()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        Assert.Equal("Hel", new string(sw[..3]));
        Assert.Equal("llo", new string(sw[2..]));
        Assert.Equal("ell", new string(sw[1..4]));
    }
    #endregion

    #region TrimStart Tests (Bug #3/#4: TrimStart used Start = start instead of Start += start)
    [Fact]
    public void TrimStart_Char_AfterPreviousTrimStart_TrimsCorrectly()
    {
        var sw = new SW("XXYYHello");
        sw.TrimStart('X');
        Assert.Equal("YYHello", sw.ToString());
        sw.TrimStart('Y');
        Assert.Equal("Hello", sw.ToString());
    }

    [Fact]
    public void TrimStart_Span_AfterPreviousTrimStart_TrimsCorrectly()
    {
        var sw = new SW("XXYYHello");
        sw.TrimStart("XY".AsSpan());
        Assert.Equal("Hello", sw.ToString());
    }

    [Fact]
    public void TrimStart_Span_AfterCharTrimStart_AdvancesStartCorrectly()
    {
        // Regression: TrimStart(ReadOnlySpan<char>) used Start = start instead of Start += start,
        // which reset the pointer when Start was already > 0 from a prior trim.
        var sw = new SW("AABBHello");
        sw.TrimStart('A'); // Start moves to 2
        Assert.Equal("BBHello", sw.ToString());
        Assert.True(sw.Start > 0);
        var startBefore = sw.Start;

        sw.TrimStart("BB".AsSpan()); // Should advance Start by 2 more, not reset it
        Assert.Equal("Hello", sw.ToString());
        Assert.Equal(startBefore + 2, sw.Start);
    }

    [Fact]
    public void TrimStart_Span_MultipleCalls_AccumulatesStartOffset()
    {
        // Chain three TrimStart(ReadOnlySpan<char>) calls — Start must accumulate, not reset
        var sw = new SW("abcdHello");
        sw.TrimStart("ab".AsSpan());
        Assert.Equal("cdHello", sw.ToString());
        sw.TrimStart("cd".AsSpan());
        Assert.Equal("Hello", sw.ToString());
        Assert.Equal(4, sw.Start);
    }

    [Fact]
    public void TrimStart_OnEmptyAfterTrimStart_DoesNotThrow()
    {
        var sw = new SW("XXX");
        sw.TrimStart('X');
        Assert.Equal(0, sw.Length);
        sw.TrimStart('X'); // Should be no-op, not throw
        Assert.Equal(0, sw.Length);
    }
    #endregion

    #region TrimEnd Tests (Bug #22: TrimEnd empty check used End == 0 instead of Length == 0)
    [Fact]
    public void TrimEnd_Char_AfterTrimStart_TrimsCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello  ");
        sw.TrimEnd(' ');
        Assert.Equal("Hello", sw.ToString());
    }

    [Fact]
    public void TrimEnd_OnEmptyAfterTrimStart_DoesNotThrow()
    {
        var sw = new SW("XXYYY");
        sw.TrimStart('X');
        sw.TrimEnd('Y');
        Assert.Equal(0, sw.Length);
        sw.TrimEnd('Z'); // Should be no-op
        Assert.Equal(0, sw.Length);
    }
    #endregion

    #region TrimSequenceStart Tests (Bug #5: TrimSequenceStart used Start = start instead of Start += start)
    [Fact]
    public void TrimSequenceStart_AfterTrimStart_TrimsCorrectly()
    {
        var sw = new SW("XXababHello");
        sw.TrimStart('X');
        Assert.Equal("ababHello", sw.ToString());
        sw.TrimSequenceStart("ab");
        Assert.Equal("Hello", sw.ToString());
    }
    #endregion

    #region TrimSequenceEnd Tests (Bug #21: early-exit used End instead of Length)
    [Fact]
    public void TrimSequenceEnd_AfterTrimStart_TrimsCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Helloabab");
        sw.TrimSequenceEnd("ab");
        Assert.Equal("Hello", sw.ToString());
    }

    [Fact]
    public void TrimSequenceStart_EarlyExit_AfterTrimStart_WhenContentShorterThanValue()
    {
        var sw = CreateWithStartOffset("XXXXXXXX", "Hi");
        // Length is 2, value length is 5 — should early-exit, not use End (which is 10)
        sw.TrimSequenceStart("XXXXX");
        Assert.Equal("Hi", sw.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_EarlyExit_AfterTrimStart_WhenContentShorterThanValue()
    {
        var sw = CreateWithStartOffset("XXXXXXXX", "Hi");
        sw.TrimSequenceEnd("XXXXX");
        Assert.Equal("Hi", sw.ToString());
    }
    #endregion

    #region Truncate Tests (Bug #10: Truncate used End instead of Length, set End = length instead of Start + length)
    [Fact]
    public void Truncate_AfterTrimStart_TruncatesCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        sw.Truncate(3);
        Assert.Equal("Hel", sw.ToString());
    }

    [Fact]
    public void Truncate_AfterTrimStart_ThrowsWhenExceedsLength()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        // Length is 5, so 6 should throw
        Assert.Throws<ArgumentOutOfRangeException>(() => sw.Truncate(6));
    }

    [Fact]
    public void Truncate_ToZero_AfterTrimStart_ClearsContent()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        sw.Truncate(0);
        Assert.Equal(0, sw.Length);
        Assert.Equal("", sw.ToString());
    }
    #endregion

    #region Trim(int count) Tests (Bug #11: used End instead of Length)
    [Fact]
    public void TrimCount_AfterTrimStart_TrimsCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        sw.Trim(2);
        Assert.Equal("Hel", sw.ToString());
    }

    [Fact]
    public void TrimCount_ExceedsLength_AfterTrimStart_ClearsContent()
    {
        var sw = CreateWithStartOffset("XX", "Hi");
        sw.Trim(10); // exceeds Length of 2, should clear
        Assert.Equal(0, sw.Length);
    }
    #endregion

    #region Replace(Range) Tests (Bug #8: used End instead of Length)
    [Fact]
    public void Replace_Range_AfterTrimStart_ReplacesCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello World");
        sw.Replace(..5, "Hi".AsSpan());
        Assert.Equal("Hi World", sw.ToString());
    }

    [Fact]
    public void Replace_Range_FromEnd_AfterTrimStart_ReplacesCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello World");
        sw.Replace(6.., "Earth".AsSpan());
        Assert.Equal("Hello Earth", sw.ToString());
    }
    #endregion

    #region Remove(Range) Tests (Bug #9: used End instead of Length)
    [Fact]
    public void Remove_Range_AfterTrimStart_RemovesCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello World");
        sw.Remove(5..);
        Assert.Equal("Hello", sw.ToString());
    }

    [Fact]
    public void Remove_CharRange_AfterTrimStart_RemovesCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello World");
        sw.Remove('l', ..);
        Assert.Equal("Heo Word", sw.ToString());
    }
    #endregion

    #region ReplaceCore Tests (Bugs #12/#13: multi-index replace used End instead of Length)
    [Fact]
    public void ReplaceAll_AfterTrimStart_ReplacesAllOccurrences()
    {
        var sw = CreateWithStartOffset("XX", "aabbaabb");
        sw.ReplaceAll("aa", "X");
        Assert.Equal("XbbXbb", sw.ToString());
    }

    [Fact]
    public void ReplaceAll_WithLongerReplacement_AfterTrimStart()
    {
        var sw = CreateWithStartOffset("XX", "abab");
        sw.ReplaceAll("ab", "XYZ");
        Assert.Equal("XYZXYZ", sw.ToString());
    }

    [Fact]
    public void ReplaceCore_SingleIndex_AtEnd_AfterTrimStart()
    {
        var sw = CreateWithStartOffset("XX", "HelloXX");
        sw.Replace(5, 2, "!!".AsSpan());
        Assert.Equal("Hello!!", sw.ToString());
    }
    #endregion

    #region GetWritableMemory/GetWritableSpan Tests (Bug #2: used UsableMemory[End..] instead of UsableMemory[(End-Start)..])
    [Fact]
    public void GetWritableSpan_AfterTrimStart_ReturnsWritableRegion()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        var writable = sw.GetWritableSpan(5);
        Assert.True(writable.Length >= 5);
        "World".AsSpan().CopyTo(writable);
        sw.Expand(5);
        Assert.Equal("HelloWorld", sw.ToString());
    }

    [Fact]
    public void GetWritableMemory_AfterTrimStart_ReturnsWritableRegion()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        var writable = sw.GetWritableMemory(5);
        Assert.True(writable.Length >= 5);
        "World".AsSpan().CopyTo(writable.Span);
        sw.Expand(5);
        Assert.Equal("HelloWorld", sw.ToString());
    }
    #endregion

    #region CopyBlock Pointer Overloads Tests (Bug #17: used End instead of length for Span size)
    [Fact]
    public unsafe void CopyBlock_Pointer_AfterTrimStart_CopiesCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        var dest = new char[5];
        fixed (char* ptr = dest)
        {
            sw.CopyBlock(0, 5, ptr);
        }
        Assert.Equal("Hello", new string(dest));
    }

    [Fact]
    public unsafe void CopyBlock_ManagedRef_AfterTrimStart_CopiesCorrectly()
    {
        var sw = CreateWithStartOffset("XX", "Hello");
        var dest = new char[5];
        sw.CopyBlock(0, 5, ref dest[0]);
        Assert.Equal("Hello", new string(dest));
    }
    #endregion

    #region EnumerateIndicesOf with Range Tests (Bugs #14/#15: off-by-one)
    [Fact]
    public void EnumerateIndicesOf_Char_AtBoundary_IncludesLastPosition()
    {
        // "abcba" — search for 'a' in range (0, 5) should find both 0 and 4
        var sw = new SW("abcba");
        var indices = sw.EnumerateIndicesOf('a', 0, 5).ToList();
        Assert.Equal([0, 4], indices);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_Char_AtBoundary_IncludesLastPosition()
    {
        var sw = new SW("abcba");
        var indices = sw.EnumerateIndicesOfUnsafe('a', 0, 5).ToList();
        Assert.Equal([0, 4], indices);
    }

    [Fact]
    public void EnumerateIndicesOf_Char_ExcludesBeyondRange()
    {
        // "ababab" — search for 'a' in range (0, 4) = positions 0,1,2,3
        // 'a' at 0 and 2; position 4 is outside range
        var sw = new SW("ababab");
        var indices = sw.EnumerateIndicesOf('a', 0, 4).ToList();
        Assert.Equal([0, 2], indices);
    }
    #endregion

    #region Composite Scenarios (multiple operations chained)
    [Fact]
    public void TrimStart_ThenAppend_ThenTrimEnd_WorksCorrectly()
    {
        var sw = new SW("  Hello World  ");
        sw.TrimStart(' ');
        sw.TrimEnd(' ');
        Assert.Equal("Hello World", sw.ToString());
        sw.Append("!");
        Assert.Equal("Hello World!", sw.ToString());
    }

    [Fact]
    public void TrimStart_ThenReplace_ThenAppend_WorksCorrectly()
    {
        var sw = new SW("XXHello");
        sw.TrimStart('X');
        Assert.Equal("Hello", sw.ToString());
        sw.Replace(0, 5, "Hi".AsSpan());
        Assert.Equal("Hi", sw.ToString());
        sw.Append(" World");
        Assert.Equal("Hi World", sw.ToString());
    }

    [Fact]
    public void MultipleTrimStart_ThenIndexer_WorksCorrectly()
    {
        var sw = new SW("ABCDHello");
        sw.TrimStart('A');
        sw.TrimStart('B');
        sw.TrimStart('C');
        sw.TrimStart('D');
        Assert.Equal("Hello", sw.ToString());
        Assert.Equal('H', sw[0]);
        Assert.Equal('o', sw[^1]);
    }

    [Fact]
    public void ReplaceAtStart_BumpsStartPointer_ThenAppend()
    {
        var sw = new SW("XXXXHello");
        // Replace at index 0 with shorter string bumps Start
        sw.Replace(0, 4, default);
        Assert.Equal("Hello", sw.ToString());
        sw.Append("!");
        Assert.Equal("Hello!", sw.ToString());
    }

    [Fact]
    public void TrimStart_ThenTruncate_ThenAppend()
    {
        var sw = new SW("XXHello World");
        sw.TrimStart('X');
        Assert.Equal("Hello World", sw.ToString());
        sw.Truncate(5);
        Assert.Equal("Hello", sw.ToString());
        sw.Append("!");
        Assert.Equal("Hello!", sw.ToString());
    }

    [Fact]
    public void TrimSequence_BothEnds_ThenAppend()
    {
        var sw = new SW("ababHelloabab");
        sw.TrimSequence("ab");
        Assert.Equal("Hello", sw.ToString());
        sw.Append("!");
        Assert.Equal("Hello!", sw.ToString());
    }

    [Fact]
    public void Trim_BothEnds_ThenRangeIndexer()
    {
        var sw = new SW("  Hello  ");
        sw.Trim(' ');
        Assert.Equal("Hello", sw.ToString());
        Assert.Equal("ell", new string(sw[1..4]));
    }

    [Fact]
    public void Clear_AfterTrimStart_ResetsStartAndEnd()
    {
        var sw = new SW("XXHello");
        sw.TrimStart('X');
        Assert.True(sw.Start > 0);
        sw.Clear();
        Assert.Equal(0, sw.Start);
        Assert.Equal(0, sw.End);
        Assert.Equal(0, sw.Length);
    }
    #endregion

    #region PcreRegex Replace Tests (Bug #16: missing Success check)
    [Fact]
    public void Replace_PcreRegex_NoMatch_DoesNotThrow()
    {
        var sw = new SW("Hello World");
        // Pattern that doesn't match — should be a no-op, not throw
        sw.Replace(new PcreRegex("ZZZZZ"), "replacement");
        Assert.Equal("Hello World", sw.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_WithMatch_ReplacesCorrectly()
    {
        var sw = new SW("Hello World");
        sw.Replace(new PcreRegex("World"), "Earth");
        Assert.Equal("Hello Earth", sw.ToString());
    }
    #endregion

    #region NativeBuffer Pressure Tests
    [Fact]
    public void NativeBuffer_Dispose_DoesNotThrow()
    {
        // This indirectly tests NativeBuffer's pressure tracking through UnsafeStringWeaver
        var sw = new UnsafeStringWeaver(1024);
        sw.Append("Hello World");
        Assert.Equal("Hello World", sw.ToString());
        sw.Dispose(); // Should not throw due to pressure mismatch
    }
    #endregion
}
