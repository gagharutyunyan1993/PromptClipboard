using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using PromptClipboard.App.ViewModels;
using PromptClipboard.App.Views;
using PromptClipboard.Application.Services;
using PromptClipboard.Application.UseCases;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;
using PromptClipboard.Infrastructure.Platform;

namespace PromptClipboard.App;

public partial class App
{
    /// <summary>
    /// Returns resolved text, or null if the user cancelled the template dialog.
    /// </summary>
    private string? ResolveTemplate(Prompt prompt)
    {
        if (_services == null || _paletteWindow == null) return null;

        var templateEngine = _services.GetRequiredService<TemplateEngine>();
        if (!templateEngine.HasVariables(prompt.Body))
            return prompt.Body;

        _log?.Debug("Prompt has template variables, showing dialog");
        var variables = templateEngine.ExtractVariables(prompt.Body);
        var dialogVm = new TemplateDialogViewModel();
        dialogVm.LoadVariables(variables);

        var dialog = new TemplateDialog();
        dialog.Initialize(dialogVm);

        _paletteWindow.SuppressDeactivate(true);
        try
        {
            dialog.Owner = _paletteWindow;
            if (dialog.ShowDialog() != true)
            {
                _log?.Debug("Template dialog cancelled");
                return null;
            }
        }
        finally
        {
            _paletteWindow.SuppressDeactivate(false);
        }

        return templateEngine.Resolve(prompt.Body, dialogVm.GetValues());
    }

    private async void OnPasteRequested(Prompt prompt)
    {
        _log?.Information("OnPasteRequested: prompt={Id} '{Title}'", prompt.Id, prompt.Title);
        if (_services == null || _paletteWindow == null) return;

        try
        {
            var resolvedText = ResolveTemplate(prompt);
            if (resolvedText == null) return;

            _paletteWindow.HideWindow();

            var pasteUseCase = _services.GetRequiredService<PastePromptUseCase>();
            var settings = _services.GetRequiredService<ISettingsService>().Load();
            pasteUseCase.PrePasteDelayMs = settings.PasteDelayMs;
            pasteUseCase.PostPasteDelayMs = settings.RestoreDelayMs;

            var ilChecker = _services.GetRequiredService<IntegrityLevelChecker>();
            var currentIL = ilChecker.GetCurrentProcessIntegrityLevel();

            var focusTracker = _services.GetRequiredService<IFocusTracker>();
            var vm = _paletteWindow.ViewModel;
            pasteUseCase.PasteFailed += OnPasteFailed;
            vm.IsPasting = true;
            try
            {
                await pasteUseCase.ExecuteAsync(
                    prompt.Id,
                    resolvedText,
                    () => _paletteWindow?.HideWindow(),
                    () => focusTracker.GetCurrentForegroundWindow(),
                    hwnd => focusTracker.IsWindowValid(hwnd),
                    hwnd => ilChecker.GetProcessIntegrityLevel(hwnd),
                    currentIL);
                _log?.Information("Paste completed for prompt {Id}", prompt.Id);
            }
            finally
            {
                vm.IsPasting = false;
                pasteUseCase.PasteFailed -= OnPasteFailed;
            }
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnPasteRequested failed for prompt {Id}", prompt.Id);
        }
    }

    private async void OnPasteAsTextRequested(Prompt prompt)
    {
        _log?.Information("OnPasteAsTextRequested: prompt={Id} '{Title}'", prompt.Id, prompt.Title);
        if (_services == null || _paletteWindow == null) return;

        try
        {
            var resolvedText = ResolveTemplate(prompt);
            if (resolvedText == null) return;

            _paletteWindow.HideWindow();

            var clipService = _services.GetRequiredService<IClipboardService>();
            clipService.SetTextWithMarker(resolvedText, Guid.NewGuid());
            _log?.Information("PasteAsText: text copied to clipboard for prompt {Id}", prompt.Id);
            _trayIcon?.ShowBalloonTip("Prompt Clipboard", "Text copied to clipboard", BalloonIcon.Info);

            var repo = _services.GetRequiredService<IPromptRepository>();
            await repo.MarkUsedAsync(prompt.Id, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnPasteAsTextRequested failed for prompt {Id}", prompt.Id);
        }
    }

    private void OnPasteFailed(string reason)
    {
        _log?.Warning("Paste failed: {Reason}", reason);
        Dispatcher.Invoke(() =>
        {
            _trayIcon?.ShowBalloonTip("Prompt Clipboard", reason, BalloonIcon.Warning);
        });
    }
}
