namespace PromptClipboard.Infrastructure.Tests;

using Microsoft.Data.Sqlite;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Infrastructure.Persistence;
using Serilog;

public class SqlitePromptRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqlitePromptRepository _repo;

    public SqlitePromptRepositoryTests()
    {
        // Use a named shared-cache in-memory database so that multiple connections
        // (created by the factory) all see the same data. This sentinel connection
        // keeps the database alive for the lifetime of the test.
        var dbName = $"TestDb_{Guid.NewGuid():N}";
        _connection = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();

        // Enable foreign keys
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();

        var factory = new SqliteConnectionFactory(_connection);
        var log = new LoggerConfiguration().CreateLogger();
        var runner = new MigrationRunner(factory, log);
        runner.RunAll();

        _repo = new SqlitePromptRepository(factory);
    }

    [Fact]
    public async Task CreateAndGetById_Works()
    {
        var prompt = new Prompt { Title = "Test", Body = "Body text" };
        prompt.SetTags(new[] { "tag1", "tag2" });

        var id = await _repo.CreateAsync(prompt);
        Assert.True(id > 0);

        var loaded = await _repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal("Test", loaded.Title);
        Assert.Equal("Body text", loaded.Body);
        Assert.Contains("tag1", loaded.GetTags());
    }

    [Fact]
    public async Task Search_FTS5Works()
    {
        var p1 = new Prompt { Title = "Email template", Body = "Professional email" };
        p1.SetTags(new[] { "email" });
        await _repo.CreateAsync(p1);

        var p2 = new Prompt { Title = "Jira ticket", Body = "Bug report" };
        p2.SetTags(new[] { "jira" });
        await _repo.CreateAsync(p2);

        var results = await _repo.SearchAsync("email");
        Assert.Single(results);
        Assert.Equal("Email template", results[0].Title);
    }

    [Fact]
    public async Task Search_TagFilterWorks()
    {
        var p1 = new Prompt { Title = "Test 1", Body = "Body 1" };
        p1.SetTags(new[] { "email", "work" });
        await _repo.CreateAsync(p1);

        var p2 = new Prompt { Title = "Test 2", Body = "Body 2" };
        p2.SetTags(new[] { "jira" });
        await _repo.CreateAsync(p2);

        var results = await _repo.SearchAsync("", tagFilter: "email");
        Assert.Single(results);
        Assert.Equal("Test 1", results[0].Title);
    }

    [Fact]
    public async Task Search_TagFilterIsCaseInsensitive()
    {
        var p1 = new Prompt { Title = "Test", Body = "Body" };
        p1.SetTags(new[] { "email" }); // stored as lowercase
        await _repo.CreateAsync(p1);

        var results = await _repo.SearchAsync("", tagFilter: "Email");
        Assert.Single(results);
    }

    [Fact]
    public async Task GetPinned_ReturnsPinnedOnly()
    {
        var p1 = new Prompt { Title = "Pinned", Body = "Body", IsPinned = true };
        await _repo.CreateAsync(p1);

        var p2 = new Prompt { Title = "Not pinned", Body = "Body", IsPinned = false };
        await _repo.CreateAsync(p2);

        var results = await _repo.GetPinnedAsync();
        Assert.Single(results);
        Assert.Equal("Pinned", results[0].Title);
    }

    [Fact]
    public async Task MarkUsed_IncrementsCountAndSetsDate()
    {
        var p = new Prompt { Title = "Test", Body = "Body" };
        var id = await _repo.CreateAsync(p);

        var now = DateTime.UtcNow;
        await _repo.MarkUsedAsync(id, now);

        var loaded = await _repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.UseCount);
        Assert.NotNull(loaded.LastUsedAt);
    }

    [Fact]
    public async Task Update_ModifiesPrompt()
    {
        var p = new Prompt { Title = "Original", Body = "Body" };
        var id = await _repo.CreateAsync(p);

        var loaded = await _repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        loaded.Title = "Updated";
        loaded.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(loaded);

        var updated = await _repo.GetByIdAsync(id);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Title);
    }

    [Fact]
    public async Task Delete_RemovesPrompt()
    {
        var p = new Prompt { Title = "ToDelete", Body = "Body" };
        var id = await _repo.CreateAsync(p);

        await _repo.DeleteAsync(id);

        var loaded = await _repo.GetByIdAsync(id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetCount_ReturnsCorrectCount()
    {
        Assert.Equal(0, await _repo.GetCountAsync());

        await _repo.CreateAsync(new Prompt { Title = "A", Body = "B" });
        await _repo.CreateAsync(new Prompt { Title = "C", Body = "D" });

        Assert.Equal(2, await _repo.GetCountAsync());
    }

    [Fact]
    public async Task GetRecent_ReturnsRecentlyUsed()
    {
        var p1 = new Prompt { Title = "Old", Body = "B" };
        var id1 = await _repo.CreateAsync(p1);
        await _repo.MarkUsedAsync(id1, DateTime.UtcNow.AddDays(-2));

        var p2 = new Prompt { Title = "New", Body = "B" };
        var id2 = await _repo.CreateAsync(p2);
        await _repo.MarkUsedAsync(id2, DateTime.UtcNow);

        var results = await _repo.GetRecentAsync(10);
        Assert.Equal(2, results.Count);
        Assert.Equal("New", results[0].Title); // Most recent first
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
