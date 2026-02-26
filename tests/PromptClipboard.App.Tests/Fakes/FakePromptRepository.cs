namespace PromptClipboard.App.Tests.Fakes;

using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;

internal class FakePromptRepository : IPromptRepository
{
    public List<Prompt> Prompts { get; set; } = [];
    public bool ThrowOnSearch { get; set; }

    public Task<List<Prompt>> SearchAsync(string query, string? tagFilter = null, string? langFilter = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (ThrowOnSearch) throw new InvalidOperationException("Simulated FTS5 error");
        var results = Prompts.Where(p => string.IsNullOrEmpty(query) || p.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Body.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrEmpty(tagFilter))
            results = results.Where(p => p.TagsText.Contains(tagFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult(results);
    }

    public Task<List<Prompt>> GetPinnedAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (ThrowOnSearch) throw new InvalidOperationException("Simulated error");
        return Task.FromResult(Prompts.Where(p => p.IsPinned).ToList());
    }

    public Task<List<Prompt>> GetRecentAsync(int limit = 10, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (ThrowOnSearch) throw new InvalidOperationException("Simulated error");
        return Task.FromResult(Prompts.OrderByDescending(p => p.LastUsedAt ?? p.CreatedAt).Take(limit).ToList());
    }

    public Task<Prompt?> GetByIdAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(Prompts.FirstOrDefault(p => p.Id == id));

    public Task<long> CreateAsync(Prompt prompt, CancellationToken ct = default)
    {
        prompt.Id = Prompts.Count + 1;
        Prompts.Add(prompt);
        return Task.FromResult(prompt.Id);
    }

    public Task UpdateAsync(Prompt prompt, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(long id, CancellationToken ct = default)
    {
        Prompts.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }

    public Task MarkUsedAsync(long id, DateTime usedAt, CancellationToken ct = default) => Task.CompletedTask;
    public Task<List<Prompt>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Prompts.ToList());
    public Task<int> GetCountAsync(CancellationToken ct = default) => Task.FromResult(Prompts.Count);
}
