namespace PromptClipboard.Domain.Interfaces;

public class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+Q";
    public uint HotkeyModifiers { get; set; } = 0x0002 | 0x0004; // CTRL + SHIFT
    public uint HotkeyVk { get; set; } = 0x51; // Q
    public string DbPath { get; set; } = string.Empty;
    public bool AutoStart { get; set; }
    public int PasteDelayMs { get; set; } = 50;
    public int RestoreDelayMs { get; set; } = 150;
    public DateTime? LastUpdateCheckUtc { get; set; }
}

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
