namespace PromptClipboard.Application.UseCases;

using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class ImportExportUseCase
{
    private readonly IPromptRepository _repository;

    public ImportExportUseCase(IPromptRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> ExportAsync(CancellationToken ct = default)
    {
        var prompts = await _repository.GetAllAsync(ct);
        var export = new ExportData
        {
            SchemaVersion = "1.0",
            ExportedAt = DateTime.UtcNow,
            Prompts = prompts.Select(p => new ExportPrompt
            {
                Title = p.Title,
                Body = p.Body,
                Tags = p.GetTags(),
                Folder = p.Folder,
                IsPinned = p.IsPinned,
                Lang = p.Lang,
                ModelHint = p.ModelHint
            }).ToList()
        };

        return JsonSerializer.Serialize(export, ExportJsonContext.Default.ExportData);
    }

    public async Task<int> ImportAsync(string json, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize(json, ExportJsonContext.Default.ExportData)
            ?? throw new InvalidOperationException("Invalid import data");

        var count = 0;
        foreach (var ep in data.Prompts)
        {
            var prompt = new Prompt
            {
                Title = ep.Title,
                Body = ep.Body,
                Folder = ep.Folder,
                IsPinned = ep.IsPinned,
                Lang = ep.Lang,
                ModelHint = ep.ModelHint
            };
            prompt.SetTags(ep.Tags);
            await _repository.CreateAsync(prompt, ct);
            count++;
        }

        return count;
    }
}

public sealed class ExportData
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; }
    public List<ExportPrompt> Prompts { get; set; } = [];
}

public sealed class ExportPrompt
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Folder { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public string Lang { get; set; } = string.Empty;
    public string ModelHint { get; set; } = string.Empty;
}

[JsonSerializable(typeof(ExportData))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class ExportJsonContext : JsonSerializerContext;
