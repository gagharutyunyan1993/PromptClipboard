namespace PromptClipboard.App.Helpers;

public static class BodyPreviewHelper
{
    private const int LongCharThreshold = 220;
    private const int LongLineThreshold = 4;
    private const int CollapsedMaxChars = 80;
    private const int ExpandedMaxLines = 10;
    private const int ExpandedMaxChars = 3000;

    public static bool IsLongBody(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return false;

        if (body.Length >= LongCharThreshold)
            return true;

        return CountLines(body) >= LongLineThreshold;
    }

    public static string GetCollapsedPreview(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return string.Empty;

        var normalized = body.ReplaceLineEndings("\n");
        var lines = normalized.Split('\n');

        // Find first non-empty line
        var firstLine = string.Empty;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                firstLine = trimmed;
                break;
            }
        }

        if (firstLine.Length == 0)
            return string.Empty;

        if (firstLine.Length <= CollapsedMaxChars)
            return firstLine;

        return firstLine[..CollapsedMaxChars] + "...";
    }

    public static string GetMetaLabel(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return string.Empty;

        var charCount = body.Length;
        var lineCount = CountLines(body);

        return $"[{charCount} chars \u00b7 {lineCount} {(lineCount == 1 ? "line" : "lines")}]";
    }

    public static string GetExpandedPreview(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return string.Empty;

        var normalized = body.ReplaceLineEndings("\n");
        var lines = normalized.TrimEnd('\n').Split('\n');

        var totalLines = lines.Length;
        var takeLines = Math.Min(totalLines, ExpandedMaxLines);
        var result = string.Join("\n", lines.Take(takeLines));

        var charsTruncated = false;
        if (result.Length > ExpandedMaxChars)
        {
            result = result[..ExpandedMaxChars];
            charsTruncated = true;
        }

        if (takeLines < totalLines || charsTruncated)
            result += "\n... (use Edit for full text)";

        return result;
    }

    private static int CountLines(string body)
    {
        var normalized = body.ReplaceLineEndings("\n").TrimEnd('\n');
        if (normalized.Length == 0)
            return 0;
        return normalized.Split('\n').Length;
    }
}
