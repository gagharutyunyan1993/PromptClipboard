namespace PromptClipboard.App.Handlers;

using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;
using Serilog;

internal static class PromptSeeder
{
    public static async Task SeedIfEmptyAsync(IPromptRepository repo, ILogger? log)
    {
        var count = await repo.GetCountAsync();
        if (count > 0) return;

        log?.Information("Seeding initial prompts...");

        var seeds = new List<Prompt>
        {
            CreateSeed("Email: Professional reply",
                "Write a professional email reply about \"{{topic}}\". Tone: {{tone|default=polite and professional}}. Target audience: {{audience|default=colleagues}}.",
                ["email", "work"], isPinned: true),
            CreateSeed("Jira: Task description",
                "**Task:** {{task_name}} **Description:** {{description}} **Acceptance criteria:** - {{criteria_1}} - {{criteria_2}} - {{criteria_3}} **Technical details:** {{tech_details}}",
                ["jira", "work", "tasks"], isPinned: true),
            CreateSeed("Code: Review comment",
                "Improvement suggestion: {{suggestion}} Reason: {{reason}} Example: ```{{example}}```",
                ["code", "review"]),
            CreateSeed("Code analysis",
                "Analyze the following code and provide: 1. Potential issues 2. Optimization opportunities 3. Readability improvements Code: {{code}}",
                ["code", "analysis"]),
            CreateSeed("Text translation",
                "Translate the following text to {{target_lang|default=english}}, preserving the original style and tone:\n\n{{text}}",
                ["translation", "text"]),
        };

        foreach (var seed in seeds)
            await repo.CreateAsync(seed);

        log?.Information("Seeded {Count} prompts", seeds.Count);
    }

    private static Prompt CreateSeed(string title, string body, List<string> tags, bool isPinned = false)
    {
        var p = new Prompt
        {
            Title = title,
            Body = body,
            IsPinned = isPinned,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        p.SetTags(tags);
        return p;
    }
}
