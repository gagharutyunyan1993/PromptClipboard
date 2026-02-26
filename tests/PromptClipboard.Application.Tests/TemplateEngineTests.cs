namespace PromptClipboard.Application.Tests;

using PromptClipboard.Application.Services;

public class TemplateEngineTests
{
    private readonly TemplateEngine _engine = new();

    [Fact]
    public void ExtractVariables_FindsSimpleVariable()
    {
        var vars = _engine.ExtractVariables("Hello {{name}}!");
        Assert.Single(vars);
        Assert.Equal("name", vars[0].Name);
        Assert.Null(vars[0].DefaultValue);
    }

    [Fact]
    public void ExtractVariables_FindsVariableWithDefault()
    {
        var vars = _engine.ExtractVariables("Tone: {{tone|default=neutral}}");
        Assert.Single(vars);
        Assert.Equal("tone", vars[0].Name);
        Assert.Equal("neutral", vars[0].DefaultValue);
    }

    [Fact]
    public void ExtractVariables_DeduplicatesByName()
    {
        var vars = _engine.ExtractVariables("{{name}} and {{name}} again");
        Assert.Single(vars);
    }

    [Fact]
    public void ExtractVariables_FindsMultipleVariables()
    {
        var vars = _engine.ExtractVariables("{{topic}} with {{tone|default=neutral}} for {{audience}}");
        Assert.Equal(3, vars.Count);
    }

    [Fact]
    public void Resolve_ReplacesWithValues()
    {
        var result = _engine.Resolve("Hello {{name}}!", new Dictionary<string, string> { ["name"] = "World" });
        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void Resolve_UsesDefaultWhenNoValue()
    {
        var result = _engine.Resolve("Tone: {{tone|default=neutral}}", new Dictionary<string, string>());
        Assert.Equal("Tone: neutral", result);
    }

    [Fact]
    public void Resolve_LeavesUnresolvedVariable()
    {
        var result = _engine.Resolve("Hello {{unknown}}!", new Dictionary<string, string>());
        Assert.Equal("Hello {{unknown}}!", result);
    }

    [Fact]
    public void Resolve_DateMacro()
    {
        var result = _engine.Resolve("Today: {{date}}", new Dictionary<string, string>());
        var expected = DateTime.Now.ToString("yyyy-MM-dd");
        Assert.Equal($"Today: {expected}", result);
    }

    [Fact]
    public void Resolve_TimeMacro()
    {
        var result = _engine.Resolve("Now: {{time}}", new Dictionary<string, string>());
        Assert.Contains(":", result); // HH:mm format
    }

    [Fact]
    public void HasVariables_TrueForTemplates()
    {
        Assert.True(_engine.HasVariables("Hello {{name}}"));
    }

    [Fact]
    public void HasVariables_FalseForPlainText()
    {
        Assert.False(_engine.HasVariables("Hello world"));
    }
}
