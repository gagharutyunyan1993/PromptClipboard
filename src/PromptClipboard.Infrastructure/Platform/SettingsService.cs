namespace PromptClipboard.Infrastructure.Platform;

using System.IO;
using PromptClipboard.Domain.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly ILogger _log;

    public SettingsService(string appDataPath, ILogger log)
    {
        _settingsPath = Path.Combine(appDataPath, "settings.json");
        _log = log;
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to load settings, using defaults");
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
            File.WriteAllText(_settingsPath, json);
            _log.Information("Settings saved");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to save settings");
        }
    }
}

[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class SettingsJsonContext : JsonSerializerContext;
