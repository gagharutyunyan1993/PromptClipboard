using PromptClipboard.Domain.Interfaces;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace PromptClipboard.Infrastructure.Platform;

public class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/gagharutyunyan1993/PromptClipboard";

    private readonly ILogger _log;
    private UpdateManager? _manager;

    public bool IsUpdateReady { get; private set; }
    public string? PendingVersion { get; private set; }

    public VelopackUpdateService(ILogger log)
    {
        _log = log;
        try
        {
            var source = new GithubSource(RepoUrl, null, false);
            _manager = new UpdateManager(source);

            // Check if a previously downloaded update is pending restart
            var pending = _manager.UpdatePendingRestart;
            if (pending != null)
            {
                PendingVersion = pending.Version.ToString();
                IsUpdateReady = true;
                _log.Information("Pending update found from previous session: v{Version}", PendingVersion);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "UpdateManager init failed (dev mode?) — updates disabled");
        }
    }

    public async Task<string?> CheckAndDownloadAsync()
    {
        if (_manager == null) return null;
        if (IsUpdateReady) return PendingVersion;
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update == null) return null;

            PendingVersion = update.TargetFullRelease.Version.ToString();
            _log.Information("Update available: v{Version}", PendingVersion);

            await _manager.DownloadUpdatesAsync(update);
            IsUpdateReady = true;
            _log.Information("Update v{Version} downloaded and ready", PendingVersion);
            return PendingVersion;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Update check/download failed");
            return null;
        }
    }

    public void ApplyAndRestart()
    {
        if (_manager == null || !IsUpdateReady) return;
        _manager.ApplyUpdatesAndRestart(null);
    }
}
