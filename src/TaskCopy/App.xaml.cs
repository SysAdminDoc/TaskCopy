using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.DependencyInjection;
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
    private ClipboardWatcher? _clipboardWatcher;

    private SnippetMenuWindow? _snippetMenu;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;

    // I35: DI container, populated in OnStartup after the services are constructed.
    // Existing manual wiring (still here) feeds the container; v0.5 features
    // (Velopack update service, Windhawk IPC bridge) prefer constructor injection
    // from the provider over App.xaml.cs hand-hooks.
    private ServiceProvider? _services;

    // One-time-per-session suppression for the "auto-paste skipped" toast.
    private bool _autoPasteFailToastShown;

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

        // F21: integrity guard. SQLite reports "ok" on a healthy DB; anything
        // else is either corruption or a fatal-ish migration scar. Surface the
        // restore path instead of letting later writes worsen the damage.
        try
        {
            var status = _db.IntegrityCheck();
            if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                CrashLog.Write("IntegrityCheck", new Exception($"PRAGMA quick_check returned: {status}"));
                var slots = BackupRotator.ListAvailable(_db);
                var freshest = slots.FirstOrDefault();
                var promptMsg = freshest is null
                    ? $"TaskCopy's snippet database may be corrupted (quick_check: {status}). No backups are available — Open data folder to inspect or replace manually."
                    : $"TaskCopy's snippet database may be corrupted (quick_check: {status}).\n\nRestore from {freshest.DisplayLabel}? A pre-restore snapshot will be saved so this is reversible.";

                var result = freshest is null
                    ? MessageBoxResult.None
                    : MessageBox.Show(promptMsg, "TaskCopy — Database integrity",
                        MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);

                if (result == MessageBoxResult.OK && freshest is not null)
                {
                    try
                    {
                        BackupRotator.RestoreFrom(_db, freshest.Path);
                        RelaunchSelf("--settings");
                        return;
                    }
                    catch (Exception restoreEx)
                    {
                        CrashLog.Write("StartupRestoreFromBackup", restoreEx);
                    }
                }
            }
        }
        catch (Exception ex) { CrashLog.Write("StartupIntegrityCheck", ex); }

        // Apply theme before any window opens — Mocha is the default and is
        // already merged via App.xaml, but if the user picked Latte (or Auto
        // resolves to Latte on a Light system) we swap the palette here.
        try { ThemeService.Apply(ThemeService.Resolve(_settings.Theme)); }
        catch (Exception ex) { CrashLog.Write("ThemeService.Apply", ex); }

        // B17: when Theme.Auto is selected, monitor OS theme changes so the
        // user gets the same I16-A relaunch prompt the Settings dropdown uses.
        try
        {
            ThemeService.StartSystemThemeWatcher(_settings.Theme);
            ThemeService.SystemThemeChanged += OnSystemThemeChanged;
        }
        catch (Exception ex) { CrashLog.Write("ThemeService.StartSystemThemeWatcher", ex); }

        // Daily-rotated 3-deep VACUUM INTO backups + 30-day trash purge,
        // ran off the UI thread so startup latency stays unchanged.
        // Backup runs at most once per 24h so frequent relaunches don't burn
        // through the 3-deep ring buffer in a single session.
        _ = Task.Run(() =>
        {
            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var elapsed = now - _settings.LastBackupAt;
                if (elapsed >= 24 * 60 * 60 || _settings.LastBackupAt == 0)
                {
                    BackupRotator.Rotate(_db);
                    _settings.LastBackupAt = now;
                }
            }
            catch (Exception ex) { CrashLog.Write("BackupRotator.Rotate", ex); }

            try
            {
                var cutoff = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
                var n = _db.PurgeDeletedOlderThan(cutoff);
                if (n > 0) CrashLog.Write("PurgeTrash", new Exception($"Purged {n} soft-deleted snippets older than 30 days."));
            }
            catch (Exception ex) { CrashLog.Write("PurgeDeleted", ex); }
        });
        _clipboard = new ClipboardService();
        _startup = new StartupService();
        _foreground = new ForegroundWindowCapture();
        _autoPaste = new AutoPasteService(_foreground, _settings);
        _autoPaste.CursorOffsetClamped += (_, requested) =>
            Dispatcher.BeginInvoke(() =>
                _trayIcon?.ShowNotification(
                    title: "TaskCopy",
                    message: $"Caret offset ({requested}) was clamped to 5000 — the {{cursor}} placeholder landed at the cap.",
                    icon: NotificationIcon.Info));

        _pipeServer = new SingleInstanceServer(OnPipeMessage);
        _pipeServer.Start();

        _hotkeys = new HotkeyService();
        _hotkeys.Triggered += (_, _) => Dispatcher.Invoke(ShowSnippetMenu);
        _hotkeys.SnippetTriggered += (_, snippetId) =>
            Dispatcher.BeginInvoke(() => OnSnippetHotkeyAsync(snippetId));
        _hotkeys.RegistrationFailed += (_, msg) =>
            Dispatcher.Invoke(() => _trayIcon?.ShowNotification(
                title: "TaskCopy",
                message: msg,
                icon: NotificationIcon.Warning));

        _hotkeyHost = new HotkeyHostWindow();
        MainWindow = _hotkeyHost;
        _hotkeys.TryRegister(_settings.HotkeyKey, _settings.HotkeyModifiers);

        // Per-snippet quick hotkeys (F7) — register every snippet that has a
        // quick_hotkey set. Failures are surfaced via RegistrationFailed.
        _hotkeys.RegisterAllSnippets(_db.GetAll().Select(s => (s.Id, s.QuickHotkey)));

        // Clipboard auto-capture (F15) — off by default, opt-in via Settings.
        _clipboardWatcher = new ClipboardWatcher(_hotkeyHost);
        _clipboardWatcher.Captured += OnClipboardCaptured;
        if (_settings.RecentClipsEnabled) _clipboardWatcher.Start();

        // I35: build the DI container from the now-constructed singletons. New
        // services (F26, Windhawk IPC) can resolve from `_services` without
        // adding hand-wired constructor calls here.
        var svc = new ServiceCollection();
        svc.AddSingleton(_db);
        svc.AddSingleton(_settings);
        svc.AddSingleton(_clipboard);
        svc.AddSingleton(_startup);
        svc.AddSingleton(_foreground);
        svc.AddSingleton(_autoPaste);
        svc.AddSingleton(_hotkeys);
        svc.AddSingleton(_pipeServer);
        svc.AddSingleton(_clipboardWatcher);
        _services = svc.BuildServiceProvider();

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

        // I27: wire CrashLog to surface non-fatal exceptions as a tray toast
        // instead of stealing foreground with a MessageBox.
        CrashLog.NonFatalNotifier = (title, message) =>
        {
            try
            {
                _trayIcon?.ShowNotification(
                    title: title,
                    message: message,
                    icon: NotificationIcon.Warning);
            }
            catch { /* notification failures are themselves non-fatal */ }
        };

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

            // Open Settings first so the welcome toast doesn't race the window-show
            // for foreground focus. Toast fires once Settings is actually visible.
            Dispatcher.BeginInvoke(() =>
            {
                ShowSettings();
                _trayIcon?.ShowNotification(
                    title: "Welcome to TaskCopy",
                    message: $"We've added a few example snippets. Right-click the tray for options, or press {HotkeyService.FormatHotkey(_settings.HotkeyKey, _settings.HotkeyModifiers)} to open the picker.",
                    icon: NotificationIcon.Info);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private static void SeedExampleSnippets(SnippetDatabase db)
    {
        // Skip seeding if a returning user lost the firstrun flag but still has snippets.
        if (db.GetAll().Count > 0) return;

        // Names/identity stay generic so first-launch doesn't ship anybody else's signature.
        // {{ask:Name}} prompts the user for their name at paste time — also demonstrates
        // the placeholder feature on day one.
        db.Insert("Email signature", "Best,\n{{ask:Name}}");
        db.Insert("Today's date", "{{date}}");
        db.Insert("Markdown link", "[label](https://)");
        db.Insert("Code-fence block", "```\n\n```");
        db.Insert("TaskCopy repo URL", "https://github.com/SysAdminDoc/TaskCopy");
    }

    private void ShowSnippetMenu()
    {
        if (_db is null || _clipboard is null || _foreground is null || _settings is null) return;

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
        vm.RecentClipCopyRequested += async (_, clip) => await HandleRecentClipCopyAsync(clip);
        vm.PromoteRecentClipRequested += (_, clip) => PromoteRecentClipToSnippet(clip);

        _snippetMenu = new SnippetMenuWindow(vm);
        _snippetMenu.Closed += (_, _) => _snippetMenu = null;
        _snippetMenu.ShowAtCursor(_settings?.FlyoutPosition ?? FlyoutPosition.Cursor);
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
        vm.ToggleRecentClipsRequested += (_, enabled) => SetRecentClipsEnabled(enabled);
        vm.ApplyThemeRequested += (_, label) => OfferThemeRelaunch(label);
        vm.ShowTrashRequested += (_, _) => ShowTrash(vm);
        vm.RestoreBackupRequested += (_, _) => ShowRestoreBackup(vm);
        vm.ResetToDefaultsRequested += (_, _) => ShowResetToDefaults(vm);
        // F47: hand the modal helper down so SettingsViewModel doesn't need a
        // Views-namespace using directive.
        vm.DeleteConfirmer = title => ConfirmDeleteWindow.Prompt(title, _settingsWindow);
        _settingsWindow = new SettingsWindow(vm);
        _settingsWindow.Closed += (_, _) =>
        {
            vm.FlushPendingSave();
            _settingsWindow = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async Task OnSnippetHotkeyAsync(long snippetId)
    {
        if (_db is null) return;
        var snippet = _db.GetAll().FirstOrDefault(s => s.Id == snippetId);
        if (snippet is null) return;
        // Capture foreground BEFORE we run any UI work — for direct hotkeys
        // there's no flyout to pre-capture from.
        _foreground?.Capture();
        await HandleSnippetCopyAsync(snippet);
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

        // Suppress the watcher echo *before* writing — clipboard listeners
        // fire synchronously in WM_CLIPBOARDUPDATE on the same dispatcher
        // pump and the comparison key needs to be in place when they do.
        _clipboardWatcher?.SuppressNext(expansion.Body);

        if (!_clipboard.TryCopy(expansion.Body))
        {
            return;
        }

        try { _db.RecordUse(snippet.Id); } catch (Exception ex) { CrashLog.Write("RecordUse", ex); }

        _snippetMenu?.Close();

        await Task.Delay(20);
        if (_autoPaste is null) return;
        var result = await _autoPaste.TryAutoPasteDetailedAsync(
            expansion.CursorOffsetFromEnd,
            typedBody: expansion.Body,
            pasteMode: snippet.PasteMode).ConfigureAwait(true);
        if (result == AutoPasteService.Result.ForegroundRestoreFailed && !_autoPasteFailToastShown)
        {
            _autoPasteFailToastShown = true;
            _trayIcon?.ShowNotification(
                title: "TaskCopy",
                message: "Auto-paste was skipped — the target window may be running elevated. The text is on your clipboard; press Ctrl+V to paste it manually.",
                icon: NotificationIcon.Info);
        }
    }

    private async Task HandleRecentClipCopyAsync(RecentClip clip)
    {
        if (_clipboard is null) return;

        _clipboardWatcher?.SuppressNext(clip.Body);
        if (!_clipboard.TryCopy(clip.Body)) return;

        _snippetMenu?.Close();
        await Task.Delay(20);
        if (_autoPaste is null) return;
        var result = await _autoPaste.TryAutoPasteDetailedAsync(null).ConfigureAwait(true);
        if (result == AutoPasteService.Result.ForegroundRestoreFailed && !_autoPasteFailToastShown)
        {
            _autoPasteFailToastShown = true;
            _trayIcon?.ShowNotification(
                title: "TaskCopy",
                message: "Auto-paste was skipped — the target window may be running elevated. The text is on your clipboard; press Ctrl+V to paste it manually.",
                icon: NotificationIcon.Info);
        }
    }

    private void PromoteRecentClipToSnippet(RecentClip clip)
    {
        if (_db is null) return;

        // Use the clip's first non-empty line as the title (capped at 60 chars).
        var line = clip.Body
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? "New snippet";
        if (line.Length > 60) line = line[..60] + "…";

        try
        {
            _db.Insert(line, clip.Body);
        }
        catch (Exception ex)
        {
            CrashLog.Write("PromoteRecentClipToSnippet", ex);
            return;
        }

        _snippetMenu?.Close();
        ShowSettings();
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

    private void OnClipboardCaptured(object? sender, string body)
    {
        if (_db is null || _settings is null) return;
        try { _db.InsertRecentClip(body, _settings.RecentClipsMax); }
        catch (Exception ex) { CrashLog.Write("InsertRecentClip", ex); }
    }

    /// <summary>
    /// Toggle clipboard auto-capture (F15). Called by Settings when the user
    /// flips the checkbox. Starting the watcher requires the hidden HWND.
    /// </summary>
    public void SetRecentClipsEnabled(bool enabled)
    {
        if (_settings is null || _clipboardWatcher is null) return;
        _settings.RecentClipsEnabled = enabled;
        if (enabled) _clipboardWatcher.Start();
        else _clipboardWatcher.Stop();
    }

    private void OnPipeMessage(string msg)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (string.IsNullOrEmpty(msg)) { ShowSettings(); return; }

            if (msg.StartsWith(SingleInstanceServer.MsgCopyPrefix, StringComparison.Ordinal))
            {
                var arg = msg[SingleInstanceServer.MsgCopyPrefix.Length..];
                HandleCliCopyOrPaste(arg, paste: false);
                return;
            }
            if (msg.StartsWith(SingleInstanceServer.MsgPastePrefix, StringComparison.Ordinal))
            {
                var arg = msg[SingleInstanceServer.MsgPastePrefix.Length..];
                HandleCliCopyOrPaste(arg, paste: true);
                return;
            }
            switch (msg)
            {
                case SingleInstanceServer.MsgOpenFlyout:
                    ShowSnippetMenu();
                    break;
                case SingleInstanceServer.MsgList:
                    DumpListToDisk();
                    break;
                case SingleInstanceServer.MsgOpenSettings:
                default:
                    ShowSettings();
                    break;
            }
        });
    }

    private void HandleCliCopyOrPaste(string idOrTitle, bool paste)
    {
        if (_db is null || string.IsNullOrWhiteSpace(idOrTitle)) return;
        var all = _db.GetAll();
        Snippet? match = null;
        if (long.TryParse(idOrTitle, out var id))
        {
            match = all.FirstOrDefault(s => s.Id == id);
        }
        match ??= all.FirstOrDefault(s => string.Equals(s.Title, idOrTitle, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            CrashLog.Write("CliCopyOrPaste", new Exception($"No snippet matched '{idOrTitle}'."));
            return;
        }

        if (paste)
        {
            // Capture foreground BEFORE we run any UI work — the foreground at the
            // moment the CLI arrived is the right paste target.
            _foreground?.Capture();
            _ = HandleSnippetCopyAsync(match);
        }
        else
        {
            // Copy-only: skip auto-paste regardless of setting.
            var ctx = new TemplatingContext
            {
                PreviousClipboard = TryReadClipboardText(),
                PromptFor = field => Dispatcher.Invoke(() => AskWindow.Prompt(field)),
            };
            try
            {
                var expansion = SnippetTemplating.Expand(match.Body, ctx);
                if (expansion.Cancelled) return;
                _clipboardWatcher?.SuppressNext(expansion.Body);
                _clipboard?.TryCopy(expansion.Body);
                try { _db.RecordUse(match.Id); } catch { }
            }
            catch (Exception ex) { CrashLog.Write("CliCopy", ex); }
        }
    }

    private void DumpListToDisk()
    {
        if (_db is null) return;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TaskCopy");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "snippets.list");
            using var w = new StreamWriter(path, append: false, System.Text.Encoding.UTF8);
            foreach (var s in _db.GetAll())
            {
                w.WriteLine($"{s.Id}\t{s.Title}");
            }
        }
        catch (Exception ex) { CrashLog.Write("DumpList", ex); }
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

    private void ShowTrash(SettingsViewModel parentVm)
    {
        if (_db is null) return;
        var vm = new TrashViewModel(_db);
        var w = new TrashWindow(vm) { Owner = _settingsWindow };
        // Restored snippets need re-registration of any quick hotkey + UI refresh.
        vm.RestoredAny += (_, _) =>
        {
            if (_hotkeys is not null)
            {
                _hotkeys.RegisterAllSnippets(_db.GetAll().Select(s => (s.Id, s.QuickHotkey)));
            }
            parentVm.LoadFromStore();
        };
        w.ShowDialog();
    }

    /// <summary>
    /// F21: present available .bak.{0..2} files and swap in the chosen one
    /// after a confirm. A pre-restore snapshot is taken so the operation is
    /// itself reversible (the snapshot lands at snippets.bak.preRestore.db).
    /// </summary>
    private void ShowRestoreBackup(SettingsViewModel parentVm)
    {
        if (_db is null) return;
        var slots = BackupRotator.ListAvailable(_db);
        if (slots.Count == 0)
        {
            MessageBox.Show("No backups available yet. Backups are written on app launch (at most once per 24 h).",
                "TaskCopy — Restore", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Simple chooser: build a labelled menu via MessageBox lines. WPF doesn't
        // ship a built-in single-select dialog; the rotated set is <=3 entries
        // so a small modal is overkill — we present the freshest with a confirm
        // and let the user re-run if they want an older one. Surface the list.
        var labels = string.Join("\n", slots.Select(s => "  • " + s.DisplayLabel));
        var prompt =
            $"Restore from the latest backup?\n\n{slots[0].DisplayLabel}\n\nAll available backups (newest first):\n{labels}\n\n"
            + "A pre-restore snapshot will be saved as snippets.bak.preRestore.db so this is reversible.";
        var result = MessageBox.Show(prompt, "TaskCopy — Restore backup",
            MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (result != MessageBoxResult.OK) return;

        try
        {
            BackupRotator.RestoreFrom(_db, slots[0].Path);
            parentVm.StatusMessage = $"Restored {slots[0].DisplayLabel}. Relaunch to apply.";
            MessageBox.Show(
                "Restore complete. TaskCopy will now restart so the new database is opened cleanly.",
                "TaskCopy — Restore backup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RelaunchSelf("--settings");
        }
        catch (Exception ex)
        {
            CrashLog.Write("RestoreBackup", ex);
            MessageBox.Show($"Restore failed: {ex.Message}", "TaskCopy — Restore backup",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// F52: confirm + wipe the settings KV table + relaunch. Snippets/groups/
    /// trash are preserved; the in-memory hotkey/theme/etc. caches are stale
    /// after the wipe so we relaunch back into Settings.
    /// </summary>
    private void ShowResetToDefaults(SettingsViewModel parentVm)
    {
        if (_db is null) return;
        var result = MessageBox.Show(
            "Reset all settings to defaults?\n\n"
            + "Your snippets, groups, and trash are NOT affected — only preferences "
            + "(hotkey, theme, auto-paste, recent-clips opt-in, flyout position, "
            + "delete-confirm-skip, last backup timestamp).\n\n"
            + "TaskCopy will restart and reopen Settings.",
            "TaskCopy — Reset to defaults",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);
        if (result != MessageBoxResult.OK) return;

        try
        {
            _db.ClearAllSettings();
            parentVm.StatusMessage = "Settings reset to defaults. Relaunching…";
            RelaunchSelf("--settings");
        }
        catch (Exception ex)
        {
            CrashLog.Write("ResetToDefaults", ex);
            MessageBox.Show($"Reset failed: {ex.Message}", "TaskCopy",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RelaunchSelf(string args)
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                });
            }
            QuitApp();
        }
        catch (Exception ex)
        {
            CrashLog.Write("RelaunchSelf", ex);
        }
    }

    /// <summary>
    /// B17: OS theme flipped while we're in Theme.Auto and the resolved
    /// palette would change. Same prompt-and-relaunch UX as the manual theme
    /// dropdown — UserPreferenceChanged fires on a non-UI thread, so dispatch
    /// the MessageBox onto the UI thread.
    /// </summary>
    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
            OfferThemeRelaunch("System theme — TaskCopy is in Auto mode"));
    }

    /// <summary>
    /// I16 (Option A): theme changes can't propagate into already-shown windows
    /// because brushes are bound via StaticResource. Offer an explicit relaunch
    /// that restores Settings on the other side via the --settings CLI handoff.
    /// </summary>
    private void OfferThemeRelaunch(string themeLabel)
    {
        var result = MessageBox.Show(
            $"Apply {themeLabel} now? TaskCopy will restart and Settings will reopen.",
            "TaskCopy — Theme",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);
        if (result != MessageBoxResult.OK) return;
        RelaunchSelf("--settings");
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
            ThemeService.SystemThemeChanged -= OnSystemThemeChanged;
            ThemeService.StopSystemThemeWatcher();
            _clipboardWatcher?.Dispose();
            _pipeServer?.Stop();
            _hotkeys?.Unregister();
            _trayIcon?.Dispose();
            _hotkeyHost?.Close();
            _services?.Dispose();
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
