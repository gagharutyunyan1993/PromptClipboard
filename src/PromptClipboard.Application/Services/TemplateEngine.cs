namespace PromptClipboard.Application.Services;

using System.Text.RegularExpressions;

public sealed partial class TemplateEngine
{
    public record TemplateVariable(string Name, string? DefaultValue);

    public List<TemplateVariable> ExtractVariables(string body)
    {
        var variables = new List<TemplateVariable>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in VariablePattern().Matches(body))
        {
            var name = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : null;

            if (seen.Add(name))
                variables.Add(new TemplateVariable(name, defaultValue));
        }

        return variables;
    }

    public string Resolve(string body, Dictionary<string, string> values)
    {
        return VariablePattern().Replace(body, match =>
        {
            var name = match.Groups[1].Value;

            // Built-in macros
            if (name.Equals("date", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("yyyy-MM-dd");
            if (name.Equals("time", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("HH:mm");
            if (name.Equals("clipboard", StringComparison.OrdinalIgnoreCase))
                return values.TryGetValue("clipboard", out var cb) ? cb : match.Value;

            if (values.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
                return value;

            // Use default value if available
            if (match.Groups[2].Success)
                return match.Groups[2].Value;

            return match.Value; // Leave as-is if no value and no default
        });
    }

    public bool HasVariables(string body) => VariablePattern().IsMatch(body);

    [GeneratedRegex(@"\{\{(\w+)(?:\|default=([^}]*))?\}\}")]
    private static partial Regex VariablePattern();
}
