namespace PromptClipboard.Domain.Entities;

public sealed class Prompt
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public string TagsText { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public int UseCount { get; set; }
    public bool IsPinned { get; set; }
    public string Lang { get; set; } = string.Empty;
    public string ModelHint { get; set; } = string.Empty;
    public long? VersionParentId { get; set; }

    public List<string> GetTags()
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(TagsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SetTags(IEnumerable<string> tags)
    {
        var normalized = tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
        TagsJson = System.Text.Json.JsonSerializer.Serialize(normalized);
        TagsText = string.Join(" ", normalized);
    }

    public bool HasTemplateVariables() => Body.Contains("{{");
}
