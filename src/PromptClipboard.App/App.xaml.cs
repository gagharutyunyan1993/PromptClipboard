using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using PromptClipboard.App.Handlers;
using PromptClipboard.App.Interfaces;
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
    private bool _ownsMutex;
    private System.Windows.Controls.MenuItem? _updateMenuItem;

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
        _ownsMutex = isNew;
        if (!isNew)
        {
            MessageBox.Show("Prompt Clipboard is already running.", "Prompt Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
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
            try
            {
                migrationRunner.RunAll();
            }
            catch (BackupFailedException ex)
            {
                _log.Fatal(ex, "Database migration blocked — backup failed");
                var errorHandler = _services.GetRequiredService<IStartupErrorHandler>();
                errorHandler.HandleFatalError(
                    "Prompt Clipboard — Startup Error",
                    $"Database upgrade requires a backup, but backup failed:\n{ex.InnerException?.Message}\n\n" +
                    "Please check disk space/permissions and restart the application.",
                    ex);
                return;
            }
            catch (IncompatibleSchemaException ex)
            {
                _log.Fatal(ex, "Database migration blocked — incompatible schema");
                var errorHandler = _services.GetRequiredService<IStartupErrorHandler>();
                errorHandler.HandleFatalError(
                    "Prompt Clipboard — Startup Error",
                    $"Database schema is incompatible with this version:\n{ex.Message}\n\n" +
                    "Please restore from backup or run manual migration.",
                    ex);
                return;
            }

            // Seed data
            await PromptSeeder.SeedIfNeededAsync(
                _services.GetRequiredService<IPromptRepository>(),
                _services.GetRequiredService<ISettingsService>(),
                _log);

            // Analytics cleanup (runs if enabled)
            var currentSettings = _services.GetRequiredService<ISettingsService>().Load();
            if (currentSettings.EnableUsageStats)
            {
                try
                {
                    var analytics = _services.GetRequiredService<IAnalyticsService>();
                    await analytics.CleanupAsync(currentSettings.StatsRetentionDays, currentSettings.StatsMaxRows);
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Analytics cleanup failed (non-fatal)");
                }
            }

            // Palette window
            _paletteWindow = new PaletteWindow();
            _paletteWindow.Width = Win32WindowPositioner.DefaultPaletteWidth;
            _paletteWindow.Height = Win32WindowPositioner.DefaultPaletteHeight;

            var paletteVm = _services.GetRequiredService<PaletteViewModel>();
            _paletteWindow.DataContext = paletteVm;

            // Wire QuickAdd sub-VM
            var quickAdd = new QuickAddViewModel(
                _services.GetRequiredService<IPromptRepository>(),
                _services.GetRequiredService<ITagSuggestionRepository>(),
                _services.GetRequiredService<IDuplicateDetectionRepository>(),
                Log.Logger);
            paletteVm.InitializeQuickAdd(quickAdd);

            // Wire events
            paletteVm.PasteRequested += OnPasteRequested;
            paletteVm.PasteAsTextRequested += OnPasteAsTextRequested;
            paletteVm.EditRequested += OnEditRequested;
            paletteVm.CreateRequested += OnCreateRequested;
            paletteVm.CreateWithTitleRequested += OnCreateWithTitleRequested;
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

            // Auto-update: check if a previously downloaded update is pending
            var updateService = _services.GetRequiredService<IUpdateService>();
            if (updateService.IsUpdateReady)
            {
                ShowUpdateMenuItem(updateService.PendingVersion);
            }

            // Background update check after 5s delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                await CheckForUpdatesAsync();
            });

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
        services.AddSingleton<SqlitePromptRepository>();
        services.AddSingleton<IPromptRepository>(sp => sp.GetRequiredService<SqlitePromptRepository>());
        services.AddSingleton<IAdvancedSearchRepository>(sp => sp.GetRequiredService<SqlitePromptRepository>());
        services.AddSingleton<ITagSuggestionRepository>(sp => sp.GetRequiredService<SqlitePromptRepository>());
        services.AddSingleton<IDuplicateDetectionRepository>(sp => sp.GetRequiredService<SqlitePromptRepository>());
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
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<PastePromptUseCase>();
        services.AddSingleton<IStartupErrorHandler, StartupErrorHandler>();
        services.AddSingleton<IAnalyticsService>(new SqliteAnalyticsService(AppDataPath, Log.Logger));
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

        var showItem = new System.Windows.Controls.MenuItem { Header = "Show palette" };
        showItem.Click += (_, _) => ShowPaletteFromTray();
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
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

    private bool _isShowingPalette;

    private async void ShowPalette()
    {
        if (_paletteWindow == null) return;
        if (_isShowingPalette) return;
        _isShowingPalette = true;
        try
        {
            var focusTracker = _services!.GetRequiredService<IFocusTracker>();
            _paletteWindow.ViewModel.HasTarget = focusTracker.SavedHwnd != IntPtr.Zero;
            _log?.Debug("ShowPalette: hasTarget={HasTarget}", _paletteWindow.ViewModel.HasTarget);

            // Stage: load data BEFORE showing window for correct empty state
            var countTask = _paletteWindow.ViewModel.RefreshTotalCountAsync();
            var loadTask = _paletteWindow.ViewModel.LoadPromptsAsync();
            await Task.WhenAll(countTask, loadTask);

            // Show: data is already loaded, empty state is correct
            _paletteWindow.ShowAndFocus();
            _log?.Debug("ShowPalette: loaded {Count} prompts", _paletteWindow.ViewModel.Prompts.Count);
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "ShowPalette failed");
            _paletteWindow.ViewModel.SetTransientLoadError(
                "Couldn't load prompts. Check database file/permissions and try again.");
            _paletteWindow.ShowAndFocus();
        }
        finally
        {
            _isShowingPalette = false;
        }
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

            await _paletteWindow.ViewModel.RefreshTotalCountAsync();
            await _paletteWindow.ViewModel.LoadPromptsAsync();
            _paletteWindow.Activate();
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnCreateRequested failed");
        }
    }

    private async void OnCreateWithTitleRequested(string title, string? tag, string? lang)
    {
        _log?.Information("OnCreateWithTitleRequested: title='{Title}', tag='{Tag}', lang='{Lang}'", title, tag, lang);
        if (_services == null || _paletteWindow == null) return;

        try
        {
            _paletteWindow.SuppressDeactivate(true);
            try
            {
                var vm = new EditorViewModel(_services.GetRequiredService<IPromptRepository>());
                vm.LoadForCreate();
                vm.Title = title;
                if (tag != null) vm.TagsInput = tag;
                if (lang != null) vm.Lang = lang;
                var editor = new EditorWindow();
                editor.Initialize(vm);
                editor.Owner = _paletteWindow;
                editor.ShowDialog();
                _log?.Debug("CreateWithTitle editor closed");
            }
            finally
            {
                _paletteWindow.SuppressDeactivate(false);
            }

            await _paletteWindow.ViewModel.RefreshTotalCountAsync();
            await _paletteWindow.ViewModel.LoadPromptsAsync();
            _paletteWindow.Activate();
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnCreateWithTitleRequested failed");
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
            _trayIcon?.ShowBalloonTip("Prompt Clipboard", "Prompt copied to clipboard", BalloonIcon.Info);
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
                    $"Delete prompt \"{prompt.Title}\"?",
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
            await _paletteWindow.ViewModel.RefreshTotalCountAsync();
            await _paletteWindow.ViewModel.LoadPromptsAsync();
            _paletteWindow.Activate();
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "OnDeleteRequested failed for prompt {Id}", prompt.Id);
        }
    }

    // ── Auto-Update ──────────────────────────────────────────────────

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var settingsService = _services!.GetRequiredService<ISettingsService>();
            var appSettings = settingsService.Load();

            var updateService = _services!.GetRequiredService<IUpdateService>();
            var version = await updateService.CheckAndDownloadAsync();

            appSettings.LastUpdateCheckUtc = DateTime.UtcNow;
            settingsService.Save(appSettings);

            if (version != null)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowUpdateMenuItem(version);
                    _trayIcon?.ShowBalloonTip(
                        "Prompt Clipboard",
                        $"Update v{version} ready — right-click tray icon to restart",
                        BalloonIcon.Info);
                });
            }
        }
        catch (Exception ex)
        {
            _log?.Warning(ex, "Background update check failed");
        }
    }

    private void ShowUpdateMenuItem(string? version)
    {
        if (_trayIcon?.ContextMenu == null || _updateMenuItem != null) return;

        var label = string.IsNullOrEmpty(version)
            ? "Update available — restart to apply"
            : $"Update v{version} — restart to apply";

        _updateMenuItem = new System.Windows.Controls.MenuItem { Header = label, FontWeight = FontWeights.Bold };
        _updateMenuItem.Click += (_, _) =>
        {
            var updateService = _services?.GetRequiredService<IUpdateService>();
            updateService?.ApplyAndRestart();
        };

        // Insert at the top of the context menu
        _trayIcon.ContextMenu.Items.Insert(0, _updateMenuItem);
        _trayIcon.ContextMenu.Items.Insert(1, new System.Windows.Controls.Separator());
    }

    // ── Shutdown ───────────────────────────────────────────────────────

    private void ExitApplication()
    {
        _log?.Information("=== Prompt Clipboard shutting down (user exit) ===");
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Information("=== OnExit ===");
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _services?.Dispose();
        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _ownsMutex = false;
        }
        _singleInstanceMutex?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
