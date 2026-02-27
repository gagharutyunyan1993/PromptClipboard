namespace PromptClipboard.App.Tests;

using PromptClipboard.App.Helpers;

public class BodyPreviewHelperTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("short text", false)]
    [InlineData("one\ntwo\nthree", false)] // 3 lines
    public void IsLongBody_ShortText_ReturnsFalse(string? body, bool expected)
    {
        Assert.Equal(expected, BodyPreviewHelper.IsLongBody(body));
    }

    [Fact]
    public void IsLongBody_220PlusChars_ReturnsTrue()
    {
        var body = new string('a', 220);
        Assert.True(BodyPreviewHelper.IsLongBody(body));
    }

    [Fact]
    public void IsLongBody_4Lines_ReturnsTrue()
    {
        var body = "line1\nline2\nline3\nline4";
        Assert.True(BodyPreviewHelper.IsLongBody(body));
    }

    [Fact]
    public void IsLongBody_4Lines_CRLF_ReturnsTrue()
    {
        var body = "line1\r\nline2\r\nline3\r\nline4";
        Assert.True(BodyPreviewHelper.IsLongBody(body));
    }

    [Fact]
    public void IsLongBody_TrailingNewline_NotCountedAsExtraLine()
    {
        // "line1\nline2\n" should be 2 lines, not 3
        var body = "line1\nline2\n";
        Assert.False(BodyPreviewHelper.IsLongBody(body)); // 2 lines < 4
    }

    [Fact]
    public void IsLongBody_TrailingCRLF_NotCountedAsExtraLine()
    {
        var body = "a\r\n";
        Assert.False(BodyPreviewHelper.IsLongBody(body)); // 1 line
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void GetCollapsedPreview_NullOrEmpty_ReturnsEmpty(string? body, string expected)
    {
        Assert.Equal(expected, BodyPreviewHelper.GetCollapsedPreview(body));
    }

    [Fact]
    public void GetCollapsedPreview_ShortFirstLine_ReturnsAsIs()
    {
        Assert.Equal("Hello World", BodyPreviewHelper.GetCollapsedPreview("Hello World\nSecond line"));
    }

    [Fact]
    public void GetCollapsedPreview_LongFirstLine_TruncatesAt80()
    {
        var longLine = new string('x', 100);
        var result = BodyPreviewHelper.GetCollapsedPreview(longLine);
        Assert.Equal(83, result.Length); // 80 + "..."
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void GetCollapsedPreview_SkipsEmptyFirstLines()
    {
        var body = "\n\n  \nActual content here";
        Assert.Equal("Actual content here", BodyPreviewHelper.GetCollapsedPreview(body));
    }

    [Fact]
    public void GetCollapsedPreview_AllEmptyLines_ReturnsEmpty()
    {
        Assert.Equal("", BodyPreviewHelper.GetCollapsedPreview("\n\n\n"));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void GetMetaLabel_NullOrEmpty_ReturnsEmpty(string? body, string expected)
    {
        Assert.Equal(expected, BodyPreviewHelper.GetMetaLabel(body));
    }

    [Fact]
    public void GetMetaLabel_ContainsCharAndLineCount()
    {
        var body = "line1\nline2\nline3";
        var label = BodyPreviewHelper.GetMetaLabel(body);
        Assert.Contains("17 chars", label);
        Assert.Contains("3 lines", label);
    }

    [Fact]
    public void GetMetaLabel_SingleLine_UsesSingular()
    {
        var label = BodyPreviewHelper.GetMetaLabel("hello");
        Assert.Contains("1 line]", label);
    }

    [Fact]
    public void GetMetaLabel_CRLF_CharsCountIsOriginalLength()
    {
        // "ab\r\ncd" has string.Length = 6, but 2 lines after normalization
        var body = "ab\r\ncd";
        var label = BodyPreviewHelper.GetMetaLabel(body);
        Assert.Contains("6 chars", label);
        Assert.Contains("2 lines", label);
    }

    [Fact]
    public void GetMetaLabel_TrailingNewline_CorrectLineCount()
    {
        var body = "line1\nline2\n";
        var label = BodyPreviewHelper.GetMetaLabel(body);
        Assert.Contains("2 lines", label);
    }

    [Fact]
    public void GetMetaLabel_TrailingCRLF_CorrectLineCount()
    {
        var body = "a\r\n";
        var label = BodyPreviewHelper.GetMetaLabel(body);
        Assert.Contains("1 line]", label);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void GetExpandedPreview_NullOrEmpty_ReturnsEmpty(string? body, string expected)
    {
        Assert.Equal(expected, BodyPreviewHelper.GetExpandedPreview(body));
    }

    [Fact]
    public void GetExpandedPreview_Under10Lines_ReturnsAll()
    {
        var body = "line1\nline2\nline3";
        var result = BodyPreviewHelper.GetExpandedPreview(body);
        Assert.Equal("line1\nline2\nline3", result);
        Assert.DoesNotContain("use Edit", result);
    }

    [Fact]
    public void GetExpandedPreview_Over10Lines_TruncatesWithHint()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"Line {i}");
        var body = string.Join("\n", lines);

        var result = BodyPreviewHelper.GetExpandedPreview(body);

        // Should contain first 10 lines
        Assert.Contains("Line 1", result);
        Assert.Contains("Line 10", result);
        Assert.DoesNotContain("Line 11", result);
        Assert.Contains("use Edit for full text", result);
    }

    [Fact]
    public void GetExpandedPreview_VeryLongLines_TruncatesByChars()
    {
        // 5 lines of 1000 chars each = 5000 chars, exceeds 3000 limit
        var lines = Enumerable.Range(1, 5).Select(_ => new string('a', 1000));
        var body = string.Join("\n", lines);

        var result = BodyPreviewHelper.GetExpandedPreview(body);
        Assert.Contains("use Edit for full text", result);
    }

    [Fact]
    public void GetExpandedPreview_Exactly3000Chars_NoFalseHint()
    {
        // Build text that is exactly 3000 chars and fits in 10 lines — no truncation should occur
        // 9 lines of 296 chars + newlines (9 * 296 + 9 newlines = 2664 + 9 = 2673), last line = 327 chars → total = 3000
        var shortLines = Enumerable.Range(1, 9).Select(_ => new string('a', 296));
        var lastLine = new string('b', 327);
        var body = string.Join("\n", shortLines.Append(lastLine));

        Assert.Equal(3000, body.Length); // Sanity check

        var result = BodyPreviewHelper.GetExpandedPreview(body);
        Assert.DoesNotContain("use Edit", result);
        Assert.Equal(body, result);
    }
}
