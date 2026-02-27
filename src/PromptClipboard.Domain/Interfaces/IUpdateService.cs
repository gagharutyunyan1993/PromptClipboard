namespace PromptClipboard.Domain.Interfaces;

public interface IUpdateService
{
    /// <summary>Check for updates and download if available. Returns version string or null.</summary>
    Task<string?> CheckAndDownloadAsync();

    /// <summary>True if an update has been downloaded and is pending restart.</summary>
    bool IsUpdateReady { get; }

    /// <summary>Version string of the pending update, or null.</summary>
    string? PendingVersion { get; }

    /// <summary>Apply the downloaded update and restart the app.</summary>
    void ApplyAndRestart();
}
