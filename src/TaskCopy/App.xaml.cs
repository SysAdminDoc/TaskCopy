using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using TaskCopy.Data;
using TaskCopy.Services;
using TaskCopy.ViewModels;
using TaskCopy.Views;

namespace TaskCopy;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\TaskCopy_SingleInstance";

    private Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;
    private HotkeyHostWindow? _hotkeyHost;

    private SnippetDatabase? _db;
    private SettingsStore? _settings;
    private ClipboardService? _clipboard;
    private StartupService? _startup;
    private HotkeyService? _hotkeys;
    private ForegroundWindowCapture? _foreground;

    private SnippetMenuWindow? _snippetMenu;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown(0);
            return;
        }

        CrashLog.Install();

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskCopy");
        Directory.CreateDirectory(dataDir);

        _db = new SnippetDatabase(Path.Combine(dataDir, "snippets.db"));
        _settings = new SettingsStore(_db);
        _clipboard = new ClipboardService();
        _startup = new StartupService();
        _foreground = new ForegroundWindowCapture();

        _hotkeys = new HotkeyService();
        _hotkeys.Triggered += (_, _) => Dispatcher.Invoke(ShowSnippetMenu);
        _hotkeys.RegistrationFailed += (_, msg) =>
            Dispatcher.Invoke(() => _trayIcon?.ShowNotification(
                title: "TaskCopy",
                message: msg,
                icon: NotificationIcon.Warning));

        _hotkeyHost = new HotkeyHostWindow();
        MainWindow = _hotkeyHost;
        _hotkeys.TryRegister(_settings.HotkeyKey, _settings.HotkeyModifiers);

        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ToolTipText = "TaskCopy — click for snippets",
            NoLeftClickDelay = true,
        };
        _trayIcon.TrayLeftMouseUp += (_, _) => ShowSnippetMenu();
        _trayIcon.TrayRightMouseUp += (_, _) => ShowSnippetMenu();
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowSettings();
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);

        _trayIcon.ShowNotification(
            title: "TaskCopy is running",
            message: $"Right-click the tray icon or press {HotkeyService.FormatHotkey(_settings.HotkeyKey, _settings.HotkeyModifiers)} to open your snippets.",
            icon: NotificationIcon.Info);
    }

    private void ShowSnippetMenu()
    {
        if (_db is null || _clipboard is null || _foreground is null) return;

        _foreground.Capture();

        if (_snippetMenu is not null && _snippetMenu.IsVisible)
        {
            _snippetMenu.Close();
            return;
        }

        var vm = new SnippetMenuViewModel(_db, _clipboard);
        vm.EditRequested += (_, _) =>
        {
            _snippetMenu?.Close();
            ShowSettings();
        };
        vm.QuitRequested += (_, _) => QuitApp();

        _snippetMenu = new SnippetMenuWindow(vm);
        _snippetMenu.Closed += (_, _) => _snippetMenu = null;
        _snippetMenu.ShowAtCursor();
    }

    private void ShowSettings()
    {
        if (_db is null || _settings is null || _startup is null || _hotkeys is null) return;

        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            if (_settingsWindow.DataContext is SettingsViewModel existingVm) existingVm.LoadFromStore();
            _settingsWindow.Activate();
            return;
        }

        var vm = new SettingsViewModel(_db, _settings, _startup, _hotkeys);
        _settingsWindow = new SettingsWindow(vm);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void QuitApp()
    {
        _hotkeys?.Unregister();
        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _hotkeys?.Unregister();
            _trayIcon?.Dispose();
            _hotkeyHost?.Close();
            if (_singleInstanceMutex is { } m)
            {
                try { m.ReleaseMutex(); } catch (ApplicationException) { }
                m.Dispose();
            }
        }
        catch
        {
            // best-effort on exit
        }

        base.OnExit(e);
    }
}
