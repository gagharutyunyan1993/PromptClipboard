namespace PromptClipboard.Application.UseCases;

using PromptClipboard.Domain.Interfaces;
using Serilog;

public sealed class PastePromptUseCase
{
    private readonly IPromptRepository _repository;
    private readonly IClipboardService _clipboardService;
    private readonly IInputSimulator _inputSimulator;
    private readonly IFocusTracker _focusTracker;
    private readonly IFocusRestoreService _focusRestoreService;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _pasteGate = new(1, 1);

    public event Action<string>? PasteFailed;
    public event Action? PasteSucceeded;

    // Configurable delays
    public int PrePasteDelayMs { get; set; } = 50;
    public int PostPasteDelayMs { get; set; } = 150;

    public PastePromptUseCase(
        IPromptRepository repository,
        IClipboardService clipboardService,
        IInputSimulator inputSimulator,
        IFocusTracker focusTracker,
        IFocusRestoreService focusRestoreService,
        ILogger log)
    {
        _repository = repository;
        _clipboardService = clipboardService;
        _inputSimulator = inputSimulator;
        _focusTracker = focusTracker;
        _focusRestoreService = focusRestoreService;
        _log = log;
    }

    public async Task ExecuteAsync(long promptId, string resolvedText, Action hidePalette, Func<IntPtr> getForegroundWindow, Func<IntPtr, bool> isWindow, Func<IntPtr, int?> getIntegrityLevel, int currentProcessIL)
    {
        await _pasteGate.WaitAsync();
        try
        {
            var savedHwnd = _focusTracker.SavedHwnd;
            hidePalette();

            if (savedHwnd == IntPtr.Zero)
            {
                AbortPaste("No target window");
                return;
            }
            if (!isWindow(savedHwnd))
            {
                AbortPaste("Target window closed");
                return;
            }

            var targetIL = getIntegrityLevel(savedHwnd);
            if (targetIL == null)
            {
                AbortPaste("Failed to check target window privileges");
                return;
            }
            if (targetIL > currentProcessIL)
            {
                AbortPaste("Target window runs with elevated privileges");
                return;
            }

            if (!_focusRestoreService.RestoreFocus(savedHwnd))
            {
                AbortPaste("Failed to restore focus");
                return;
            }

            // Give the target window time to settle after receiving focus
            await Task.Delay(150);

            // Simple flow like Windows clipboard (Win+V):
            // 1. Set text on clipboard
            // 2. Send Ctrl+V
            // No save/restore — just overwrite clipboard
            var operationId = Guid.NewGuid();
            _clipboardService.SetTextWithMarker(resolvedText, operationId);
            _log.Debug("Clipboard text set, waiting {Delay}ms before Ctrl+V", PrePasteDelayMs);

            await Task.Delay(PrePasteDelayMs);

            if (getForegroundWindow() != savedHwnd)
            {
                AbortPaste("Focus lost before paste");
                return;
            }

            var sent = _inputSimulator.SimulateCtrlV();
            if (sent != 4)
            {
                AbortPaste($"SendInput sent {sent}/4 events");
                return;
            }

            await Task.Delay(PostPasteDelayMs);

            await _repository.MarkUsedAsync(promptId, DateTime.UtcNow);
            PasteSucceeded?.Invoke();
            _log.Information("Prompt {PromptId} pasted successfully", promptId);
        }
        finally
        {
            _pasteGate.Release();
        }
    }

    private void AbortPaste(string reason)
    {
        _log.Warning("Paste aborted: {Reason}", reason);
        PasteFailed?.Invoke(reason);
    }
}
