namespace PromptClipboard.Domain.Entities;

public sealed class PromptVersion
{
    public long Id { get; set; }
    public long PromptId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Note { get; set; } = string.Empty;
}
