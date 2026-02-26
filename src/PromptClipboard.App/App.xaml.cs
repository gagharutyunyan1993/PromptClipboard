using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using PromptClipboard.App.ViewModels;
using PromptClipboard.App.Views;
using PromptClipboard.Application.Services;
using PromptClipboard.Application.UseCases;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;
using PromptClipboard.Infrastructure.Persistence;
using PromptClipboard.Infrastructure.Platform;
using Serilog;

namespace PromptClipboard.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private TaskbarIcon? _trayIcon;
    private PaletteWindow? _paletteWindow;
    private Win32HotkeyService? _hotkeyService;
    private ILogger? _log;
    private Mutex? _singleInstanceMutex;

    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PromptClipboard");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers — must be first
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Single instance check
        _singleInstanceMutex = new Mutex(true, "Global\\PromptClipboard_SingleInstance", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("Prompt Clipboard уже запущен.", "Prompt Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Directory.CreateDirectory(AppDataPath);

        // Logging
        var logPath = Path.Combine(AppDataPath, "logs", "promptclipboard-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
        _log = Log.Logger;
        _log.Information("=== Prompt Clipboard starting ===");

        try
        {
            // DI
            var services = new ServiceCollection();
            ConfigureServices(services);
            _services = services.BuildServiceProvider();

            // Database
            var migrationRunner = _services.GetRequiredService<MigrationRunner>();
            migrationRunner.RunAll();

            // Seed data
            await SeedDataAsync();

            // Palette window
            _paletteWindow = new PaletteWindow();
            _paletteWindow.Width = Win32WindowPositioner.DefaultPaletteWidth;
            _paletteWindow.Height = Win32WindowPositioner.DefaultPaletteHeight;

            var paletteVm = _services.GetRequiredService<PaletteViewModel>();
            _paletteWindow.DataContext = paletteVm;

            // Wire events
            paletteVm.PasteRequested += OnPasteRequested;
            paletteVm.PasteAsTextRequested += OnPasteAsTextRequested;
            paletteVm.EditRequested += OnEditRequested;
            paletteVm.CreateRequested += OnCreateRequested;
            paletteVm.CloseRequested += () =>
            {
                _log?.Debug("CloseRequested fired");
                _paletteWindow.HideWindow();
            };
            paletteVm.CopyRequested += OnCopyRequested;
            paletteVm.PinToggleRequested += OnPinToggleRequested;
            paletteVm.DeleteRequested += OnDeleteRequested;

            // Hotkey
            _hotkeyService = _services.GetRequiredService<Win32HotkeyService>();
            var hotkeyHwndSource = new HwndSource(new HwndSourceParameters("PromptClipboardHotkeyWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = -100,
                PositionY = -100,
                WindowStyle = 0
            });
            _hotkeyService.Initialize(hotkeyHwndSource);
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;

            var settings = _services.GetRequiredService<ISettingsService>().Load();
            if (!_hotkeyService.Register(settings.HotkeyModifiers, settings.HotkeyVk))
            {
                _log.Warning("Primary hotkey {Hotkey} failed, trying fallback Ctrl+Shift+Q", settings.Hotkey);
                const uint MOD_CTRL_SHIFT = 0x0002 | 0x0004;
                if (!_hotkeyService.Register(MOD_CTRL_SHIFT, 0x51))
                {
                    _log.Warning("Fallback hotkey also failed");
                }
            }

            // Tray icon
            SetupTrayIcon();

            _log.Information("=== Prompt Clipboard started successfully ===");
        }
        catch (Exception ex)
        {
            _log?.Fatal(ex, "Fatal error during startup");
            Log.CloseAndFlush();
            throw;
        }
    }

    // ── Global Exception Handlers ──────────────────────────────────────

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.Error(e.Exception, "[CRASH] DispatcherUnhandledException: {Message}", e.Exception.Message);
        Log.CloseAndFlush();
        // Don't swallow — let the app crash so user sees it, but we have the log
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            _log?.Error(ex, "[CRASH] AppDomain.UnhandledException (terminating={Terminating}): {Message}",
                e.IsTerminating, ex.Message);
        else
            _log?.Error("[CRASH] AppDomain.UnhandledException (terminating={Terminating}): {Obj}",
                e.IsTerminating, e.ExceptionObject);
        Log.CloseAndFlush();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log?.Error(e.Exception, "[CRASH] UnobservedTaskException: {Message}", e.Exception?.Message);
        Log.CloseAndFlush();
        // Observe it so it doesn't kill the process
        e.SetObserved();
    }

    // ── Services ───────────────────────────────────────────────────────

    private void ConfigureServices(ServiceCollection services)
    {
        var settings = new SettingsService(AppDataPath, Log.Logger);
        var appSettings = settings.Load();

        var dbPath = string.IsNullOrEmpty(appSettings.DbPath)
            ? Path.Combine(AppDataPath, "prompts.db")
            : appSettings.DbPath;

        var connectionFactory = new SqliteConnectionFactory(dbPath);

        services.AddSingleton<ILogger>(Log.Logger);
        services.AddSingleton<ISettingsService>(settings);
        services.AddSingleton(connectionFactory);
        services.AddSingleton(new MigrationRunner(connectionFactory, Log.Logger));
        services.AddSingleton<IPromptRepository, SqlitePromptRepository>();
        services.AddSingleton<SearchRankingService>();
        services.AddSingleton<TemplateEngine>();
        services.AddSingleton<ImportExportUseCase>();
        services.AddSingleton<Win32HotkeyService>();
        services.AddSingleton<IFocusTracker, FocusTracker>();
        services.AddSingleton<IFocusRestoreService, FocusRestoreService>();
        services.AddSingleton<IInputSimulator, Win32InputSimulator>();
        services.AddSingleton<IClipboardService, Win32ClipboardService>();
        services.AddSingleton<IWindowPositioner, Win32WindowPositioner>();
        services.AddSingleton<IntegrityLevelChecker>();
        services.AddSingleton<PastePromptUseCase>();
        services.AddSingleton<PaletteViewModel>();
    }

    // ── Tray ───────────────────────────────────────────────────────────

    private void SetupTrayIcon()
    {
        var iconUri = new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute);
        var iconStream = System.Windows.Application.GetResourceStream(iconUri)?.Stream;

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Prompt Clipboard — Ctrl+Shift+Q"
        };

        if (iconStream != null)
        {
            _trayIcon.Icon = new System.Drawing.Icon(iconStream);
        }

        var contextMenu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "Показать палитру" };
        showItem.Click += (_, _) => ShowPaletteFromTray();
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Выход" };
        exitItem.Click += (_, _) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowPaletteFromTray();
    }

    // ── Show/Hide Palette ──────────────────────────────────────────────

    private void ShowPaletteFromTray()
    {
        _log?.Debug("ShowPaletteFromTray");
        var focusTracker = _services!.GetRequiredService<IFocusTracker>();
        focusTracker.ClearSavedHwnd();
        ShowPalette();
    }

    private void OnHotkeyPressed()
    {
        _log?.Debug("OnHotkeyPressed, visible={Visible}", _paletteWindow?.IsVisible);
        if (_paletteWindow == null) return;

        if (_paletteWindow.IsVisible)
        {
            _paletteWindow.HideWindow();
            return;
        }

        var focusTracker = _services!.GetRequiredService<IFocusTracker>();
        focusTracker.CaptureForegroundWindow();
        _log?.Debug("Captured target HWND: {Hwnd}", focusTracker.SavedHwnd);

        var positioner = _services!.GetRequiredService<IWindowPositioner>();
        var pos = positioner.GetPositionNearCaret(focusTracker.SavedHwnd);
        _paletteWindow.Left = pos.X;
        _paletteWindow.Top = pos.Y;
        _log?.Debug("Palette position: {X},{Y}", pos.X, pos.Y);

        ShowPalette();
    }

    private async void ShowPalette()
    {
        if (_paletteWindow == null) return;

        try
        {
            var focusTracker = _services!.GetRequiredService<IFocusTracker>();
            _paletteWindow.ViewModel.HasTarget = focusTracker.SavedHwnd != IntPtr.Zero;
            _log?.Debug("ShowPalette: hasTarget={HasTarget}", _paletteWindow.ViewModel.HasTarget);

            _paletteWindow.ShowAndFocus();
            await _paletteWindow.ViewModel.LoadPromptsAsync();
            _log?.Debug("ShowPalette: loaded {Count} prompts", _paletteWindow.ViewModel.Prompts.Count);
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "ShowPalette failed");
        }
    }

    // ── Paste ──────────────────────────────────────────────────────────

    private async void OnPasteRequested(Prompt prompt)
    {
        _log?.Information("OnPasteRequested: prompt={Id} '{Title}'", prompt.Id, prompt.Title);
        if (_services == null || _paletteWindow == null) return;

        try
        {
            var vm = _paletteWindow.ViewModel;
            var templateEngine = _services.GetRequiredService<TemplateEngine>();
            var resolvedText = prompt.Body;

            if (templateEngine.HasVariables(prompt.Body))
            {
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
                        return;
                    }
                }
                finally
                {
                    _paletteWindow.SuppressDeactivate(false);
                }

                resolvedText = templateEngine.Resolve(prompt.Body, dialogVm.GetValues());
            }

            _paletteWindow.HideWindow();

            var pasteUseCase = _services.GetRequiredService<PastePromptUseCase>();
            var settings = _services.GetRequiredService<ISettingsService>().Load();
            pasteUseCase.PrePasteDelayMs = settings.PasteDelayMs;
            pasteUseCase.PostPasteDelayMs = settings.RestoreDelayMs;

            var ilChecker = _services.GetRequiredService<IntegrityLevelChecker>();
            var currentIL = ilChecker.GetCurrentProcessIntegrityLevel();

            var focusTracker = _services.GetRequiredService<IFocusTracker>();
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
            var templateEngine = _services.GetRequiredService<TemplateEngine>();
            var resolvedText = prompt.Body;

            if (templateEngine.HasVariables(prompt.Body))
            {
                _log?.Debug("PasteAsText: prompt has template variables");
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
                        _log?.Debug("PasteAsText: template dialog cancelled");
                        return;
                    }
                }
                finally
                {
                    _paletteWindow.SuppressDeactivate(false);
                }

                resolvedText = templateEngine.Resolve(prompt.Body, dialogVm.GetValues());
            }

            _paletteWindow.HideWindow();

            var clipService = _services.GetRequiredService<IClipboardService>();
            clipService.SetTextWithMarker(resolvedText, Guid.NewGuid());
            _log?.Information("PasteAsText: text copied to clipboard for prompt {Id}", prompt.Id);
            _trayIcon?.ShowBalloonTip("Prompt Clipboard", "Текст скопирован в буфер обмена", BalloonIcon.Info);

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

    // ── Edit / Create ──────────────────────────────────────────────────

    private async void OnEditRequested(Prompt prompt)
    {
        _log?.Information("OnEditRequested: prompt={Id} '{Title}'", prompt.Id, prompt.Title);
        if (_services == null || _paletteWindow == null) return;

        try
        {
            _paletteWindow.SuppressDeactivate(true);
            try
            {
                var vm = new EditorViewModel(_services.GetRequiredService<IPromptRepository>());
                vm.LoadForEdit(prompt);
                var editor = new EditorWindow();
                editor.Initialize(vm);
                editor.Owner = _paletteWindow;
                editor.ShowDialog();
                _log?.Debug("Editor closed for prompt {Id}", prompt.Id);
            }
            finally
            {
                _paletteWindow.SuppressDeactivate(false);
            }

            await _paletteWindow.ViewModel.LoadPromptsAsync();
            _paletteWindow.Activate();
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnEditRequested failed for prompt {Id}", prompt.Id);
        }
    }

    private async void OnCreateRequested()
    {
        _log?.Information("OnCreateRequested");
        if (_services == null || _paletteWindow == null) return;

        try
        {
            _paletteWindow.SuppressDeactivate(true);
            try
            {
                var vm = new EditorViewModel(_services.GetRequiredService<IPromptRepository>());
                vm.LoadForCreate();
                var editor = new EditorWindow();
                editor.Initialize(vm);
                editor.Owner = _paletteWindow;
                editor.ShowDialog();
                _log?.Debug("Create editor closed");
            }
            finally
            {
                _paletteWindow.SuppressDeactivate(false);
            }

            await _paletteWindow.ViewModel.LoadPromptsAsync();
            _paletteWindow.Activate();
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnCreateRequested failed");
        }
    }

    // ── Copy / Pin ─────────────────────────────────────────────────────

    private void OnCopyRequested(Prompt prompt)
    {
        _log?.Debug("OnCopyRequested: prompt={Id}", prompt.Id);
        try
        {
            var clipService = _services!.GetRequiredService<IClipboardService>();
            clipService.SetTextWithMarker(prompt.Body, Guid.NewGuid());
            _trayIcon?.ShowBalloonTip("Prompt Clipboard", "Промпт скопирован в буфер обмена", BalloonIcon.Info);
        }
        catch (Exception ex)
        {
            _log?.Warning(ex, "Failed to copy to clipboard");
        }
    }

    private async void OnPinToggleRequested(Prompt prompt)
    {
        _log?.Debug("OnPinToggleRequested: prompt={Id}, wasPinned={Pinned}", prompt.Id, prompt.IsPinned);
        if (_services == null || _paletteWindow == null) return;

        try
        {
            var repo = _services.GetRequiredService<IPromptRepository>();
            prompt.IsPinned = !prompt.IsPinned;
            prompt.UpdatedAt = DateTime.UtcNow;
            await repo.UpdateAsync(prompt);
            await _paletteWindow.ViewModel.LoadPromptsAsync();
            _log?.Debug("Pin toggled for prompt {Id}, isPinned={Pinned}", prompt.Id, prompt.IsPinned);
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnPinToggleRequested failed for prompt {Id}", prompt.Id);
        }
    }

    private async void OnDeleteRequested(Prompt prompt)
    {
        _log?.Information("OnDeleteRequested: prompt={Id} '{Title}'", prompt.Id, prompt.Title);
        if (_services == null || _paletteWindow == null) return;

        try
        {
            _paletteWindow.SuppressDeactivate(true);
            try
            {
                var result = MessageBox.Show(
                    $"Удалить промпт \"{prompt.Title}\"?",
                    "Prompt Clipboard",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }
            finally
            {
                _paletteWindow.SuppressDeactivate(false);
            }

            var repo = _services.GetRequiredService<IPromptRepository>();
            await repo.DeleteAsync(prompt.Id);
            _log?.Information("Prompt {Id} deleted", prompt.Id);
            await _paletteWindow.ViewModel.LoadPromptsAsync();
            _paletteWindow.Activate();
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnDeleteRequested failed for prompt {Id}", prompt.Id);
        }
    }

    // ── Seed ───────────────────────────────────────────────────────────

    private async Task SeedDataAsync()
    {
        var repo = _services!.GetRequiredService<IPromptRepository>();
        var count = await repo.GetCountAsync();
        if (count > 0) return;

        _log?.Information("Seeding initial prompts...");

        var seeds = new List<Prompt>
        {
            CreateSeed("Email: Профессиональный ответ",
                "Напиши профессиональный ответ на email по теме \"{{topic}}\". Тон: {{tone|default=вежливый и деловой}}. Целевая аудитория: {{audience|default=коллеги}}.",
                ["email", "работа"], isPinned: true),
            CreateSeed("Jira: Описание задачи",
                "**Задача:** {{task_name}} **Описание:** {{description}} **Критерии приёмки:** - {{criteria_1}} - {{criteria_2}} - {{criteria_3}} **Технические детали:** {{tech_details}}",
                ["jira", "работа", "задачи"], isPinned: true),
            CreateSeed("Код: Ревью комментарий",
                "Предложение по улучшению: {{suggestion}} Причина: {{reason}} Пример: ```{{example}}```",
                ["код", "ревью"]),
            CreateSeed("Анализ кода",
                "Проанализируй следующий код и выдели: 1. Потенциальные проблемы 2. Возможности для оптимизации 3. Улучшения читаемости Код: {{code}}",
                ["код", "анализ"]),
            CreateSeed("Перевод текста",
                "Переведи следующий текст на {{target_lang|default=английский}} язык, сохраняя стиль и тон оригинала:\n\n{{text}}",
                ["перевод", "текст"]),
        };

        foreach (var seed in seeds)
            await repo.CreateAsync(seed);

        _log?.Information("Seeded {Count} prompts", seeds.Count);
    }

    private static Prompt CreateSeed(string title, string body, List<string> tags, bool isPinned = false)
    {
        var p = new Prompt
        {
            Title = title,
            Body = body,
            IsPinned = isPinned,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        p.SetTags(tags);
        return p;
    }

    // ── Shutdown ───────────────────────────────────────────────────────

    private void ExitApplication()
    {
        _log?.Information("=== Prompt Clipboard shutting down (user exit) ===");
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _services?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        Log.CloseAndFlush();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Information("=== OnExit ===");
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _services?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
