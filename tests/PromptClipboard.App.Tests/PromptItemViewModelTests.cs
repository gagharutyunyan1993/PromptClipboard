namespace PromptClipboard.App.Tests;

using PromptClipboard.App.ViewModels;
using PromptClipboard.Domain.Entities;

public class PromptItemViewModelTests
{
    [Fact]
    public void ShortBody_IsLongBody_ReturnsFalse()
    {
        var vm = new PromptItemViewModel(new Prompt { Body = "short" });
        Assert.False(vm.IsLongBody);
    }

    [Fact]
    public void ShortBody_PreviewText_ReturnsFullBody()
    {
        var vm = new PromptItemViewModel(new Prompt { Body = "short text" });
        Assert.Equal("short text", vm.PreviewText);
    }

    [Fact]
    public void LongBody_IsLongBody_ReturnsTrue()
    {
        var body = new string('x', 250);
        var vm = new PromptItemViewModel(new Prompt { Body = body });
        Assert.True(vm.IsLongBody);
    }

    [Fact]
    public void LongBody_Collapsed_ShowsCondensedPreview()
    {
        var body = new string('x', 250);
        var vm = new PromptItemViewModel(new Prompt { Body = body });

        // Collapsed by default
        Assert.False(vm.IsExpanded);
        Assert.NotEqual(body, vm.PreviewText);
        Assert.True(vm.PreviewText.Length < body.Length);
    }

    [Fact]
    public void LongBody_Collapsed_MetaLabelContainsCharsAndLines()
    {
        var body = "line1\nline2\nline3\nline4\nline5";
        var vm = new PromptItemViewModel(new Prompt { Body = body });

        Assert.Contains("chars", vm.MetaLabel);
        Assert.Contains("lines", vm.MetaLabel);
    }

    [Fact]
    public void LongBody_Expanded_ShowsExpandedPreview()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"Line {i}");
        var body = string.Join("\n", lines);
        var vm = new PromptItemViewModel(new Prompt { Body = body });

        vm.ToggleExpandedCommand.Execute(null);

        Assert.True(vm.IsExpanded);
        Assert.Contains("Line 1", vm.PreviewText);
        Assert.Contains("Line 10", vm.PreviewText);
    }

    [Fact]
    public void ToggleExpandedCommand_NotifiesProperties()
    {
        var body = new string('x', 250);
        var vm = new PromptItemViewModel(new Prompt { Body = body });

        var changedProps = new List<string>();
        vm.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        vm.ToggleExpandedCommand.Execute(null);

        Assert.Contains(nameof(PromptItemViewModel.IsExpanded), changedProps);
        Assert.Contains(nameof(PromptItemViewModel.PreviewText), changedProps);
        Assert.Contains(nameof(PromptItemViewModel.MetaLabel), changedProps);
        Assert.Contains(nameof(PromptItemViewModel.ToggleLabel), changedProps);
    }

    [Fact]
    public void ToggleLabel_Collapsed_ShowMore()
    {
        var vm = new PromptItemViewModel(new Prompt { Body = new string('x', 250) });
        Assert.Equal("Show more", vm.ToggleLabel);
    }

    [Fact]
    public void ToggleLabel_Expanded_ShowLess()
    {
        var vm = new PromptItemViewModel(new Prompt { Body = new string('x', 250) });
        vm.ToggleExpandedCommand.Execute(null);
        Assert.Equal("Show less", vm.ToggleLabel);
    }

    [Fact]
    public void Prompt_Property_ReturnsOriginalPrompt()
    {
        var prompt = new Prompt { Id = 42, Title = "Test", Body = "Body" };
        var vm = new PromptItemViewModel(prompt);
        Assert.Same(prompt, vm.Prompt);
    }
}
