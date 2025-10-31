using System.Runtime.InteropServices;

namespace StringWeaver.Tests;

public class StringWeaverTests
{
    [Fact]
    public void Constructor_Default_CreatesWithDefaultCapacity()
    {
        var weaver = new StringWeaver();

        Assert.Equal(0, weaver.Length);
        Assert.True(weaver.Capacity >= 256);
    }

    [Fact]
    public void Constructor_WithCapacity_CreatesWithSpecifiedCapacity()
    {
        var weaver = new StringWeaver(512);

        Assert.Equal(0, weaver.Length);
        Assert.True(weaver.Capacity >= 512);
    }

    [Fact]
    public void Constructor_WithString_CopiesContent()
    {
        var weaver = new StringWeaver("hello");

        Assert.Equal(5, weaver.Length);
        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Constructor_WithStringAndCapacity_CopiesContentWithCapacity()
    {
        var weaver = new StringWeaver("hello", 512);

        Assert.Equal(5, weaver.Length);
        Assert.Equal("hello", weaver.ToString());
        Assert.True(weaver.Capacity >= 512);
    }

    [Fact]
    public void Constructor_WithStringAndCapacity_ThrowsWhenCapacityTooSmall() => Assert.Throws<ArgumentOutOfRangeException>(static () => new StringWeaver("hello", 3));

    [Fact]
    public void Constructor_WithReadOnlySpan_CopiesContent()
    {
        var weaver = new StringWeaver("hello".AsSpan());

        Assert.Equal(5, weaver.Length);
        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Constructor_WithReadOnlySpanAndCapacity_CopiesContent()
    {
        var weaver = new StringWeaver("hello".AsSpan(), 512);

        Assert.Equal(5, weaver.Length);
        Assert.Equal("hello", weaver.ToString());
        Assert.True(weaver.Capacity >= 512);
    }

    [Fact]
    public void Constructor_Copy_CreatesIndependentCopy()
    {
        var original = new StringWeaver("hello");
        var copy = new StringWeaver(original);

        Assert.Equal(original.ToString(), copy.ToString());

        copy.Append(" world");
        Assert.NotEqual(original.ToString(), copy.ToString());
    }

    [Fact]
    public void Constructor_CopyNull_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(static () => new StringWeaver((StringWeaver)null));

    [Fact]
    public void Append_Char_AppendsCharacter()
    {
        var weaver = new StringWeaver();
        weaver.Append('a');

        Assert.Equal(1, weaver.Length);
        Assert.Equal("a", weaver.ToString());
    }

    [Fact]
    public void Append_ReadOnlySpan_AppendsContent()
    {
        var weaver = new StringWeaver();
        weaver.Append("hello".AsSpan());

        Assert.Equal(5, weaver.Length);
        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Append_EmptySpan_DoesNothing()
    {
        var weaver = new StringWeaver("test");
        weaver.Append([]);

        Assert.Equal("test", weaver.ToString());
    }

    [Fact]
    public void Append_String_AppendsString()
    {
        var weaver = new StringWeaver();
        weaver.Append("hello");

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Append_CharArray_AppendsSection()
    {
        var weaver = new StringWeaver();
        var chars = "hello world".ToCharArray();
        weaver.Append(chars, 6, 5);

        Assert.Equal("world", weaver.ToString());
    }

    [Fact]
    public void Append_CharArrayNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver();
        Assert.Throws<ArgumentNullException>(() => weaver.Append(null, 0, 5));
    }

    [Fact]
    public void Append_CharArrayInvalidRange_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver();
        var chars = "hello".ToCharArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Append(chars, -1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Append(chars, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Append(chars, 3, 10));
    }

    [Fact]
    public void Append_CharArrayZeroLength_DoesNothing()
    {
        var weaver = new StringWeaver("test");
        var chars = "hello".ToCharArray();
        weaver.Append(chars, 0, 0);

        Assert.Equal("test", weaver.ToString());
    }

    [Fact]
    public void Append_UnsafePointer_AppendsContent()
    {
        var weaver = new StringWeaver();
        var str = "hello";
        unsafe
        {
            fixed (char* ptr = str)
            {
                weaver.Append(ptr, 5);
            }
        }

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Append_UnsafePointerZeroLength_DoesNothing()
    {
        var weaver = new StringWeaver("test");
        var str = "hello";
        unsafe
        {
            fixed (char* ptr = str)
            {
                weaver.Append(ptr, 0);
            }
        }

        Assert.Equal("test", weaver.ToString());
    }

    [Fact]
    public void Append_UnsafePointerNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver();
        unsafe
        {
            Assert.Throws<ArgumentNullException>(() => weaver.Append((char*)null, 5));
        }
    }

    [Fact]
    public void Append_ManagedRef_AppendsContent()
    {
        var weaver = new StringWeaver();
        var str = "hello";
        var span = str.AsSpan();
        ref readonly var charRef = ref span[0];
        weaver.Append(in charRef, 5);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Append_ManagedRefZeroLength_DoesNothing()
    {
        var weaver = new StringWeaver("test");
        var str = "hello";
        var span = str.AsSpan();
        ref readonly var charRef = ref span[0];
        weaver.Append(in charRef, 0);

        Assert.Equal("test", weaver.ToString());
    }

    [Fact]
    public void Append_ISpanFormattable_AppendsFormattedValue()
    {
        var weaver = new StringWeaver();
        weaver.Append(42);

        Assert.Equal("42", weaver.ToString());
    }

    [Fact]
    public void Append_ISpanFormattableWithFormat_AppendsFormattedValue()
    {
        var weaver = new StringWeaver();
        weaver.Append(42.5, "F2".AsSpan());

        Assert.Equal("42.50", weaver.ToString());
    }

    [Fact]
    public void IndexOf_Char_FindsFirstOccurrence()
    {
        var weaver = new StringWeaver("hello world");

        Assert.Equal(2, weaver.IndexOf('l'));
    }

    [Fact]
    public void IndexOf_CharNotFound_ReturnsMinusOne()
    {
        var weaver = new StringWeaver("hello");

        Assert.Equal(-1, weaver.IndexOf('x'));
    }

    [Fact]
    public void IndexOf_CharWithStart_FindsFromStart()
    {
        var weaver = new StringWeaver("hello world");

        Assert.Equal(3, weaver.IndexOf('l', 3));
    }

    [Fact]
    public void IndexOf_CharInvalidStart_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOf('l', -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOf('l', 10));
    }

    [Fact]
    public void IndexOf_ReadOnlySpan_FindsFirstOccurrence()
    {
        var weaver = new StringWeaver("hello world");

        Assert.Equal(6, weaver.IndexOf("world".AsSpan()));
    }

    [Fact]
    public void IndexOf_ReadOnlySpanNotFound_ReturnsMinusOne()
    {
        var weaver = new StringWeaver("hello");

        Assert.Equal(-1, weaver.IndexOf("world".AsSpan()));
    }

    [Fact]
    public void IndexOf_ReadOnlySpanWithStart_FindsFromStart()
    {
        var weaver = new StringWeaver("hello world hello");

        Assert.Equal(12, weaver.IndexOf("hello".AsSpan(), 6));
    }

    [Fact]
    public void IndexOf_ReadOnlySpanInvalidStart_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOf("ll".AsSpan(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOf("ll".AsSpan(), 10));
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_Char_EnumeratesAllOccurrences()
    {
        var weaver = new StringWeaver("hello world");
        var indices = weaver.EnumerateIndicesOfUnsafe('l').ToList();

        Assert.Equal(3, indices.Count);
        Assert.Equal(2, indices[0]);
        Assert.Equal(3, indices[1]);
        Assert.Equal(9, indices[2]);
    }

    [Fact]
    public void EnumerateIndicesOf_Char_EnumeratesAllOccurrences()
    {
        var weaver = new StringWeaver("hello world");
        var indices = weaver.EnumerateIndicesOf('l').ToList();

        Assert.Equal(3, indices.Count);
        Assert.Equal(2, indices[0]);
        Assert.Equal(3, indices[1]);
        Assert.Equal(9, indices[2]);
    }

    [Fact]
    public void EnumerateIndicesOf_CharModifiedDuringEnumeration_ThrowsInvalidOperationException()
    {
        var weaver = new StringWeaver("hello world");
        var enumerable = weaver.EnumerateIndicesOf('l');

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var index in enumerable)
            {
                weaver.Append('!');
            }
        });
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_ReadOnlySpan_EnumeratesAllOccurrences()
    {
        var weaver = new StringWeaver("hello world hello");
        var indices = new System.Collections.Generic.List<int>();

        foreach (var index in weaver.EnumerateIndicesOfUnsafe("ll".AsSpan()))
        {
            indices.Add(index);
        }

        Assert.Equal(2, indices.Count);
        Assert.Equal(2, indices[0]);
        Assert.Equal(14, indices[1]);
    }

    [Fact]
    public void EnumerateIndicesOf_ReadOnlySpan_EnumeratesAllOccurrences()
    {
        var weaver = new StringWeaver("hello world hello");
        var indices = new System.Collections.Generic.List<int>();

        foreach (var index in weaver.EnumerateIndicesOf("ll".AsSpan()))
        {
            indices.Add(index);
        }

        Assert.Equal(2, indices.Count);
        Assert.Equal(2, indices[0]);
        Assert.Equal(14, indices[1]);
    }

    [Fact]
    public void IndexOf_PcreRegex_FindsFirstMatch()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new PcreRegex(@"\d+");

        Assert.Equal(5, weaver.IndexOf(regex));
    }

    [Fact]
    public void IndexOf_PcreRegexNotFound_ReturnsMinusOne()
    {
        var weaver = new StringWeaver("hello world");
        var regex = new PcreRegex(@"\d+");

        Assert.Equal(-1, weaver.IndexOf(regex));
    }

    [Fact]
    public void IndexOf_PcreRegexNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentNullException>(() => weaver.IndexOf((PcreRegex)null));
    }

