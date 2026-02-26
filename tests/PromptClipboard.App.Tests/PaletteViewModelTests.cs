namespace PromptClipboard.App.Tests;

using PromptClipboard.App.Tests.Fakes;
using PromptClipboard.App.ViewModels;
using PromptClipboard.Application.Services;
using PromptClipboard.Domain.Entities;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.InMemory;

public class PaletteViewModelTests
{
    private readonly FakePromptRepository _repo;
    private readonly InMemorySink _sink;
    private readonly PaletteViewModel _vm;

    public PaletteViewModelTests()
    {
        _repo = new FakePromptRepository();
        _sink = new InMemorySink();
        var log = new LoggerConfiguration().WriteTo.Sink(_sink).CreateLogger();
        var searchService = new SearchRankingService(_repo);
        _vm = new PaletteViewModel(searchService, _repo, log, debounceMs: 0);
    }

    [Fact]
    public async Task Search_WhenRepoThrows_ClearsPromptsAndLogs()
    {
        // Arrange: seed one prompt, load it, then break the repo
        _repo.Prompts.Add(new Prompt { Id = 1, Title = "Test", Body = "Body" });
        await _vm.LoadPromptsAsync();
        Assert.Single(_vm.Prompts);

        _repo.ThrowOnSearch = true;

        // Act: trigger debounced search (debounceMs=0 → immediate)
        _vm.SearchText = "fail";
        // Give the fire-and-forget task time to complete
        await Task.Delay(50);

        // Assert
        Assert.Empty(_vm.Prompts);
        Assert.Equal(-1, _vm.SelectedIndex);
        Assert.Null(_vm.SelectedPrompt);
        Assert.Contains(_sink.LogEvents, e => e.Level == LogEventLevel.Error);
    }

    [Fact]
    public async Task Search_Cancellation_DoesNotThrow()
    {
        _repo.Prompts.Add(new Prompt { Id = 1, Title = "A", Body = "B" });

        // Rapid-fire search text changes — each cancels the previous
        _vm.SearchText = "x";
        _vm.SearchText = "xy";
        _vm.SearchText = "xyz";

        // Wait for the last search to settle
        await Task.Delay(100);

        // No exception thrown — test passes if we get here
        Assert.True(true);
    }

    [Fact]
    public void MoveDown_WrapAround()
    {
        _vm.Prompts.Add(new Prompt { Id = 1, Title = "A", Body = "1" });
        _vm.Prompts.Add(new Prompt { Id = 2, Title = "B", Body = "2" });
        _vm.Prompts.Add(new Prompt { Id = 3, Title = "C", Body = "3" });
        _vm.SelectedIndex = 2;
        _vm.SelectedPrompt = _vm.Prompts[2];

        _vm.MoveDownCommand.Execute(null);

        Assert.Equal(0, _vm.SelectedIndex);
        Assert.Equal("A", _vm.SelectedPrompt!.Title);
    }

    [Fact]
    public void MoveUp_WrapAround()
    {
        _vm.Prompts.Add(new Prompt { Id = 1, Title = "A", Body = "1" });
        _vm.Prompts.Add(new Prompt { Id = 2, Title = "B", Body = "2" });
        _vm.Prompts.Add(new Prompt { Id = 3, Title = "C", Body = "3" });
        _vm.SelectedIndex = 0;
        _vm.SelectedPrompt = _vm.Prompts[0];

        _vm.MoveUpCommand.Execute(null);

        Assert.Equal(2, _vm.SelectedIndex);
        Assert.Equal("C", _vm.SelectedPrompt!.Title);
    }

    [Fact]
    public void MoveDown_EmptyList_NoOp()
    {
        Assert.Empty(_vm.Prompts);
        _vm.MoveDownCommand.Execute(null);
        // No exception — test passes
    }

    [Fact]
    public void MoveUp_EmptyList_NoOp()
    {
        Assert.Empty(_vm.Prompts);
        _vm.MoveUpCommand.Execute(null);
        // No exception — test passes
    }
}
