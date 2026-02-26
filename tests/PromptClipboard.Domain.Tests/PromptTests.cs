namespace PromptClipboard.Domain.Tests;

using PromptClipboard.Domain.Entities;

public class PromptTests
{
    [Fact]
    public void SetTags_NormalizesToLowercase()
    {
        var prompt = new Prompt();
        prompt.SetTags(new[] { "Email", "JIRA", "work" });

        var tags = prompt.GetTags();
        Assert.Equal(3, tags.Count);
        Assert.Contains("email", tags);
        Assert.Contains("jira", tags);
        Assert.Contains("work", tags);
    }

    [Fact]
    public void SetTags_RemovesDuplicates()
    {
        var prompt = new Prompt();
        prompt.SetTags(new[] { "email", "Email", "EMAIL" });

        var tags = prompt.GetTags();
        Assert.Single(tags);
        Assert.Equal("email", tags[0]);
    }

    [Fact]
    public void SetTags_TrimsAndFiltersEmpty()
    {
        var prompt = new Prompt();
        prompt.SetTags(new[] { " email ", "", "  ", "work" });

        var tags = prompt.GetTags();
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void SetTags_UpdatesTagsText()
    {
        var prompt = new Prompt();
        prompt.SetTags(new[] { "email", "work" });
        Assert.Equal("email work", prompt.TagsText);
    }

    [Fact]
    public void HasTemplateVariables_DetectsTemplates()
    {
        var prompt = new Prompt { Body = "Hello {{name}}" };
        Assert.True(prompt.HasTemplateVariables());
    }

    [Fact]
    public void HasTemplateVariables_FalseForPlainText()
    {
        var prompt = new Prompt { Body = "Hello world" };
        Assert.False(prompt.HasTemplateVariables());
    }

    [Fact]
    public void GetTags_ReturnsEmptyListForInvalidJson()
    {
        var prompt = new Prompt { TagsJson = "not json" };
        var tags = prompt.GetTags();
        Assert.Empty(tags);
    }
}