    [Fact]
    public void IndexOf_PcreRegexInvalidStart_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var regex = new PcreRegex(@"\w+");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOf(regex, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOf(regex, 10));
    }

    [Fact]
    public void IndexOf_Regex_FindsFirstMatch()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new Regex(@"\d+");

        Assert.Equal(5, weaver.IndexOf(regex));
    }

    [Fact]
    public void IndexOf_RegexNotFound_ReturnsMinusOne()
    {
        var weaver = new StringWeaver("hello world");
        var regex = new Regex(@"\d+");

        Assert.Equal(-1, weaver.IndexOf(regex));
    }

    [Fact]
    public void IndexOf_RegexNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentNullException>(() => weaver.IndexOf((Regex)null));
    }

    [Fact]
    public void IndexOfAny_FindsFirstOccurrence()
    {
        var weaver = new StringWeaver("hello world");

        Assert.Equal(2, weaver.IndexOfAny("lox".AsSpan()));
    }

    [Fact]
    public void IndexOfAny_NotFound_ReturnsMinusOne()
    {
        var weaver = new StringWeaver("hello");

        Assert.Equal(-1, weaver.IndexOfAny("xyz".AsSpan()));
    }

    [Fact]
    public void IndexOfAny_InvalidStart_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOfAny("lo".AsSpan(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOfAny("lo".AsSpan(), 10));
    }

    [Fact]
    public void IndexOfAnyExcept_FindsFirstCharNotInSet()
    {
        var weaver = new StringWeaver("aaabbb");

        Assert.Equal(3, weaver.IndexOfAnyExcept("a".AsSpan()));
    }

    [Fact]
    public void IndexOfAnyExcept_InvalidStart_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOfAnyExcept("a".AsSpan(), -1));
    }

    [Fact]
    public void IndexOfAnyInRange_FindsFirstCharInRange()
    {
        var weaver = new StringWeaver("abc123");

        Assert.Equal(3, weaver.IndexOfAnyInRange('0', '9'));
    }

    [Fact]
    public void IndexOfAnyExceptInRange_FindsFirstCharOutsideRange()
    {
        var weaver = new StringWeaver("123abc");

        Assert.Equal(3, weaver.IndexOfAnyExceptInRange('0', '9'));
    }

    [Fact]
    public void IndexOfAny_SearchValues_FindsFirstOccurrence()
    {
        var weaver = new StringWeaver("hello world");
        var searchValues = SearchValues.Create("lox");

        Assert.Equal(2, weaver.IndexOfAny(searchValues));
    }

    [Fact]
    public void IndexOfAnyExcept_SearchValues_FindsFirstCharNotInSet()
    {
        var weaver = new StringWeaver("aaabbb");
        var searchValues = SearchValues.Create("a");

        Assert.Equal(3, weaver.IndexOfAnyExcept(searchValues));
    }

    [Fact]
    public void Replace_CharToChar_ReplacesFirstOccurrence()
    {
        var weaver = new StringWeaver("hello");
        weaver.Replace('l', 'x');

        Assert.Equal("hexlo", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_CharToChar_ReplacesAllOccurrences()
    {
        var weaver = new StringWeaver("hello");
        weaver.ReplaceAll('l', 'x');

        Assert.Equal("hexxo", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_CharToSameChar_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        weaver.ReplaceAll('l', 'l');

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Replace_SpanToSpan_ReplacesFirstOccurrence()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Replace("world".AsSpan(), "there".AsSpan());

        Assert.Equal("hello there", weaver.ToString());
    }

    [Fact]
    public void Replace_EmptySpan_ThrowsArgumentException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentException>(() => weaver.Replace([], "x".AsSpan()));
    }

    [Fact]
    public void Replace_SameSpan_DoesNothing()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Replace("world".AsSpan(), "world".AsSpan());

        Assert.Equal("hello world", weaver.ToString());
    }

    [Fact]
    public void Replace_OverlappingSpan_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        var span = weaver.Span;
        weaver.Replace(span, span);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Replace_NotFound_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        weaver.Replace("world".AsSpan(), "there".AsSpan());

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_SpanToSpan_ReplacesAllOccurrences()
    {
        var weaver = new StringWeaver("hello hello hello");
        weaver.ReplaceAll("hello".AsSpan(), "hi".AsSpan());

        Assert.Equal("hi hi hi", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_SpanToLongerSpan_ReplacesAndGrows()
    {
        var weaver = new StringWeaver("a b a");
        weaver.ReplaceAll("a".AsSpan(), "longer".AsSpan());

        Assert.Equal("longer b longer", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_SpanToEmpty_RemovesAllOccurrences()
    {
        var weaver = new StringWeaver("hello world");
        weaver.ReplaceAll("l".AsSpan(), []);

        Assert.Equal("heo word", weaver.ToString());
    }

    [Fact]
    public void Replace_Range_ReplacesSpecifiedRange()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Replace(6..11, "there".AsSpan());

        Assert.Equal("hello there", weaver.ToString());
    }

    [Fact]
    public void Replace_IndexLength_ReplacesSpecifiedRange()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Replace(6, 5, "there".AsSpan());

        Assert.Equal("hello there", weaver.ToString());
    }

    [Fact]
    public void Replace_IndexLengthInvalid_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Replace(-1, 1, "x".AsSpan()));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Replace(10, 1, "x".AsSpan()));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Replace(0, 0, "x".AsSpan()));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Replace(0, 10, "x".AsSpan()));
    }

    [Fact]
    public void Replace_PcreRegex_ReplacesFirstMatch()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new PcreRegex(@"\d+");
        weaver.Replace(regex, "XXX".AsSpan());

        Assert.Equal("helloXXXworld456", weaver.ToString());
    }

    [Fact]
    public void Replace_PcreRegexNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentNullException>(() => weaver.Replace((PcreRegex)null, "x".AsSpan()));
    }

    [Fact]
    public void ReplaceAll_PcreRegex_ReplacesAllMatches()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new PcreRegex(@"\d+");
        weaver.ReplaceAll(regex, "XXX".AsSpan());

        Assert.Equal("helloXXXworldXXX", weaver.ToString());
    }

    [Fact]
    public void Replace_PcreRegexWithWriter_ReplacesFirstMatch()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new PcreRegex(@"\d+");
        weaver.Replace(regex, 10, static (buffer, match) => "XXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXworld", weaver.ToString());
    }

    [Fact]
    public void Replace_PcreRegexWithWriterZeroBufferSize_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new PcreRegex(@"\d+");
        weaver.Replace(regex, 0, static (buffer, match) => { });

        Assert.Equal("helloworld", weaver.ToString());
    }

    [Fact]
    public void Replace_PcreRegexWithWriterNullAction_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123");
        var regex = new PcreRegex(@"\d+");

        weaver.Replace(regex, 10, null);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Replace_PcreRegexNegativeBufferSize_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var regex = new PcreRegex(@"\w+");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Replace(regex, -1, (b, m) => { }));
    }

    [Fact]
    public void ReplaceAll_PcreRegexWithWriter_ReplacesAllMatches()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new PcreRegex(@"\d+");
        weaver.ReplaceAll(regex, 10, static (buffer, match) => "XXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXworldXXX", weaver.ToString());
    }

    [Fact]
    public void ReplaceExact_PcreRegex_ReplacesFirstMatch()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new PcreRegex(@"\d+");
        weaver.ReplaceExact(regex, 3, static (buffer, match) => "XXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceExact_PcreRegexZeroLength_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new PcreRegex(@"\d+");
        weaver.ReplaceExact(regex, 0, static (buffer, match) => { });

        Assert.Equal("helloworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceAllExact_PcreRegex_ReplacesAllMatches()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new PcreRegex(@"\d+");
        weaver.ReplaceAllExact(regex, 3, static (buffer, match) => "XXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXworldXXX", weaver.ToString());
    }

    [Fact]
    public void Replace_Regex_ReplacesFirstMatch()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new Regex(@"\d+");
        weaver.Replace(regex, "XXX".AsSpan());

        Assert.Equal("helloXXXworld456", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_Regex_ReplacesAllMatches()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new Regex(@"\d+");
        weaver.ReplaceAll(regex, "XXX".AsSpan());

        Assert.Equal("helloXXXworldXXX", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_RegexDifferentLengths_ReplacesAllMatches()
    {
        var weaver = new StringWeaver("a1b22c333");
        var regex = new Regex(@"\d+");
        weaver.ReplaceAll(regex, "X".AsSpan());

        Assert.Equal("aXbXcX", weaver.ToString());
    }

    [Fact]
    public void Replace_RegexWithWriter_ReplacesFirstMatch()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new Regex(@"\d+");
        weaver.Replace(regex, 10, static (buffer, match) => "XXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_RegexWithWriter_ReplacesAllMatches()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new Regex(@"\d+");
        weaver.ReplaceAll(regex, 10, static (buffer, match) => "XXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXworldXXX", weaver.ToString());
    }

    [Fact]
    public void ReplaceExact_Regex_ReplacesFirstMatch()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new Regex(@"\d+");
        weaver.ReplaceExact(regex, 3, static (buffer, match) => "XXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceAllExact_Regex_ReplacesAllMatches()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new Regex(@"\d+");
        weaver.ReplaceAllExact(regex, 3, static (buffer, match) => "XXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXworldXXX", weaver.ToString());
    }

    [Fact]
    public void ReplaceExact_RegexNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var regex = new Regex(@"\w+");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.ReplaceExact(regex, -1, (b, m) => { }));
    }

    [Fact]
    public void Remove_Range_RemovesSpecifiedRange()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Remove(5..11);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Remove_IndexLength_RemovesSpecifiedRange()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Remove(5, 6);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Trim_Char_TrimsFromBothEnds()
    {
        var weaver = new StringWeaver("xxxhelloxxx");
        weaver.Trim('x');

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Trim_Chars_TrimsAnyFromBothEnds()
    {
        var weaver = new StringWeaver("xyzhellovwx");
        weaver.Trim("xyzv".AsSpan());

        Assert.Equal("hellovw", weaver.ToString());
    }

    [Fact]
    public void TrimStart_Char_TrimsFromStart()
    {
        var weaver = new StringWeaver("xxxhello");
        weaver.TrimStart('x');

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimStart_EmptyBuffer_DoesNothing()
    {
        var weaver = new StringWeaver();
        weaver.TrimStart('x');

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void TrimStart_Chars_TrimsAnyFromStart()
    {
        var weaver = new StringWeaver("xyzhellovwx");
        weaver.TrimStart("xyz".AsSpan());

        Assert.Equal("hellovwx", weaver.ToString());
    }

    [Fact]
    public void TrimEnd_Char_TrimsFromEnd()
    {
        var weaver = new StringWeaver("helloxxx");
        weaver.TrimEnd('x');

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimEnd_EmptyBuffer_DoesNothing()
    {
        var weaver = new StringWeaver();
        weaver.TrimEnd('x');

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void TrimEnd_Chars_TrimsAnyFromEnd()
    {
        var weaver = new StringWeaver("xyzhellovwx");
        weaver.TrimEnd("vwx".AsSpan());

        Assert.Equal("xyzhello", weaver.ToString());
    }

    [Fact]
    public void TrimSequence_TrimsSequenceFromBothEnds()
    {
        var weaver = new StringWeaver("abcabchelloabcabc");
        weaver.TrimSequence("abc".AsSpan());

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceStart_TrimsSequenceFromStart()
    {
        var weaver = new StringWeaver("abcabchello");
        weaver.TrimSequenceStart("abc".AsSpan());

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceStart_SingleChar_UsesCharOverload()
    {
        var weaver = new StringWeaver("xxxhello");
        weaver.TrimSequenceStart("x".AsSpan());

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceStart_EmptySequence_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        weaver.TrimSequenceStart([]);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceStart_SequenceLongerThanBuffer_DoesNothing()
    {
        var weaver = new StringWeaver("hi");
        weaver.TrimSequenceStart("hello".AsSpan());

        Assert.Equal("hi", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_TrimsSequenceFromEnd()
    {
        var weaver = new StringWeaver("helloabcabc");
        weaver.TrimSequenceEnd("abc".AsSpan());

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_SingleChar_UsesCharOverload()
    {
        var weaver = new StringWeaver("helloxxx");
        weaver.TrimSequenceEnd("x".AsSpan());

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_EmptySequence_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        weaver.TrimSequenceEnd([]);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Truncate_SetsLengthToSpecifiedValue()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Truncate(5);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Truncate_InvalidLength_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Truncate(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Truncate(10));
    }

    [Fact]
    public void Trim_Int_DecreasesLengthByCount()
    {
        var weaver = new StringWeaver("hello");
        weaver.Trim(2);

        Assert.Equal("hel", weaver.ToString());
    }

    [Fact]
    public void Trim_IntNegative_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Trim(-1));
    }

    [Fact]
    public void Trim_IntGreaterThanLength_ClearsBuffer()
    {
        var weaver = new StringWeaver("hello");
        weaver.Trim(10);

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void Expand_IncreasesLengthBySpecifiedAmount()
    {
        var weaver = new StringWeaver("hello");
        var initialLength = weaver.Length;
        weaver.Expand(5);

        Assert.Equal(initialLength + 5, weaver.Length);
    }

    [Fact]
    public void Expand_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Expand(-1));
    }

    [Fact]
    public void Expand_BeyondCapacity_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Expand(weaver.Capacity + 100));
    }

    [Fact]
    public void GetWritableMemory_ReturnsMemoryBeyondLength()
    {
        var weaver = new StringWeaver("hello");
        var memory = weaver.GetWritableMemory();

        Assert.True(memory.Length > 0);
    }

    [Fact]
    public void GetWritableMemory_WithMinimumSize_EnsuresMinimumSize()
    {
        var weaver = new StringWeaver("hello");
        var memory = weaver.GetWritableMemory(100);

        Assert.True(memory.Length >= 100);
    }

    [Fact]
    public void GetWritableSpan_ReturnsSpanBeyondLength()
    {
        var weaver = new StringWeaver("hello");
        var span = weaver.GetWritableSpan();

        Assert.True(span.Length > 0);
    }

    [Fact]
    public void GetWritableSpan_WithMinimumSize_EnsuresMinimumSize()
    {
        var weaver = new StringWeaver("hello");
        var span = weaver.GetWritableSpan(100);

        Assert.True(span.Length >= 100);
    }

    [Fact]
    public void GetWritableSpanAndExpand_WorksTogether()
    {
        var weaver = new StringWeaver("hello");
        var span = weaver.GetWritableSpan(5);
        " wor".AsSpan().CopyTo(span);
        weaver.Expand(4);

        Assert.Equal("hello wor", weaver.ToString());
    }

    [Fact]
    public void EnsureCapacity_EnsuresMinimumCapacity()
    {
        var weaver = new StringWeaver();
        weaver.EnsureCapacity(1000);

        Assert.True(weaver.Capacity >= 1000);
    }

    [Fact]
    public void EnsureCapacity_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver();

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.EnsureCapacity(-1));
    }

    [Fact]
    public void Clear_ResetsLength()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Clear();

        Assert.Equal(0, weaver.Length);
    }

    [Fact]
    public void Clear_WithWipe_ClearsContent()
    {
        var weaver = new StringWeaver("hello");
        weaver.Clear(true);

        Assert.Equal(0, weaver.Length);
        Assert.Equal(0, weaver.Span.Length);

        var asBytes = MemoryMarshal.AsBytes(weaver.FullSpan);
        var index = asBytes.IndexOfAnyExcept((byte)0);
        Assert.Equal(-1, index);
    }

    [Fact]
    public void CopyTo_Span_CopiesContent()
    {
        var weaver = new StringWeaver("hello");
        var destination = new char[10];
        weaver.CopyTo(destination.AsSpan());

        Assert.Equal("hello", new string(destination, 0, 5));
    }

    [Fact]
    public void CopyTo_SpanTooSmall_ThrowsArgumentException()
    {
        var weaver = new StringWeaver("hello");
        var destination = new char[3];

        Assert.Throws<ArgumentException>(() => weaver.CopyTo(destination.AsSpan()));
    }

    [Fact]
    public void CopyTo_Memory_CopiesContent()
    {
        var weaver = new StringWeaver("hello");
        var destination = new char[10];
        weaver.CopyTo(destination.AsMemory());

        Assert.Equal("hello", new string(destination, 0, 5));
    }

    [Fact]
    public void CopyTo_CharArray_CopiesContent()
    {
        var weaver = new StringWeaver("hello");
        var destination = new char[10];
        weaver.CopyTo(destination, 2);

        Assert.Equal("hello", new string(destination, 2, 5));
    }

    [Fact]
    public void CopyTo_CharArrayInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var destination = new char[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.CopyTo(destination, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.CopyTo(destination, 11));
    }

    [Fact]
    public void CopyTo_CharArrayTooSmall_ThrowsArgumentException()
    {
        var weaver = new StringWeaver("hello");
        var destination = new char[6];

        Assert.Throws<ArgumentException>(() => weaver.CopyTo(destination, 2));
    }

    [Fact]
    public void CopyTo_ManagedPointer_CopiesContent()
    {
        var weaver = new StringWeaver("hello");
        var destination = new char[10];
        ref var charRef = ref destination[0];
        weaver.CopyTo(ref charRef);

        Assert.Equal("hello", new string(destination, 0, 5));
    }

    [Fact]
    public void CopyTo_UnmanagedPointer_CopiesContent()
    {
        var weaver = new StringWeaver("hello");
        var destination = new char[10];
        unsafe
        {
            fixed (char* ptr = destination)
            {
                weaver.CopyTo(ptr);
            }
        }

        Assert.Equal("hello", new string(destination, 0, 5));
    }

    [Fact]
    public void GetStream_ReturnsStreamWithDefaultEncoding()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream();

        Assert.NotNull(stream);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void GetStream_ReturnsStreamWithSpecifiedEncoding()
    {
        var weaver = new StringWeaver();
        var stream = weaver.GetStream(Encoding.UTF8);

        Assert.NotNull(stream);
    }

    [Fact]
    public void GetStream_ReturnsSameInstanceWhenNotDisposed()
    {
        var weaver = new StringWeaver();
        var stream1 = weaver.GetStream();
        var stream2 = weaver.GetStream();

        Assert.Same(stream1, stream2);
    }

    [Fact]
    public void ToString_ReturnsStringRepresentation()
    {
        var weaver = new StringWeaver("hello");

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Drain_ReturnsStringAndClears()
    {
        var weaver = new StringWeaver("hello");
        var result = weaver.Drain();

        Assert.Equal("hello", result);
        Assert.Equal(0, weaver.Length);
    }

    [Fact]
    public void Drain_WithWipe_ReturnsStringAndClearsWithWipe()
    {
        var weaver = new StringWeaver("hello");
        var result = weaver.Drain(true);

        Assert.Equal("hello", result);
        Assert.Equal(0, weaver.Length);
    }

    [Fact]
    public void Indexer_Get_ReturnsCharAtIndex()
    {
        var weaver = new StringWeaver("hello");

        Assert.Equal('h', weaver[0]);
        Assert.Equal('e', weaver[1]);
        Assert.Equal('o', weaver[^1]);
    }

    [Fact]
    public void Indexer_Set_SetsCharAtIndex()
    {
        var weaver = new StringWeaver("hello");
        weaver[0] = 'H';

        Assert.Equal("Hello", weaver.ToString());
    }

    [Fact]
    public void Indexer_GetInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver[10]);
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver[^10]);
    }

    [Fact]
    public void Indexer_SetInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver[10] = 'x');
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver[^10] = 'x');
    }

    [Fact]
    public void Properties_ReturnCorrectValues()
    {
        var weaver = new StringWeaver("hello");

        Assert.Equal(5, weaver.Length);
        Assert.True(weaver.Capacity >= 5);
        Assert.True(weaver.FreeCapacity >= 0);
        Assert.Equal(weaver.Capacity - weaver.Length, weaver.FreeCapacity);
    }

    [Fact]
    public void Memory_ReturnsUsedPortion()
    {
        var weaver = new StringWeaver("hello");
        var memory = weaver.Memory;

        Assert.Equal(5, memory.Length);
        Assert.Equal("hello", new string(memory.Span));
    }

    [Fact]
    public void Span_ReturnsUsedPortion()
    {
        var weaver = new StringWeaver("hello");
        var span = weaver.Span;

        Assert.Equal(5, span.Length);
        Assert.Equal("hello", new string(span));
    }

    [Fact]
    public void IBufferWriter_Advance_IncreasesLength()
    {
        var weaver = new StringWeaver("hello");
        IBufferWriter<char> bufferWriter = weaver;
        bufferWriter.Advance(5);

        Assert.Equal(10, weaver.Length);
    }

    [Fact]
    public void IBufferWriter_GetMemory_ReturnsWritableMemory()
    {
        var weaver = new StringWeaver();
        IBufferWriter<char> bufferWriter = weaver;
        var memory = bufferWriter.GetMemory(10);

        Assert.True(memory.Length >= 10);
    }
    [Fact]
    public void IBufferWriter_GetSpan_ReturnsWritableSpan()
    {
        var weaver = new StringWeaver();
        IBufferWriter<char> bufferWriter = weaver;
        var span = bufferWriter.GetSpan(10);

        Assert.True(span.Length >= 10);
    }

    [Fact]
    public void Version_IncreasesOnModification()
    {
        var weaver = new StringWeaver("hello");
        var initialVersion = weaver.Version;

        weaver.Append('!');
        Assert.NotEqual(initialVersion, weaver.Version);
    }

    [Fact]
    public void Version_IncreasesOnIndexerSet()
    {
        var weaver = new StringWeaver("hello");
        var initialVersion = weaver.Version;

        weaver[0] = 'H';
        Assert.NotEqual(initialVersion, weaver.Version);
    }

    [Fact]
    public void UnsafeIndexEnumerator_MoveNext_UpdatesCurrent()
    {
        var weaver = new StringWeaver("hello");
        var enumerable = weaver.EnumerateIndicesOfUnsafe('l');
        using var enumerator = enumerable.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);
    }

    [Fact]
    public void UnsafeIndexEnumerator_MoveNextBeyondLength_ReturnsFalse()
    {
        var weaver = new StringWeaver("h");
        var enumerable = weaver.EnumerateIndicesOfUnsafe('x');
        using var enumerator = enumerable.GetEnumerator();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void IndexEnumerator_Current_StartsAtMinusOne()
    {
        var weaver = new StringWeaver("hello");
        var enumerable = weaver.EnumerateIndicesOf("l".AsSpan());
        var enumerator = enumerable.GetEnumerator();

        Assert.Equal(-1, enumerator.Current);
    }

    [Fact]
    public void IndexEnumerator_MoveNextAfterModification_ThrowsInvalidOperationException()
    {
        var weaver = new StringWeaver("hello world");

        Assert.Throws<InvalidOperationException>(() =>
        {
            var enumerable = weaver.EnumerateIndicesOf("l".AsSpan());
            var enumerator = enumerable.GetEnumerator();

            enumerator.MoveNext();
            weaver.Append('!');

            enumerator.MoveNext();
        });
    }

    [Fact]
    public void IndexEnumerator_MoveNextBeyondLength_ReturnsFalse()
    {
        var weaver = new StringWeaver("h");
        var enumerable = weaver.EnumerateIndicesOf("x".AsSpan());
        var enumerator = enumerable.GetEnumerator();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Replace_WithShorterReplacement_ShiftsContentCorrectly()
    {
        var weaver = new StringWeaver("hello wonderful world");
        weaver.Replace("wonderful".AsSpan(), "good".AsSpan());

        Assert.Equal("hello good world", weaver.ToString());
    }

    [Fact]
    public void Replace_WithLongerReplacement_GrowsAndShiftsCorrectly()
    {
        var weaver = new StringWeaver("hello tiny world");
        weaver.Replace("tiny".AsSpan(), "absolutely massive".AsSpan());

        Assert.Equal("hello absolutely massive world", weaver.ToString());
    }

    [Fact]
    public void Replace_WithEmptyReplacement_RemovesContent()
    {
        var weaver = new StringWeaver("hello world");
        weaver.Replace(5, 6, []);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_MultipleOccurrencesWithDifferentLengths_HandlesCorrectly()
    {
        var weaver = new StringWeaver("a b a b a");
        weaver.ReplaceAll("a".AsSpan(), "longer".AsSpan());

        Assert.Equal("longer b longer b longer", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_WithEmptyReplacement_RemovesAllOccurrences()
    {
        var weaver = new StringWeaver("a b a b a");
        weaver.ReplaceAll("a ".AsSpan(), []);

        Assert.Equal("b b a", weaver.ToString());
    }

    [Fact]
    public void ReplaceCore_WithMultipleIndices_ReplacesAllCorrectly()
    {
        var weaver = new StringWeaver("a a a");
        weaver.ReplaceAll("a".AsSpan(), "bb".AsSpan());

        Assert.Equal("bb bb bb", weaver.ToString());
    }

    [Fact]
    public void Append_MultipleChars_GrowsBufferAsNeeded()
    {
        var weaver = new StringWeaver(10);
        for (var i = 0; i < 100; i++)
        {
            weaver.Append('a');
        }

        Assert.Equal(100, weaver.Length);
        Assert.True(weaver.Capacity >= 100);
    }

    [Fact]
    public void Append_LargeSpan_GrowsBufferAsNeeded()
    {
        var weaver = new StringWeaver(10);
        var largeString = new string('a', 1000);
        weaver.Append(largeString.AsSpan());

        Assert.Equal(1000, weaver.Length);
        Assert.Equal(largeString, weaver.ToString());
    }

    [Fact]
    public void Replace_NearCapacity_GrowsCorrectly()
    {
        var weaver = new StringWeaver("short");
        var longReplacement = new string('x', 1000);
        weaver.Replace(0, 5, longReplacement.AsSpan());

        Assert.Equal(longReplacement, weaver.ToString());
    }

    [Fact]
    public void TrimStart_AllCharsMatch_ClearsBuffer()
    {
        var weaver = new StringWeaver("aaaaa");
        weaver.TrimStart('a');

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void TrimEnd_AllCharsMatch_ClearsBuffer()
    {
        var weaver = new StringWeaver("aaaaa");
        weaver.TrimEnd('a');

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceStart_NoMatch_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        weaver.TrimSequenceStart("abc".AsSpan());

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimSequenceEnd_NoMatch_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        weaver.TrimSequenceEnd("abc".AsSpan());

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void TrimSequence_PartialMatchAtEnd_KeepsPartial()
    {
        var weaver = new StringWeaver("helloab");
        weaver.TrimSequence("abc".AsSpan());

        Assert.Equal("helloab", weaver.ToString());
    }

    [Fact]
    public void Replace_PcreRegexWithWriterAndNullTerminator_UsesContentBeforeNull()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new PcreRegex(@"\d+");
        weaver.Replace(regex, 10, static (buffer, match) =>
        {
            "XX".AsSpan().CopyTo(buffer);
            buffer[2] = '\0';
            buffer[3] = 'Y';
        });

        Assert.Equal("helloXXworld", weaver.ToString());
    }

    [Fact]
    public void Replace_PcreRegexWithWriterNoNullTerminator_UsesFullBuffer()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new PcreRegex(@"\d+");
        weaver.Replace(regex, 5, static (buffer, match) => "XXXXX".AsSpan().CopyTo(buffer));

        Assert.Equal("helloXXXXXworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_PcreRegexWithWriter_ClearsBufferBetweenIterations()
    {
        var weaver = new StringWeaver("a1b2c");
        var regex = new PcreRegex(@"\d");
        var iteration = 0;
        weaver.ReplaceAll(regex, 10, (buffer, match) =>
        {
            if (iteration == 0)
            {
                "LONGTEXT".AsSpan().CopyTo(buffer);
                buffer[8] = '\0';
            }
            else
            {
                "X".AsSpan().CopyTo(buffer);
                buffer[1] = '\0';
            }
            iteration++;
        });

        Assert.Equal("aLONGTEXTbXc", weaver.ToString());
    }

    [Fact]
    public void ReplaceExact_PcreRegexNullAction_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123");
        var regex = new PcreRegex(@"\d+");

        weaver.ReplaceExact(regex, 3, null);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceExact_PcreRegexNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var regex = new PcreRegex(@"\w+");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.ReplaceExact(regex, -1, (b, m) => { }));
    }

    [Fact]
    public void ReplaceAllExact_PcreRegexNullAction_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123");
        var regex = new PcreRegex(@"\d+");

        weaver.ReplaceAllExact(regex, 3, null);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceAllExact_PcreRegexNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var regex = new PcreRegex(@"\w+");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.ReplaceAllExact(regex, -1, (b, m) => { }));
    }

    [Fact]
    public void ReplaceAllExact_PcreRegexZeroLength_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new PcreRegex(@"\d+");
        weaver.ReplaceAllExact(regex, 0, static (buffer, match) => { });

        Assert.Equal("helloworld", weaver.ToString());
    }

    [Fact]
    public void Replace_RegexNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentNullException>(() => weaver.Replace((Regex)null, "x".AsSpan()));
    }

    [Fact]
    public void ReplaceAll_RegexNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentNullException>(() => weaver.ReplaceAll((Regex)null, "x".AsSpan()));
    }

    [Fact]
    public void Replace_RegexWithWriterNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentNullException>(() => weaver.Replace((Regex)null, 10, (b, m) => { }));
    }

    [Fact]
    public void Replace_RegexNegativeBufferSize_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var regex = new Regex(@"\w+");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.Replace(regex, -1, (b, m) => { }));
    }

    [Fact]
    public void Replace_RegexZeroBufferSize_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new Regex(@"\d+");
        weaver.Replace(regex, 0, static (buffer, match) => { });

        Assert.Equal("helloworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_RegexWithWriterNull_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123");
        var regex = new Regex(@"\d+");

        weaver.ReplaceAll(regex, 10, null);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_RegexNegativeBufferSize_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var regex = new Regex(@"\w+");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.ReplaceAll(regex, -1, (b, m) => { }));
    }

    [Fact]
    public void ReplaceAll_RegexZeroBufferSize_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new Regex(@"\d+");
        weaver.ReplaceAll(regex, 0, static (buffer, match) => { });

        Assert.Equal("helloworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceExact_RegexNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentNullException>(() => weaver.ReplaceExact((Regex)null, 3, (b, m) => { }));
    }

    [Fact]
    public void ReplaceExact_RegexZeroLength_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new Regex(@"\d+");
        weaver.ReplaceExact(regex, 0, static (buffer, match) => { });

        Assert.Equal("helloworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceExact_RegexNullAction_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123");
        var regex = new Regex(@"\d+");

        weaver.ReplaceExact(regex, 3, null);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceAllExact_RegexNull_ThrowsArgumentNullException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentNullException>(() => weaver.ReplaceAllExact((Regex)null, 3, (b, m) => { }));
    }

    [Fact]
    public void ReplaceAllExact_RegexNullAction_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123");
        var regex = new Regex(@"\d+");

        weaver.ReplaceAllExact(regex, 3, null);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceAllExact_RegexZeroLength_ReplacesWithEmpty()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new Regex(@"\d+");
        weaver.ReplaceAllExact(regex, 0, static (buffer, match) => { });

        Assert.Equal("helloworld", weaver.ToString());
    }

    [Fact]
    public void Replace_RegexWithWriter_UsesNullTerminator()
    {
        var weaver = new StringWeaver("hello123world");
        var regex = new Regex(@"\d+");
        weaver.Replace(regex, 10, static (buffer, match) =>
        {
            "XX".AsSpan().CopyTo(buffer);
            buffer[2] = '\0';
        });

        Assert.Equal("helloXXworld", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_RegexWithWriter_CreatesNewEnumeratorOnLengthChange()
    {
        var weaver = new StringWeaver("a1b22c");
        var regex = new Regex(@"\d+");
        weaver.ReplaceAll(regex, 10, static (buffer, match) =>
        {
            "X".AsSpan().CopyTo(buffer);
            buffer[1] = '\0';
        });

        Assert.Equal("aXbXc", weaver.ToString());
    }

    [Fact]
    public void ReplaceAllExact_RegexWithWriter_CreatesNewEnumeratorOnLengthChange()
    {
        var weaver = new StringWeaver("a1b22c");
        var regex = new Regex(@"\d+");
        weaver.ReplaceAllExact(regex, 1, static (buffer, match) => "X".AsSpan().CopyTo(buffer));

        Assert.Equal("aXbXc", weaver.ToString());
    }

    [Fact]
    public void IndexOf_CharAtEndOfBuffer_Finds()
    {
        var weaver = new StringWeaver("hello");
        Assert.Equal(4, weaver.IndexOf('o'));
    }

    [Fact]
    public void IndexOf_SpanAtEndOfBuffer_Finds()
    {
        var weaver = new StringWeaver("hello world");
        Assert.Equal(6, weaver.IndexOf("world".AsSpan()));
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_CharWithStart_StartsFromCorrectPosition()
    {
        var weaver = new StringWeaver("hello world");
        var indices = weaver.EnumerateIndicesOfUnsafe('l', 3).ToList();

        Assert.Equal(2, indices.Count);
        Assert.Equal(3, indices[0]);
        Assert.Equal(9, indices[1]);
    }

    [Fact]
    public void EnumerateIndicesOf_CharWithStart_StartsFromCorrectPosition()
    {
        var weaver = new StringWeaver("hello world");
        var indices = weaver.EnumerateIndicesOf('l', 3).ToList();

        Assert.Equal(2, indices.Count);
        Assert.Equal(3, indices[0]);
        Assert.Equal(9, indices[1]);
    }

    [Fact]
    public void EnumerateIndicesOfUnsafe_SpanWithStart_StartsFromCorrectPosition()
    {
        var weaver = new StringWeaver("hello hello hello");
        var indices = new System.Collections.Generic.List<int>();

        foreach (var index in weaver.EnumerateIndicesOfUnsafe("hello".AsSpan(), 6))
        {
            indices.Add(index);
        }

        Assert.Equal(2, indices.Count);
        Assert.Equal(6, indices[0]);
        Assert.Equal(12, indices[1]);
    }

    [Fact]
    public void EnumerateIndicesOf_SpanWithStart_StartsFromCorrectPosition()
    {
        var weaver = new StringWeaver("hello hello hello");
        var indices = new System.Collections.Generic.List<int>();

        foreach (var index in weaver.EnumerateIndicesOf("hello".AsSpan(), 6))
        {
            indices.Add(index);
        }

        Assert.Equal(2, indices.Count);
        Assert.Equal(6, indices[0]);
        Assert.Equal(12, indices[1]);
    }

    [Fact]
    public void Replace_CharNotFound_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        weaver.Replace('x', 'y');

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_CharNotFound_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        weaver.ReplaceAll('x', 'y');

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void Constructor_WithReadOnlySpanAndSmallCapacity_UsesDefaultCapacity()
    {
        var weaver = new StringWeaver("hi".AsSpan(), 100);

        Assert.True(weaver.Capacity >= 100);
    }

    [Fact]
    public void Constructor_WithReadOnlySpanAndCapacityLessThanDefault_UsesLargerOfTwo()
    {
        var weaver = new StringWeaver("hello".AsSpan(), 10);

        Assert.True(weaver.Capacity >= 256);
    }

    [Fact]
    public void Indexer_GetWithNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver[-5]);
    }

    [Fact]
    public void Indexer_SetWithNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver[-5] = 'x');
    }

    [Fact]
    public void FreeCapacity_ReturnsCorrectValue()
    {
        var weaver = new StringWeaver(100);
        weaver.Append("hello");

        Assert.Equal(weaver.Capacity - 5, weaver.FreeCapacity);
    }

    [Fact]
    public void MaxCapacity_IsIntMaxValue() => Assert.Equal(int.MaxValue, StringWeaver.MaxCapacity);

    [Fact]
    public void ReplaceAll_EmptyFromSpan_ThrowsArgumentException()
    {
        var weaver = new StringWeaver("hello");

        Assert.Throws<ArgumentException>(() => weaver.ReplaceAll([], "x".AsSpan()));
    }

    [Fact]
    public void ReplaceAll_OverlappingSameOffset_DoesNothing()
    {
        var weaver = new StringWeaver("hello");
        var span = weaver.Span;
        weaver.ReplaceAll(span, span);

        Assert.Equal("hello", weaver.ToString());
    }

    [Fact]
    public void ReplaceAll_SameContent_DoesNothing()
    {
        var weaver = new StringWeaver("hello world");
        weaver.ReplaceAll("hello".AsSpan(), "hello".AsSpan());

        Assert.Equal("hello world", weaver.ToString());
    }

    [Fact]
    public void IndexOf_PcreRegexWithStart_FindsFromStart()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new PcreRegex(@"\d+");

        Assert.Equal(13, weaver.IndexOf(regex, 8));
    }

    [Fact]
    public void IndexOf_RegexWithStart_FindsFromStart()
    {
        var weaver = new StringWeaver("hello123world456");
        var regex = new Regex(@"\d+");

        Assert.Equal(13, weaver.IndexOf(regex, 8));
    }

    [Fact]
    public void IndexOf_RegexInvalidStart_ThrowsArgumentOutOfRangeException()
    {
        var weaver = new StringWeaver("hello");
        var regex = new Regex(@"\w+");

        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOf(regex, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => weaver.IndexOf(regex, 10));
    }

    [Fact]
    public void GetWritableMemory_ZeroMinimumSize_ReturnsNonZeroMemory()
    {
        var weaver = new StringWeaver("hello");
        var memory = weaver.GetWritableMemory(0);

        Assert.True(memory.Length > 0);
    }

    [Fact]
    public void GetWritableSpan_NegativeMinimumSize_ReturnsNonZeroSpan()
    {
        var weaver = new StringWeaver("hello");
        var span = weaver.GetWritableSpan(-10);

        Assert.True(span.Length > 0);
    }

    [Fact]
    public void TrimStart_CharsEmptyBuffer_DoesNothing()
    {
        var weaver = new StringWeaver();
        weaver.TrimStart("xyz".AsSpan());

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void TrimEnd_CharsEmptyBuffer_DoesNothing()
    {
        var weaver = new StringWeaver();
        weaver.TrimEnd("xyz".AsSpan());

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void Trim_CharsEmptyBuffer_DoesNothing()
    {
        var weaver = new StringWeaver();
        weaver.Trim("xyz".AsSpan());

        Assert.Equal("", weaver.ToString());
    }
}