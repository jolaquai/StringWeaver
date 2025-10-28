using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

using PCRE;

using Xunit;

namespace StringWeaver.Tests;

public class StringWeaverTests
{
    #region Constructor Tests
    [Fact]
    public void Constructor_Default_CreatesEmptyBuffer()
    {
        var sb = new StringWeaver();
        Assert.Equal(0, sb.Length);
        Assert.True(sb.Capacity >= 256); // Default capacity
        Assert.Equal("", sb.ToString());
    }

    [Fact]
    public void Constructor_WithCapacity_CreatesBufferWithSpecifiedCapacity()
    {
        var sb = new StringWeaver(512);
        Assert.Equal(0, sb.Length);
        Assert.True(sb.Capacity >= 512);
    }

    [Fact]
    public void Constructor_WithInitialContent_CopiesContent()
    {
        var sb = new StringWeaver("Hello");
        Assert.Equal(5, sb.Length);
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void Constructor_WithInitialContentAndCapacity_CreatesBufferWithBoth()
    {
        var sb = new StringWeaver("Test".AsSpan(), 100);
        Assert.Equal(4, sb.Length);
        Assert.True(sb.Capacity >= 100);
        Assert.Equal("Test", sb.ToString());
    }

    [Fact]
    public void Constructor_WithCapacityLessThanContent_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StringWeaver("Hello".AsSpan(), 2));
    }

    [Fact]
    public void Constructor_Copy_CreatesIndependentCopy()
    {
        var original = new StringWeaver("Original");
        var copy = new StringWeaver(original);

        Assert.Equal("Original", copy.ToString());

        copy.Append(" Modified");
        Assert.Equal("Original", original.ToString());
        Assert.Equal("Original Modified", copy.ToString());
    }

    [Fact]
    public void Constructor_CopyNull_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => new StringWeaver((StringWeaver)null));
    }
    #endregion

    #region Append Tests
    [Fact]
    public void Append_Char_AddsToEnd()
    {
        var sb = new StringWeaver();
        sb.Append('A');
        sb.Append('B');
        Assert.Equal("AB", sb.ToString());
    }

    [Fact]
    public void Append_Span_AddsToEnd()
    {
        var sb = new StringWeaver();
        sb.Append("Hello".AsSpan());
        sb.Append(" World".AsSpan());
        Assert.Equal("Hello World", sb.ToString());
    }

    [Fact]
    public void Append_EmptySpan_DoesNothing()
    {
        var sb = new StringWeaver("Test");
        sb.Append([]);
        Assert.Equal("Test", sb.ToString());
    }

    [Fact]
    public void Append_CausesGrowth_ExpandsBuffer()
    {
        var sb = new StringWeaver(2);
        sb.Append("This is a longer string");
        Assert.Equal("This is a longer string", sb.ToString());
    }
    #endregion

    #region Indexer Tests
    [Fact]
    public void Indexer_Get_ReturnsCorrectChar()
    {
        var sb = new StringWeaver("Hello");
        Assert.Equal('H', sb[0]);
        Assert.Equal('o', sb[^1]);
        Assert.Equal('e', sb[1]);
    }

    [Fact]
    public void Indexer_Set_ModifiesChar()
    {
        var sb = new StringWeaver("Hello");
        sb[0] = 'J';
        sb[^1] = 'y';
        Assert.Equal("Jelly", sb.ToString());
    }

    [Fact]
    public void Indexer_OutOfBounds_ThrowsException()
    {
        var sb = new StringWeaver("Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sb[5]);
        Assert.Throws<ArgumentOutOfRangeException>(() => sb[^5]);
    }
    #endregion

    #region IndexOf Tests
    [Fact]
    public void IndexOf_Char_FindsFirstOccurrence()
    {
        var sb = new StringWeaver("Hello World");
        Assert.Equal(2, sb.IndexOf('l'));
        Assert.Equal(6, sb.IndexOf('W'));
        Assert.Equal(-1, sb.IndexOf('z'));
    }

    [Fact]
    public void IndexOf_CharWithStart_FindsFromPosition()
    {
        var sb = new StringWeaver("Hello World");
        Assert.Equal(3, sb.IndexOf('l', 3));
        Assert.Equal(9, sb.IndexOf('l', 4));
        Assert.Equal(-1, sb.IndexOf('H', 1));
    }

    [Fact]
    public void IndexOf_Span_FindsFirstOccurrence()
    {
        var sb = new StringWeaver("Hello World Hello");
        Assert.Equal(0, sb.IndexOf("Hello".AsSpan()));
        Assert.Equal(6, sb.IndexOf("World".AsSpan()));
        Assert.Equal(-1, sb.IndexOf("Goodbye".AsSpan()));
    }

    [Fact]
    public void IndexOf_SpanWithStart_FindsFromPosition()
    {
        var sb = new StringWeaver("Hello World Hello");
        Assert.Equal(12, sb.IndexOf("Hello".AsSpan(), 5));
        Assert.Equal(-1, sb.IndexOf("World".AsSpan(), 10));
    }
    #endregion

    #region EnumerateIndicesOf Tests
    [Fact]
    public void EnumerateIndicesOfUnsafe_Char_FindsAllOccurrences()
    {
        var sb = new StringWeaver("abcabcabc");
        var indices = sb.EnumerateIndicesOfUnsafe('a').ToList();
        Assert.Equal(new[] { 0, 3, 6 }, indices);
    }

    [Fact]
    public void EnumerateIndicesOf_Char_DetectsModification()
    {
        var sb = new StringWeaver("abcabc");
        var enumerator = sb.EnumerateIndicesOf('a').GetEnumerator();

        enumerator.MoveNext();
        Assert.Equal(0, enumerator.Current);

        sb.Append('x'); // Modify buffer

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_Span_FindsAllOccurrences()
    {
        var sb = new StringWeaver("ababab");
        var indices = new List<int>();
        foreach (var idx in sb.EnumerateIndicesOfUnsafe("ab".AsSpan()))
        {
            indices.Add(idx);
        }
        Assert.Equal(new[] { 0, 2, 4 }, indices);
    }

    [Fact]
    public void EnumerateIndicesOf_Span_DetectsModification()
    {
        // Have to do it this way because the enumerator is a ref struct
#pragma warning disable IDE0022 // Use expression body for method
        Assert.Throws<InvalidOperationException>(() =>
        {
            var sb = new StringWeaver("ababab");
            var enumerator = sb.EnumerateIndicesOf("ab".AsSpan()).GetEnumerator();

            enumerator.MoveNext();
            Assert.Equal(0, enumerator.Current);

            sb.Append('x'); // Modify buffer
            return enumerator.MoveNext();
        });
#pragma warning restore IDE0022 // Use expression body for method
    }
    #endregion

    #region Replace Tests - Single Occurrence
    [Fact]
    public void Replace_CharToChar_ReplacesFirst()
    {
        var sb = new StringWeaver("Hello");
        sb.Replace('l', 'w');
        Assert.Equal("Hewlo", sb.ToString());
    }

    [Fact]
    public void Replace_SpanToSpan_SameLength()
    {
        var sb = new StringWeaver("Hello World");
        sb.Replace("World".AsSpan(), "Earth".AsSpan());
        Assert.Equal("Hello Earth", sb.ToString());
    }

    [Fact]
    public void Replace_SpanToSpan_Shorter()
    {
        var sb = new StringWeaver("Hello World");
        sb.Replace("World".AsSpan(), "You".AsSpan());
        Assert.Equal("Hello You", sb.ToString());
    }

    [Fact]
    public void Replace_SpanToSpan_Longer()
    {
        var sb = new StringWeaver("Hello World");
        sb.Replace("World".AsSpan(), "Universe".AsSpan());
        Assert.Equal("Hello Universe", sb.ToString());
    }

    [Fact]
    public void Replace_SpanToEmpty_RemovesText()
    {
        var sb = new StringWeaver("Hello World");
        sb.Replace("World".AsSpan(), []);
        Assert.Equal("Hello ", sb.ToString());
    }

    [Fact]
    public void Replace_Range_ReplacesSpecifiedRange()
    {
        var sb = new StringWeaver("Hello World");
        sb.Replace(6..11, "Earth".AsSpan());
        Assert.Equal("Hello Earth", sb.ToString());
    }

    [Fact]
    public void Replace_IndexLength_ReplacesSpecifiedRange()
    {
        var sb = new StringWeaver("Hello World");
        sb.Replace(0, 5, "Goodbye".AsSpan());
        Assert.Equal("Goodbye World", sb.ToString());
    }

    [Fact]
    public void Replace_InvalidRange_ThrowsException()
    {
        var sb = new StringWeaver("Test");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sb.Replace(-1, 2, "X".AsSpan()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sb.Replace(3, 5, "X".AsSpan()));
    }
    #endregion

    #region ReplaceAll Tests
    [Fact]
    public void ReplaceAll_CharToChar_ReplacesAll()
    {
        var sb = new StringWeaver("Hello");
        sb.ReplaceAll('l', 'w');
        Assert.Equal("Hewwo", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_SameChar_DoesNothing()
    {
        var sb = new StringWeaver("Hello");
        sb.ReplaceAll('l', 'l');
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_SpanToSpan_ReplacesAll()
    {
        var sb = new StringWeaver("abc abc abc");
        sb.ReplaceAll("abc".AsSpan(), "xyz".AsSpan());
        Assert.Equal("xyz xyz xyz", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_SpanToShorter_CompactsText()
    {
        var sb = new StringWeaver("Hello World Hello World");
        sb.ReplaceAll("World".AsSpan(), "W".AsSpan());
        Assert.Equal("Hello W Hello W", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_SpanToLonger_ExpandsText()
    {
        var sb = new StringWeaver("a b a b");
        sb.ReplaceAll("a".AsSpan(), "aaa".AsSpan());
        Assert.Equal("aaa b aaa b", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_EmptyFrom_ThrowsException()
    {
        var sb = new StringWeaver("Test");
        Assert.Throws<ArgumentException>(() =>
            sb.ReplaceAll([], "X".AsSpan()));
    }
    #endregion

    #region Remove Tests
    [Fact]
    public void Remove_Range_RemovesSpecifiedRange()
    {
        var sb = new StringWeaver("Hello World");
        sb.Remove(5..11);
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void Remove_IndexLength_RemovesSpecifiedRange()
    {
        var sb = new StringWeaver("Hello World");
        sb.Remove(0, 6);
        Assert.Equal("World", sb.ToString());
    }
    #endregion

    #region Trim Tests
    [Fact]
    public void Trim_Char_RemovesFromBothEnds()
    {
        var sb = new StringWeaver("***Hello***");
        sb.Trim('*');
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimStart_Char_RemovesFromStart()
    {
        var sb = new StringWeaver("   Hello");
        sb.TrimStart(' ');
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimEnd_Char_RemovesFromEnd()
    {
        var sb = new StringWeaver("Hello   ");
        sb.TrimEnd(' ');
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void Trim_Span_RemovesAnyFromBothEnds()
    {
        var sb = new StringWeaver("*#*Hello*#*");
        sb.Trim("*#".AsSpan());
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimSequence_RemovesExactSequence()
    {
        var sb = new StringWeaver("ababHelloabab");
        sb.TrimSequence("ab".AsSpan());
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimSequenceStart_RemovesFromStart()
    {
        var sb = new StringWeaver("xyxyHello");
        sb.TrimSequenceStart("xy".AsSpan());
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_RemovesFromEnd()
    {
        var sb = new StringWeaver("Helloxyxy");
        sb.TrimSequenceEnd("xy".AsSpan());
        Assert.Equal("Hello", sb.ToString());
    }
    #endregion

    #region Length Modification Tests
    [Fact]
    public void Truncate_ReducesLength()
    {
        var sb = new StringWeaver("Hello World");
        sb.Truncate(5);
        Assert.Equal(5, sb.Length);
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void Truncate_InvalidLength_ThrowsException()
    {
        var sb = new StringWeaver("Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Truncate(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Truncate(10));
    }

    [Fact]
    public void Trim_Count_RemovesFromEnd()
    {
        var sb = new StringWeaver("Hello World");
        sb.Trim(6);
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void Expand_IncreasesLength()
    {
        var sb = new StringWeaver("Hello");
        var span = sb.GetWritableSpan(10);
        " World".AsSpan().CopyTo(span);
        sb.Expand(6);
        Assert.Equal("Hello World", sb.ToString());
    }

    [Fact]
    public void GetWritableSpan_ReturnsWritableArea()
    {
        var sb = new StringWeaver("Hello");
        var span = sb.GetWritableSpan(10);
        Assert.True(span.Length >= 10);

        " World".AsSpan().CopyTo(span);
        sb.Expand(6);
        Assert.Equal("Hello World", sb.ToString());
    }

    [Fact]
    public void EnsureCapacity_GrowsBuffer()
    {
        var sb = new StringWeaver(10);
        sb.EnsureCapacity(100);
        Assert.True(sb.Capacity >= 100);
    }
    #endregion

    #region Clear Tests
    [Fact]
    public void Clear_ResetsLength()
    {
        var sb = new StringWeaver("Hello World");
        var capacity = sb.Capacity;
        sb.Clear();

        Assert.Equal(0, sb.Length);
        Assert.Equal(capacity, sb.Capacity);
        Assert.Equal("", sb.ToString());
    }

    [Fact]
    public void Clear_WithWipe_ClearsContent()
    {
        var sb = new StringWeaver("Sensitive Data");
        sb.Clear(true);

        Assert.Equal(0, sb.Length);
        Assert.Equal("", sb.ToString());
    }
    #endregion

    #region Properties Tests
    [Fact]
    public void Properties_CorrectValues()
    {
        var sb = new StringWeaver("Hello");

        Assert.Equal(5, sb.Length);
        Assert.True(sb.Capacity >= 5);
        Assert.Equal(sb.Capacity - 5, sb.FreeCapacity);

        sb.Append(" World");
        Assert.Equal(11, sb.Length);
        Assert.Equal(sb.Capacity - 11, sb.FreeCapacity);
    }

    [Fact]
    public void Span_ReturnsCurrentContent()
    {
        var sb = new StringWeaver("Hello");
        var span = sb.Span;

        Assert.Equal(5, span.Length);
        Assert.True(span.SequenceEqual("Hello".AsSpan()));

        // Modifying through span should affect buffer
        span[0] = 'J';
        Assert.Equal("Jello", sb.ToString());
    }
    #endregion

    #region Regex Tests
    [Fact]
    public void Replace_Regex_ReplacesFirst()
    {
        var sb = new StringWeaver("Hello 123 World 456");
        var regex = new Regex(@"\d+");
        sb.Replace(regex, "XXX".AsSpan());
        Assert.Equal("Hello XXX World 456", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_Regex_ReplacesAll()
    {
        var sb = new StringWeaver("Hello 123 World 456");
        var regex = new Regex(@"\d+");
        sb.ReplaceAll(regex, "XXX".AsSpan());
        Assert.Equal("Hello XXX World XXX", sb.ToString());
    }

    [Fact]
    public void Replace_RegexWithAction_UsesReplacementAction()
    {
        var sb = new StringWeaver("Hello 123 World");
        var regex = new Regex(@"\d+");
        sb.Replace(regex, 10, (buffer, match) =>
        {
            "NUM".AsSpan().CopyTo(buffer);
            buffer[3] = '\0';
        });
        Assert.Equal("Hello NUM World", sb.ToString());
    }

    [Fact]
    public void ReplaceExact_Regex_ReplacesWithExactLength()
    {
        var sb = new StringWeaver("Hello 123 World");
        var regex = new Regex(@"\d+");
        sb.ReplaceExact(regex, 5, (buffer, match) =>
        {
            "[NUM]".AsSpan().CopyTo(buffer);
        });
        Assert.Equal("Hello [NUM] World", sb.ToString());
    }
    [Fact]
    public void Replace_PcreRegex_ReplacesFirst()
    {
        var sb = new StringWeaver("Hello 123 World 456");
        var regex = new PcreRegex(@"\d+");
        sb.Replace(regex, "XXX".AsSpan());
        Assert.Equal("Hello XXX World 456", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_PcreRegex_ReplacesAll()
    {
        var sb = new StringWeaver("Hello 123 World 456");
        var regex = new PcreRegex(@"\d+");
        sb.ReplaceAll(regex, "XXX".AsSpan());
        Assert.Equal("Hello XXX World XXX", sb.ToString());
    }
    #endregion

    #region ISpanFormattable Tests
    [Fact]
    public void Append_SpanFormattable_AppendsFormatted()
    {
        var sb = new StringWeaver("Value: ");
        sb.Append(123.456, "F2".AsSpan());
        Assert.Equal("Value: 123.46", sb.ToString());
    }

    [Fact]
    public void Append_SpanFormattableWithProvider_UsesProvider()
    {
        var sb = new StringWeaver("Date: ");
        var date = new DateTime(2024, 1, 15);
        sb.Append(date, "d".AsSpan(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Date: 01/15/2024", sb.ToString());
    }
    #endregion

    #region Additional Coverage Tests
    [Fact]
    public void Memory_Property_ReturnsCorrectMemory()
    {
        var sb = new StringWeaver("Hello");
        var mem = sb.Memory;
        Assert.Equal(5, mem.Length);
        Assert.Equal("Hello", mem.Span.ToString());
    }

    [Fact]
    public void RangeIndexer_Get_ReturnsSlice()
    {
        var sb = new StringWeaver("HelloWorld");
        var slice = sb[0..5];
        Assert.Equal("Hello", slice.ToString());
    }

    [Fact]
    public void RangeIndexer_Set_WritesSlice()
    {
        var sb = new StringWeaver("HelloWorld");
        var span = sb[5..10];
        "Earth".AsSpan().CopyTo(span);
        Assert.Equal("HelloEarth", sb.ToString());
    }

    [Fact]
    public void RangeIndexer_OutOfRange_Throws()
    {
        var sb = new StringWeaver("Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = sb[5..6]; });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Span<char> replacement = new char[1] { 'X' };
            sb[5..6] = replacement; // out of range setter
        });
    }

    [Fact]
    public void Append_ArraySection_Appends()
    {
        var chars = "HelloWorld".ToCharArray();
        var sb = new StringWeaver();
        sb.Append(chars, 0, 5);
        sb.Append(chars, 5, 5);
        Assert.Equal("HelloWorld", sb.ToString());
    }

    [Fact]
    public void Append_ArraySection_InvalidArgs_Throw()
    {
        var sb = new StringWeaver();
        Assert.Throws<ArgumentNullException>(() => sb.Append((char[])null!, 0, 1));
        var chars = "Hi".ToCharArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Append(chars, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Append(chars, 0, 5));
    }

    [Fact]
    public void Append_RefChar_AppendsBlock()
    {
        var data = "Block".ToCharArray();
        var sb = new StringWeaver("Start-");
        sb.Append(in data[0], data.Length);
        Assert.Equal("Start-Block", sb.ToString());
    }

    [Fact]
    public void Append_SpanFormattable_GrowPath()
    {
        var sb = new StringWeaver(4); // very small capacity
        var big = new GrowFormat("ABCDEFGH"); // needs8 chars
        sb.Append(big, default);
        Assert.Equal("ABCDEFGH", sb.ToString());
    }

    [Fact]
    public void IndexOfAny_FindsChar()
    {
        var sb = new StringWeaver("abcdef");
        Assert.Equal(2, sb.IndexOfAny("xyzcd".AsSpan()));
        Assert.Equal(-1, sb.IndexOfAny("XYZ".AsSpan()));
    }

    [Fact]
    public void IndexOfAny_InvalidStart_Throws()
    {
        var sb = new StringWeaver("abc");
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.IndexOfAny("a".AsSpan(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.IndexOfAny("a".AsSpan(), 4));
    }

    [Fact]
    public void IndexOfAnyExcept_FindsDifferentChar()
    {
        var sb = new StringWeaver("aaab");
        Assert.Equal(3, sb.IndexOfAnyExcept("a".AsSpan()));
    }

    [Fact]
    public void IndexOfAnyInRange_FindsInRange()
    {
        var sb = new StringWeaver("hello");
        Assert.Equal(0, sb.IndexOfAnyInRange('a', 'h'));
        Assert.Equal(-1, sb.IndexOfAnyInRange('x', 'z'));
    }

    [Fact]
    public void IndexOfAnyExceptInRange_FindsOutsideRange()
    {
        var sb = new StringWeaver("abcdx");
        Assert.Equal(4, sb.IndexOfAnyExceptInRange('a', 'd'));
    }

    [Fact]
    public void IndexOfAny_SearchValues_Works()
    {
        var sv = SearchValues.Create("xyz".AsSpan());
        var sb = new StringWeaver("hello x");
        Assert.Equal(6, sb.IndexOfAny(sv));
        Assert.Equal(-1, sb.IndexOfAny(SearchValues.Create("123".AsSpan())));
    }

    [Fact]
    public void IndexOfAnyExcept_SearchValues_Works()
    {
        var sv = SearchValues.Create("abc".AsSpan());
        var sb = new StringWeaver("aaabbbcddd");
        var idx = sb.IndexOfAnyExcept(sv);
        Assert.True(idx >= 0);
        Assert.Equal('d', sb[idx]);
    }

    [Fact]
    public void ReplaceAllExact_Regex_ReplacesAllWithExactLength()
    {
        var sb = new StringWeaver("Value 123 and 456");
        var regex = new Regex(@"\d+");
        sb.ReplaceAllExact(regex, 3, (buffer, match) =>
        {
            "NUM".AsSpan().CopyTo(buffer);
        });
        Assert.Equal("Value NUM and NUM", sb.ToString());
    }

    [Fact]
    public void TrimSequenceStart_SingleCharPath()
    {
        var sb = new StringWeaver("aaaHello");
        sb.TrimSequenceStart("a".AsSpan());
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_SingleCharPath()
    {
        var sb = new StringWeaver("Helloaaa");
        sb.TrimSequenceEnd("a".AsSpan());
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void Trim_Count_GreaterThanLength_Clears()
    {
        var sb = new StringWeaver("Hi");
        sb.Trim(10);
        Assert.Equal(0, sb.Length);
        Assert.Equal("", sb.ToString());
    }

    [Fact]
    public void Expand_Negative_Throws()
    {
        var sb = new StringWeaver("Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Expand(-1));
    }

    [Fact]
    public void EnsureCapacity_Negative_Throws()
    {
        var sb = new StringWeaver();
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.EnsureCapacity(-5));
    }

    [Fact]
    public void Drain_ReturnsContentAndClears()
    {
        var sb = new StringWeaver("Hello");
        var drained = sb.Drain();
        Assert.Equal("Hello", drained);
        Assert.Equal(0, sb.Length);
    }

    [Fact]
    public void Drain_Wipe_Clears()
    {
        var sb = new StringWeaver("SecretData");
        var drained = sb.Drain(true);
        Assert.Equal("SecretData", drained);
        Assert.Equal(0, sb.Length);
    }

    [Fact]
    public void IBufferWriter_InterfaceUsage_Writes()
    {
        var sb = new StringWeaver();
        IBufferWriter<char> writer = sb;
        var span = writer.GetSpan(5);
        "Hello".AsSpan().CopyTo(span);
        writer.Advance(5);
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void GetStream_WritesBytes()
    {
        var sb = new StringWeaver();
        using var stream = sb.GetStream(Encoding.UTF8);
        var bytes = Encoding.UTF8.GetBytes("Hello");
        stream.Write(bytes, 0, bytes.Length);
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void Constructor_StringAndCapacity_CreatesBuffer()
    {
        var sb = new StringWeaver("Hi", 10);
        Assert.Equal(2, sb.Length);
        Assert.True(sb.Capacity >= 10);
        Assert.Equal("Hi", sb.ToString());
    }

    [Fact]
    public void Constructor_SpanOnly_CreatesBuffer()
    {
        var sb = new StringWeaver("Span".AsSpan());
        Assert.Equal(4, sb.Length);
        Assert.Equal("Span", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_RemoveAllOccurrences()
    {
        var sb = new StringWeaver("abc abc abc");
        sb.ReplaceAll("abc".AsSpan(), []);
        Assert.Equal("  ", sb.ToString());
    }

    [Fact]
    public void Remove_InvalidRange_Throws()
    {
        var sb = new StringWeaver("Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Remove(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Remove(2, 10));
    }

    [Fact]
    public void TrimStart_NoTrimWhenNotPresent()
    {
        var sb = new StringWeaver("Hello");
        sb.TrimStart("xyz".AsSpan());
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimEnd_NoTrimWhenNotPresent()
    {
        var sb = new StringWeaver("Hello");
        sb.TrimEnd("xyz".AsSpan());
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimSequenceStart_EmptySpan_NoOp()
    {
        var sb = new StringWeaver("Hello");
        sb.TrimSequenceStart([]);
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_EmptySpan_NoOp()
    {
        var sb = new StringWeaver("Hello");
        sb.TrimSequenceEnd([]);
        Assert.Equal("Hello", sb.ToString());
    }

    [Fact]
    public void TrimSequenceStart_ValueLongerThanBuffer_NoOp()
    {
        var sb = new StringWeaver("Hi");
        sb.TrimSequenceStart("Hello".AsSpan());
        Assert.Equal("Hi", sb.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_ValueLongerThanBuffer_NoOp()
    {
        var sb = new StringWeaver("Hi");
        sb.TrimSequenceEnd("Hello".AsSpan());
        Assert.Equal("Hi", sb.ToString());
    }

    [Fact]
    public void Expand_BeyondCapacity_Throws()
    {
        var sb = new StringWeaver("Data");
        var invalid = sb.FreeCapacity + 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Expand(invalid));
    }

    [Fact]
    public void Append_SpanFormattable_GrowWhenNoFreeCapacity()
    {
        var baseContent = new string('a', 256);
        var sb = new StringWeaver(baseContent, 256);
        sb.Append(123.45, "F2".AsSpan());
        Assert.True(sb.Length > 256);
        Assert.Contains("123.45", sb.ToString());
    }

    [Fact]
    public void GetWritableMemory_GrowsForSizeHint()
    {
        var sb = new StringWeaver(16);
        sb.Append(new string('x', 16));
        var mem = sb.GetWritableMemory(100);
        Assert.True(mem.Length >= 100);
        Assert.True(sb.Capacity >= 116);
    }

    [Fact]
    public void CopyTo_Span_Works()
    {
        var sb = new StringWeaver("Hello");
        Span<char> dest = stackalloc char[5];
        sb.CopyTo(dest);
        Assert.Equal("Hello", new string(dest));
    }

    [Fact]
    public void CopyTo_Span_DestinationTooSmall_Throws()
    {
        var sb = new StringWeaver("Hello");
        Span<char> dest = stackalloc char[4];
        var threw = false;
        try { sb.CopyTo(dest); } catch (ArgumentException) { threw = true; }
        Assert.True(threw);
    }

    [Fact]
    public void CopyTo_Memory_Works()
    {
        var sb = new StringWeaver("Hello");
        var mem = new Memory<char>(new char[5]);
        sb.CopyTo(mem);
        Assert.Equal("Hello", new string(mem.Span));
    }

    [Fact]
    public void CopyTo_ArrayIndex_Works()
    {
        var sb = new StringWeaver("Hello");
        var arr = new char[10];
        sb.CopyTo(arr, 2);
        Assert.Equal('H', arr[2]);
        Assert.Equal('o', arr[6]);
    }

    [Fact]
    public void CopyTo_ArrayIndex_InvalidArgs_Throw()
    {
        var sb = new StringWeaver("Hi");
        var arr = new char[2];
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.CopyTo(arr, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.CopyTo(arr, 3));
        var small = new char[2];
        Assert.Throws<ArgumentException>(() => sb.CopyTo(small, 1));
    }

    [Fact]
    public unsafe void CopyTo_UnmanagedPointer_Writes()
    {
        var sb = new StringWeaver("Hello");
        var arr = new char[5];
        fixed (char* ptr = arr)
        {
            sb.CopyTo(ptr);
        }
        Assert.Equal("Hello", new string(arr));
    }

    [Fact]
    public void CopyTo_RefChar_Writes()
    {
        var sb = new StringWeaver("Hi");
        var arr = new char[2];
        ref var start = ref arr[0];
        sb.CopyTo(ref start);
        Assert.Equal("Hi", new string(arr));
    }

    [Fact]
    public void Replace_PcreRegex_Action_ReplacesFirst()
    {
        var sb = new StringWeaver("Value123 and456");
        var regex = new PcreRegex(@"\d+");
        sb.Replace(regex, 10, (buffer, match) =>
        {
            "NUM".AsSpan().CopyTo(buffer);
            buffer[3] = '\0';
        });
        Assert.Contains("NUM", sb.ToString());
        Assert.DoesNotContain("123", sb.ToString());
    }

    [Fact]
    public void ReplaceAll_PcreRegex_Action_ReplacesAll()
    {
        var sb = new StringWeaver("111222333");
        var regex = new PcreRegex(@"\d+");
        var callCount = 0;
        sb.ReplaceAll(regex, 8, (buffer, match) =>
        {
            "X".AsSpan().CopyTo(buffer);
            buffer[1] = '\0';
            callCount++;
        });
        Assert.Equal("X", sb.ToString());
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void ReplaceExact_PcreRegex_Action_ReplacesFirstExactLength()
    {
        var sb = new StringWeaver("Val123 end");
        var regex = new PcreRegex(@"\d+");
        sb.ReplaceExact(regex, 5, (buffer, match) =>
        {
            "[NUM]".AsSpan().CopyTo(buffer);
        });
        Assert.Contains("[NUM]", sb.ToString());
    }

    [Fact]
    public void ReplaceAllExact_PcreRegex_Action_ReplacesAllExactLength()
    {
        var sb = new StringWeaver("A1 B22 C333");
        var regex = new PcreRegex(@"\d+");
        sb.ReplaceAllExact(regex, 3, (buffer, match) =>
        {
            "NUM".AsSpan().CopyTo(buffer);
        });
        Assert.Equal("ANUM BNUM CNUM", sb.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_BufferSizeZero_RemovesFirstMatch()
    {
        var sb = new StringWeaver("Number123 here");
        var regex = new PcreRegex(@"\d+");
        sb.Replace(regex, 0, (buffer, match) => { });
        Assert.Equal("Number here", sb.ToString());
    }

    [Fact]
    public void Replace_PcreRegex_NegativeBufferSize_Throws()
    {
        var sb = new StringWeaver("123");
        var regex = new PcreRegex(@"\d+");
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.Replace(regex, -1, (buffer, match) => { }));
    }

    [Fact]
    public void ReplaceExact_PcreRegex_LengthZero_RemovesFirstMatch()
    {
        var sb = new StringWeaver("Num999 end");
        var regex = new PcreRegex(@"\d+");
        sb.ReplaceExact(regex, 0, (buffer, match) => { });
        Assert.Equal("Num end", sb.ToString());
    }

    [Fact]
    public void ReplaceExact_PcreRegex_NegativeLength_Throws()
    {
        var sb = new StringWeaver("123");
        var regex = new PcreRegex(@"\d+");
        Assert.Throws<ArgumentOutOfRangeException>(() => sb.ReplaceExact(regex, -5, (buffer, match) => { }));
    }

    [Fact]
    public void ReplaceAllExact_PcreRegex_LengthZero_RemovesAllMatches()
    {
        var sb = new StringWeaver("A1 B2 C3");
        var regex = new PcreRegex(@"\d+");
        sb.ReplaceAllExact(regex, 0, (buffer, match) => { });
        Assert.Equal("A B C", sb.ToString());
    }

    [Fact]
    public void Stream_ReadSeekSetLength_Throw()
    {
        var sb = new StringWeaver();
        using var stream = sb.GetStream(Encoding.UTF8);
        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        var bytes = Encoding.UTF8.GetBytes("Hello");
        stream.Write(bytes, 0, bytes.Length);

        Assert.Throws<NotSupportedException>(() => stream.Read(bytes, 0, bytes.Length));
        Assert.Throws<NotSupportedException>(() => _ = stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 2);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(10));
    }

    [Fact]
    public void Stream_Write_InvalidArgs_Throw()
    {
        var sb = new StringWeaver();
        using var stream = sb.GetStream(Encoding.UTF8);
        Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 0));
        var bytes = new byte[5];
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(bytes, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(bytes, 0, 10));
    }

    [Fact]
    public void Stream_Dispose_RecreatesOnNextCall()
    {
        var sb = new StringWeaver();
        var s1 = sb.GetStream();
        s1.Dispose();
        var s2 = sb.GetStream();
        Assert.NotSame(s1, s2);
    }
    #endregion

    private readonly struct GrowFormat : ISpanFormattable
    {
        private readonly string _value;
        public GrowFormat(string value) => _value = value;
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider provider)
        {
            if (destination.Length < _value.Length)
            {
                charsWritten = 0;
                return false;
            }
            _value.AsSpan().CopyTo(destination);
            charsWritten = _value.Length;
            return true;
        }
        public string ToString(string format, IFormatProvider formatProvider) => _value;
    }
}