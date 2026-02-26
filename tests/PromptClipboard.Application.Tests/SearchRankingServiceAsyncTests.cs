namespace PromptClipboard.Application.Tests;

using Microsoft.Data.Sqlite;
using PromptClipboard.Application.Services;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Infrastructure.Persistence;
using Serilog;

public class SearchRankingServiceAsyncTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqlitePromptRepository _repo;
    private readonly SearchRankingService _service;

    public SearchRankingServiceAsyncTests()
    {
        var dbName = $"TestDb_{Guid.NewGuid():N}";
        _connection = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();

        var factory = new SqliteConnectionFactory(_connection);
        var log = new LoggerConfiguration().CreateLogger();
        var runner = new MigrationRunner(factory, log);
        runner.RunAll();

        _repo = new SqlitePromptRepository(factory);
        _service = new SearchRankingService(_repo);
    }

    [Fact]
    public async Task EmptyQuery_ReturnsPinnedThenRecent()
    {
        var pinned = new Prompt { Title = "Pinned", Body = "B", IsPinned = true };
        pinned.SetTags(["test"]);
        await _repo.CreateAsync(pinned);

        var recent = new Prompt { Title = "Recent", Body = "B", IsPinned = false };
        recent.SetTags(["test"]);
        var recentId = await _repo.CreateAsync(recent);
        await _repo.MarkUsedAsync(recentId, DateTime.UtcNow);

        var results = await _service.SearchAsync("");

        Assert.True(results.Count >= 2);
        Assert.Equal("Pinned", results[0].Title);
    }

    [Fact]
    public async Task TagOnlyQuery_FiltersCorrectly()
    {
        var p1 = new Prompt { Title = "Email prompt", Body = "body" };
        p1.SetTags(["email"]);
        await _repo.CreateAsync(p1);

        var p2 = new Prompt { Title = "Jira prompt", Body = "body" };
        p2.SetTags(["jira"]);
        await _repo.CreateAsync(p2);

        var results = await _service.SearchAsync("#email");

        Assert.Single(results);
        Assert.Equal("Email prompt", results[0].Title);
    }

    [Fact]
    public async Task FtsQuery_ReturnsMatch()
    {
        var p1 = new Prompt { Title = "Professional email", Body = "Write a professional email" };
        p1.SetTags(["email"]);
        await _repo.CreateAsync(p1);

        var p2 = new Prompt { Title = "Code review", Body = "Review the code" };
        p2.SetTags(["code"]);
        await _repo.CreateAsync(p2);

        var results = await _service.SearchAsync("professional");

        Assert.Single(results);
        Assert.Equal("Professional email", results[0].Title);
    }

    [Fact]
    public async Task CancelledToken_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.SearchAsync("test", cts.Token));
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
