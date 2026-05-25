using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using TaskCopy.Data;
using TaskCopy.Models;
using TaskCopy.Services;
using TaskCopy.ViewModels;
using TaskCopy.Views;

namespace TaskCopy;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Local\\TaskCopy_SingleInstance";

    private Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;
    private HotkeyHostWindow? _hotkeyHost;

    private SnippetDatabase? _db;
    private SettingsStore? _settings;
    private ClipboardService? _clipboard;
    private StartupService? _startup;
    private HotkeyService? _hotkeys;
    private ForegroundWindowCapture? _foreground;
    private AutoPasteService? _autoPaste;
    private SingleInstanceServer? _pipeServer;

    private SnippetMenuWindow? _snippetMenu;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // Hand off to the first instance: by default open Settings,
            // or whatever the CLI args asked for.
            SingleInstanceServer.TrySend(SingleInstanceServer.ParseCliMessage(e.Args));
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

        // Daily-rotated 3-deep VACUUM INTO backups, ran off the UI thread so
        // startup latency stays unchanged.
        _ = Task.Run(() =>
        {
            try { BackupRotator.Rotate(_db); }
            catch (Exception ex) { CrashLog.Write("BackupRotator.Rotate", ex); }
        });
        _clipboard = new ClipboardService();
        _startup = new StartupService();
        _foreground = new ForegroundWindowCapture();
        _autoPaste = new AutoPasteService(_foreground, _settings);

        _pipeServer = new SingleInstanceServer(OnPipeMessage);
        _pipeServer.Start();

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
            ToolTipText = "TaskCopy — left-click for snippets, right-click for menu",
            NoLeftClickDelay = true,
            ContextMenu = BuildTrayMenu(),
        };
        _trayIcon.TrayLeftMouseUp += (_, _) => ShowSnippetMenu();
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowSettings();
        _trayIcon.ForceCreate(enablesEfficiencyMode: true);

        var isFirstRun = !_settings.IsFirstRunComplete;
        if (isFirstRun)
        {
            try
            {
                SeedExampleSnippets(_db);
            }
            catch (Exception ex)
            {
                CrashLog.Write("FirstRunSeed", ex);
            }
            _settings.MarkFirstRunComplete();

            _trayIcon.ShowNotification(
                title: "Welcome to TaskCopy",
                message: $"We've added a few example snippets. Right-click the tray for options, or press {HotkeyService.FormatHotkey(_settings.HotkeyKey, _settings.HotkeyModifiers)} to open the picker.",
                icon: NotificationIcon.Info);

            Dispatcher.BeginInvoke(ShowSettings, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private static void SeedExampleSnippets(SnippetDatabase db)
    {
        // Skip seeding if a returning user lost the firstrun flag but still has snippets.
        if (db.GetAll().Count > 0) return;

        db.Insert("Email signature", "Best,\nMatt");
        db.Insert("Markdown link", "[label](https://)");
        db.Insert("ISO date stamp", "2026-05-24");
        db.Insert("Code-fence block", "```\n\n```");
        db.Insert("TaskCopy repo URL", "https://github.com/SysAdminDoc/TaskCopy");
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

        var vm = new SnippetMenuViewModel(_db, _settings);
        vm.EditRequested += (_, _) =>
        {
            _snippetMenu?.Close();
            ShowSettings();
        };
        vm.AboutRequested += (_, _) =>
        {
            _snippetMenu?.Close();
            ShowAbout();
        };
        vm.QuitRequested += (_, _) => QuitApp();
        vm.SnippetCopyRequested += async (_, s) => await HandleSnippetCopyAsync(s);

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
        vm.ManageGroupsRequested += (_, _) => ShowManageGroups(vm);
        _settingsWindow = new SettingsWindow(vm);
        _settingsWindow.Closed += (_, _) =>
        {
            vm.FlushPendingSave();
            _settingsWindow = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async Task HandleSnippetCopyAsync(Snippet snippet)
    {
        if (_clipboard is null || _db is null) return;

        var previousClipboard = TryReadClipboardText();

        var ctx = new TemplatingContext
        {
            PreviousClipboard = previousClipboard,
            PromptFor = field => Dispatcher.Invoke(() => AskWindow.Prompt(field)),
        };

        ExpansionResult expansion;
        try
        {
            expansion = SnippetTemplating.Expand(snippet.Body, ctx);
        }
        catch (Exception ex)
        {
            CrashLog.Write("SnippetTemplating.Expand", ex);
            expansion = new ExpansionResult { Body = snippet.Body };
        }

        if (expansion.Cancelled)
        {
            _snippetMenu?.Close();
            return;
        }

        if (!_clipboard.TryCopy(expansion.Body))
        {
            return;
        }

        try { _db.RecordUse(snippet.Id); } catch (Exception ex) { CrashLog.Write("RecordUse", ex); }

        _snippetMenu?.Close();

        await Task.Delay(20);
        Dispatcher.Invoke(() => _autoPaste?.TryAutoPaste(expansion.CursorOffsetFromEnd));
    }

    private static string TryReadClipboardText()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Clipboard.ContainsText()) return Clipboard.GetText() ?? string.Empty;
                return string.Empty;
            }
            catch
            {
                Thread.Sleep(30);
            }
        }
        return string.Empty;
    }

    private void OnPipeMessage(string msg)
    {
        Dispatcher.BeginInvoke(() =>
        {
            switch (msg)
            {
                case SingleInstanceServer.MsgOpenFlyout:
                    ShowSnippetMenu();
                    break;
                case SingleInstanceServer.MsgOpenSettings:
                default:
                    ShowSettings();
                    break;
            }
        });
    }

    private void ShowManageGroups(SettingsViewModel parentVm)
    {
        if (_db is null) return;
        var vm = new ManageGroupsViewModel(_db);
        var w = new ManageGroupsWindow(vm) { Owner = _settingsWindow };
        w.Closed += (_, _) =>
        {
            // Refresh group list in the parent settings VM so the dropdown picks up
            // new/renamed/deleted groups, and re-load snippets so any group_id
            // cleared by ON DELETE SET NULL is reflected.
            parentVm.LoadFromStore();
        };
        w.ShowDialog();
    }

    private void ShowAbout()
    {
        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = new AboutWindow
        {
            Owner = _settingsWindow,
        };
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Show();
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu
        {
            Style = (Style)Resources["Mocha.ContextMenu"],
        };
        menu.Items.Add(NewMenuItem("Open snippets", (_, _) => ShowSnippetMenu(),
            HotkeyService.FormatHotkey(_settings!.HotkeyKey, _settings.HotkeyModifiers)));
        menu.Items.Add(NewMenuItem("Settings…", (_, _) => ShowSettings()));
        menu.Items.Add(NewMenuItem("About", (_, _) => ShowAbout()));
        menu.Items.Add(new Separator { Style = (Style)Resources["Mocha.MenuSeparator"] });
        menu.Items.Add(NewMenuItem("Quit TaskCopy", (_, _) => QuitApp()));
        return menu;
    }

    private MenuItem NewMenuItem(string header, RoutedEventHandler onClick, string? gesture = null)
    {
        var item = new MenuItem
        {
            Header = header,
            Style = (Style)Resources["Mocha.MenuItem"],
        };
        if (!string.IsNullOrEmpty(gesture)) item.InputGestureText = gesture;
        item.Click += onClick;
        return item;
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
            _pipeServer?.Stop();
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
