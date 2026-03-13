using System.Text;

namespace StringWeaver.Tests.IO;

public class WeaverTextWriterTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        Assert.NotNull(writer);
        Assert.Equal(Encoding.Unicode, writer.Encoding);
    }

    [Fact]
    public void Encoding_ReturnsUnicodeEncoding()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        Assert.Equal(Encoding.Unicode, writer.Encoding);
    }

    [Fact]
    public void Flush_DoesNotThrow()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Flush();
    }

    [Fact]
    public async Task FlushAsync_CompletesSuccessfully()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        await writer.FlushAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Write_Bool_True_WritesTrue()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(true);

        Assert.Equal("true", weaver.ToString());
    }

    [Fact]
    public void Write_Bool_False_WritesFalse()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(false);

        Assert.Equal("false", weaver.ToString());
    }

    [Fact]
    public void Write_Char_WritesChar()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write('A');

        Assert.Equal("A", weaver.ToString());
    }

    [Fact]
    public void Write_CharArray_WritesFullArray()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var buffer = new[] { 'H', 'e', 'l', 'l', 'o' };

        writer.Write(buffer);

        Assert.Equal("Hello", weaver.ToString());
    }

    [Fact]
    public void Write_CharArraySubset_WritesSubset()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var buffer = new[] { 'A', 'B', 'C', 'D', 'E' };

        writer.Write(buffer, 1, 3);

        Assert.Equal("BCD", weaver.ToString());
    }

    [Fact]
    public void Write_Decimal_WritesDecimal()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(123.45m);

        Assert.Equal(123.45m.ToString(), weaver.ToString());
    }

    [Fact]
    public void Write_Double_WritesDouble()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(3.14159);

        Assert.Equal(3.14159.ToString(), weaver.ToString());
    }

    [Fact]
    public void Write_Int_WritesInt()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(42);

        Assert.Equal("42", weaver.ToString());
    }

    [Fact]
    public void Write_Long_WritesLong()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(9876543210L);

        Assert.Equal("9876543210", weaver.ToString());
    }

    [Fact]
    public void Write_Object_WritesToString()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var obj = new { Name = "Test", Value = 123 };

        writer.Write(obj);

        Assert.Contains("Test", weaver.ToString());
        Assert.Contains("123", weaver.ToString());
    }

    [Fact]
    public void Write_ReadOnlySpan_WritesSpan()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var span = "SpanContent".AsSpan();

        writer.Write(span);

        Assert.Equal("SpanContent", weaver.ToString());
    }

    [Fact]
    public void Write_Float_WritesFloat()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(2.5f);

        Assert.Equal("2.5", weaver.ToString());
    }

    [Fact]
    public void Write_String_WritesString()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write("Hello World");

        Assert.Equal("Hello World", weaver.ToString());
    }

    [Fact]
    public void Write_StringBuilder_WritesContent()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var sb = new StringBuilder("StringBuilder Content");

        writer.Write(sb);

        Assert.Equal("StringBuilder Content", weaver.ToString());
    }

    [Fact]
    public void Write_UInt_WritesUInt()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(42u);

        Assert.Equal("42", weaver.ToString());
    }

    [Fact]
    public void Write_ULong_WritesULong()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(18446744073709551615ul);

        Assert.Equal("18446744073709551615", weaver.ToString());
    }

    [Fact]
    public void WriteLine_Empty_WritesNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine();

        Assert.Equal(Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_Bool_WritesValueAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine(true);

        Assert.Equal("true" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_Char_WritesCharAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine('X');

        Assert.Equal("X" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_CharArray_WritesArrayAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var buffer = new[] { 'T', 'e', 's', 't' };

        writer.WriteLine(buffer);

        Assert.Equal("Test" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_CharArraySubset_WritesSubsetAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var buffer = new[] { 'A', 'B', 'C', 'D', 'E' };

        writer.WriteLine(buffer, 1, 3);

        Assert.Equal("BCD" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_Decimal_WritesDecimalAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine(99.99m);

        Assert.Equal("99.99" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_Double_WritesDoubleAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine(2.71828);

        Assert.StartsWith("2.71828", weaver.ToString());
        Assert.EndsWith(Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_Int_WritesIntAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine(100);

        Assert.Equal("100" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_Long_WritesLongAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine(1234567890L);

        Assert.Equal("1234567890" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_Object_WritesToStringAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var obj = new { Value = 42 };

        writer.WriteLine(obj);

        Assert.Contains("42", weaver.ToString());
        Assert.EndsWith(Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_ReadOnlySpan_WritesSpanAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var span = "SpanLine".AsSpan();

        writer.WriteLine(span);

        Assert.Equal("SpanLine" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_Float_WritesFloatAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine(1.5f);

        Assert.Equal("1.5" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_String_WritesStringAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine("Test Line");

        Assert.Equal("Test Line" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_StringBuilder_WritesContentAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var sb = new StringBuilder("Builder Line");

        writer.WriteLine(sb);

        Assert.Equal("Builder Line" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_UInt_WritesUIntAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine(999u);

        Assert.Equal("999" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void WriteLine_ULong_WritesULongAndNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine(123456789ul);

        Assert.Equal("123456789" + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void MultipleWrites_AccumulatesContent()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write("Hello");
        writer.Write(' ');
        writer.Write("World");

        Assert.Equal("Hello World", weaver.ToString());
    }

    [Fact]
    public void MultipleWriteLines_AccumulatesContentWithNewLines()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine("Line 1");
        writer.WriteLine("Line 2");
        writer.WriteLine("Line 3");

        Assert.Equal($"Line 1{Environment.NewLine}Line 2{Environment.NewLine}Line 3{Environment.NewLine}", weaver.ToString());
    }

    [Fact]
    public void MixedWriteAndWriteLine_CombinesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write("Part1");
        writer.WriteLine(" Part2");
        writer.Write("Part3");

        Assert.Equal($"Part1 Part2{Environment.NewLine}Part3", weaver.ToString());
    }

    [Fact]
    public void Write_NegativeNumbers_WritesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(-42);
        writer.Write(' ');
        writer.Write(-3.14);

        Assert.Contains("-42", weaver.ToString());
        Assert.Contains("-3.14", weaver.ToString());
    }

    [Fact]
    public void Write_ZeroValues_WritesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(0);
        writer.Write(' ');
        writer.Write(0.0);
        writer.Write(' ');
        writer.Write(0m);

        Assert.Contains("0 ", weaver.ToString());
    }

    [Fact]
    public void Write_EmptyString_DoesNotChangeContent()
    {
        var weaver = new StringWeaver("Initial");
        var writer = weaver.GetTextWriter();

        writer.Write(string.Empty);

        Assert.Equal("Initial", weaver.ToString());
    }

    [Fact]
    public void Write_EmptyCharArray_DoesNotChangeContent()
    {
        var weaver = new StringWeaver("Initial");
        var writer = weaver.GetTextWriter();

        writer.Write(Array.Empty<char>());

        Assert.Equal("Initial", weaver.ToString());
    }

    [Fact]
    public void Write_EmptySpan_DoesNotChangeContent()
    {
        var weaver = new StringWeaver("Initial");
        var writer = weaver.GetTextWriter();

        writer.Write(ReadOnlySpan<char>.Empty);

        Assert.Equal("Initial", weaver.ToString());
    }

    [Fact]
    public void Write_LargeString_WritesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var largeString = new string('X', 10000);

        writer.Write(largeString);

        Assert.Equal(10000, weaver.Length);
        Assert.Equal(largeString, weaver.ToString());
    }

    [Fact]
    public void WriteLine_MultipleNewLines_AddsCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine();
        writer.WriteLine();
        writer.WriteLine();

        Assert.Equal(Environment.NewLine + Environment.NewLine + Environment.NewLine, weaver.ToString());
    }

    [Fact]
    public void Write_SpecialCharacters_WritesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write("Tab:\t");
        writer.Write("Quote:\"");
        writer.Write("Backslash:\\");

        Assert.Contains("Tab:\t", weaver.ToString());
        Assert.Contains("Quote:\"", weaver.ToString());
        Assert.Contains("Backslash:\\", weaver.ToString());
    }

    [Fact]
    public void Write_UnicodeCharacters_WritesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write("Hello 世界 🌍");

        Assert.Equal("Hello 世界 🌍", weaver.ToString());
    }

    [Fact]
    public void Write_StringBuilderWithChunks_WritesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();
        var sb = new StringBuilder();
        
        for (var i = 0; i < 1000; i++)
        {
            sb.Append("A");
        }

        writer.Write(sb);

        Assert.Equal(1000, weaver.Length);
    }

    [Fact]
    public void Write_MaxValues_WritesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(int.MaxValue);
        writer.Write(' ');
        writer.Write(long.MaxValue);
        writer.Write(' ');
        writer.Write(uint.MaxValue);
        writer.Write(' ');
        writer.Write(ulong.MaxValue);

        var result = weaver.ToString();
        Assert.Contains(int.MaxValue.ToString(), result);
        Assert.Contains(long.MaxValue.ToString(), result);
        Assert.Contains(uint.MaxValue.ToString(), result);
        Assert.Contains(ulong.MaxValue.ToString(), result);
    }

    [Fact]
    public void Write_MinValues_WritesCorrectly()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(int.MinValue);
        writer.Write(' ');
        writer.Write(long.MinValue);

        var result = weaver.ToString();
        Assert.Contains(int.MinValue.ToString(), result);
        Assert.Contains(long.MinValue.ToString(), result);
    }

    [Fact]
    public void NewLine_ReturnsEnvironmentNewLine()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        Assert.Equal(Environment.NewLine, writer.NewLine);
    }

    [Fact]
    public void Write_AfterClear_WritesToEmptyWeaver()
    {
        var weaver = new StringWeaver("Initial");
        var writer = weaver.GetTextWriter();

        weaver.Clear();
        writer.Write("New Content");

        Assert.Equal("New Content", weaver.ToString());
    }

    [Fact]
    public void WriteMultipleTimes_GrowsCapacityAsNeeded()
    {
        var weaver = new StringWeaver(10);
        var writer = weaver.GetTextWriter();

        for (var i = 0; i < 100; i++)
        {
            writer.Write("Test ");
        }

        Assert.True(weaver.Length > 10);
        Assert.Contains("Test ", weaver.ToString());
    }

    [Fact]
    public void Write_CharArrayWithZeroCount_DoesNotChangeContent()
    {
        var weaver = new StringWeaver("Initial");
        var writer = weaver.GetTextWriter();
        var buffer = new[] { 'A', 'B', 'C' };

        writer.Write(buffer, 0, 0);

        Assert.Equal("Initial", weaver.ToString());
    }

    [Fact]
    public void Write_FormattedNumbers_UsesCorrectFormat()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write(1.0 / 3.0);

        Assert.Contains("0.3333", weaver.ToString());
    }

    [Fact]
    public void WriteLine_WithFormat_WritesFormattedContent()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.WriteLine("Value: {0}", 42);

        Assert.Equal($"Value: 42{Environment.NewLine}", weaver.ToString());
    }

    [Fact]
    public void Write_WithFormat_WritesFormattedContent()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write("A: {0}, B: {1}", 1, 2);

        Assert.Equal("A: 1, B: 2", weaver.ToString());
    }

    [Fact]
    public void Write_NullObject_WritesEmptyString()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write((object)null);

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void Write_NullString_DoesNotWrite()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write((string)null);

        Assert.Equal("", weaver.ToString());
    }

    [Fact]
    public void Write_NullStringBuilder_DoesNotThrow()
    {
        var weaver = new StringWeaver();
        var writer = weaver.GetTextWriter();

        writer.Write((StringBuilder)null);

        Assert.Equal("", weaver.ToString());
    }
}
