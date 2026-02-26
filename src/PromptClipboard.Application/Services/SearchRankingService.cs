namespace PromptClipboard.Application.Services;

using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;
using System.Text.RegularExpressions;

public sealed partial class SearchRankingService
{
    private readonly IPromptRepository _repository;

    public SearchRankingService(IPromptRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Prompt>> SearchAsync(string rawQuery, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return await GetDefaultListAsync(ct);
        }

        var (query, tagFilter, langFilter) = ParseQuery(rawQuery);

        if (string.IsNullOrWhiteSpace(query) && tagFilter != null)
        {
            return await _repository.SearchAsync("", tagFilter, langFilter, ct);
        }

        return await _repository.SearchAsync(query, tagFilter, langFilter, ct);
    }

    private async Task<List<Prompt>> GetDefaultListAsync(CancellationToken ct)
    {
        var pinnedTask = _repository.GetPinnedAsync(ct);
        var recentTask = _repository.GetRecentAsync(20, ct);
        await Task.WhenAll(pinnedTask, recentTask);

        var pinned = pinnedTask.Result;
        var recent = recentTask.Result;

        var result = new List<Prompt>(pinned);
        var ids = new HashSet<long>(pinned.Select(p => p.Id));

        foreach (var p in recent)
        {
            if (ids.Add(p.Id))
                result.Add(p);
        }

        return result;
    }

    internal static (string Query, string? TagFilter, string? LangFilter) ParseQuery(string rawQuery)
    {
        string? tagFilter = null;
        string? langFilter = null;

        var remaining = rawQuery;

        var tagMatch = TagPattern().Match(remaining);
        if (tagMatch.Success)
        {
            tagFilter = tagMatch.Groups[1].Value.ToLowerInvariant();
            remaining = remaining.Remove(tagMatch.Index, tagMatch.Length).Trim();
        }

        var langMatch = LangPattern().Match(remaining);
        if (langMatch.Success)
        {
            langFilter = langMatch.Groups[1].Value;
            remaining = remaining.Remove(langMatch.Index, langMatch.Length).Trim();
        }

        return (remaining.Trim(), tagFilter, langFilter);
    }

    [GeneratedRegex(@"#(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"lang:(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex LangPattern();
}
