# TaskCopy — Roadmap

Single-click clipboard snippet manager for Windows. Right-click the tray icon (or hit a hotkey) to open a flyout near the cursor with your stored snippets; click one to copy + auto-paste into the previously focused window.

**Stack:** C# / .NET 9 WPF · H.NotifyIcon.Wpf · NHotkey.Wpf · SQLite · CommunityToolkit.Mvvm · Catppuccin Mocha
**Reality check:** literally extending the Windows 11 taskbar right-click menu requires Windhawk DLL injection. The tray + cursor-flyout pattern delivers the same UX with zero injection. A Windhawk companion mod can add the literal taskbar trigger as a v0.2+ power-user add-on.

---

## v0.1.0 — Minimal viable snippet menu ✅ SHIPPED (2026-05-23)

Goal: open a popup near the cursor, see a list of snippets, click to copy.

- [x] Project scaffold — `TaskCopy.sln`, `src/TaskCopy/TaskCopy.csproj` (`net10.0-windows`, `UseWPF=true`), NuGet refs (H.NotifyIcon.Wpf 2.4.1, NHotkey.Wpf 3.0.0, CommunityToolkit.Mvvm 8.4.0, Microsoft.Data.Sqlite 9.0.0)
- [x] App icon — multi-resolution PNG-encoded ICO (16/32/48/64/128/256) at `src/TaskCopy/Assets/app.ico`; generator at `tools/generate-icon.ps1`
- [x] Tray host — `TaskbarIcon` from `H.NotifyIcon.Wpf`; no default ContextMenu; tray-right-click/left-click both fire `ShowSnippetMenu`; double-click → Settings
- [x] Snippet store — SQLite at `%LOCALAPPDATA%\TaskCopy\snippets.db`; tables `snippets(id, title, body, sort_order, created_at)` + `settings(key, value)`
- [x] Repository layer — `SnippetDatabase` w/ `GetAll / Insert / Update / Delete / Reorder` + `GetSetting / SetSetting`
- [x] Flyout view — `SnippetMenuWindow` (WPF Window, `WindowStyle=None`, `AllowsTransparency=true`, drop-shadow, Catppuccin Mocha, 10 px corner radius)
- [x] Cursor-anchored positioning — opens above-and-left of cursor; monitor-clamped via `MonitorFromPoint` + `GetMonitorInfo`; DPI-aware via `GetDpiForMonitor`
- [x] Copy action — `Clipboard.SetDataObject(text, copy: true)` w/ 5-attempt retry on COMException
- [x] Pre-flyout HWND capture — `ForegroundWindowCapture.Capture()` stores `GetForegroundWindow()` before flyout
- [x] Tray right-click → same flyout as hotkey
- [x] Global hotkey — `Ctrl+Alt+V` default via `NHotkey.Wpf`; tray notification on failure
- [x] Settings window — list of snippets, add/edit/delete/reorder, hotkey rebind (live capture w/ Esc to cancel), "Start with Windows" toggle (HKCU `\Software\Microsoft\Windows\CurrentVersion\Run`)
- [x] Catppuccin Mocha theme — `src/TaskCopy/Themes/Mocha.xaml`; surface colors, hover/pressed states; corner radii 6–10 px (no pill backdrops)
- [x] Single-instance enforcement — `Global\TaskCopy_SingleInstance` mutex; second launch exits silently (v0.2: raise settings)
- [x] Crash log — `%LOCALAPPDATA%\TaskCopy\logs\crash.log` w/ AppDomain + Dispatcher + UnobservedTask handlers; MessageBox on Dispatcher exceptions
- [x] README — badges (version 0.1.0, MIT, Win11, .NET 10), install/build/usage steps
- [x] LICENSE (MIT), `.gitignore` (VS/C# + `CLAUDE.md` + `.claude/` + `CODEX_CHANGELOG.md`), CHANGELOG.md
- [x] Version sync — `TaskCopy.csproj` `<Version>0.1.0</Version>` + `AssemblyVersion 0.1.0.0`, README badge `0.1.0`, CHANGELOG entry `0.1.0`

**Verified:** `dotnet build -c Release` clean (0 warnings, 0 errors). `TaskCopy.exe` launches, registers tray icon, creates SQLite store, survives, terminates cleanly.

**Pending user-side validation** (cannot be done from CLI): visually confirming tray icon appearance, click-flyout interaction, hotkey trigger, Catppuccin look. Screenshots ship after first user run.

---

## v0.2.0 — Polish + auto-paste

- [ ] Auto-paste — after `SetText`, `SetForegroundWindow(savedHwnd)` + `keybd_event(VK_CONTROL down, VK_V down, VK_V up, VK_CONTROL up)`; settings toggle "Auto-paste after copy" (default ON)
- [ ] Snippet search — top-of-flyout search box; filter as you type; `Esc` closes, `Enter` copies top match
- [ ] Keyboard navigation in flyout — arrow keys, `Enter` to copy, `Esc` to dismiss
- [ ] Snippet groups / folders — `category TEXT` column + collapsible sections in flyout
- [ ] Per-snippet hotkey — optional `Ctrl+Alt+1..9` direct-copy bindings
- [ ] Snippet placeholders — `{{date}}`, `{{time}}`, `{{clipboard}}` substitution at copy time
- [ ] Import/export — JSON file (`snippets.json`) for backup + sync between machines
- [ ] Update version everywhere, capture new screenshots, ship release

---

## v0.3.0 — Optional clipboard auto-capture

- [ ] `AddClipboardFormatListener` + WndProc `WM_CLIPBOARDUPDATE` handler
- [ ] "Recent clipboard items" pinned section above curated snippets in the flyout
- [ ] Capture cap (last N items, default 50) + per-app exclude list (avoid grabbing password-manager paste)
- [ ] Settings toggle to disable capture entirely (snippet-only mode)
- [ ] Sensitive-content filter — skip items > 10 KB, skip items from `MS-EFAPI`-style apps

---

## v0.4.0 — Windhawk companion mod (literal taskbar right-click)

Optional power-user add-on. Ships as separate artifact, not bundled with the main installer.

- [ ] `windhawk/taskcopy-taskbar-menu.wh.cpp` based on `taskbar-classic-menu.wh.cpp` template
- [ ] Hook `TaskbarResources::OnTaskListButtonContextRequested` (Win11 Taskbar.View.dll) + `CTaskListWnd::_HandleContextMenu` (Win10 fallback) via `SYMBOL_HOOK` + `Wh_SetFunctionHook`
- [ ] Inject "TaskCopy ▶" menu item (with submenu populated from named-pipe IPC)
- [ ] Named-pipe server in WPF app — `\\.\pipe\TaskCopy`, JSON protocol (`list_snippets`, `copy_snippet { id }`)
- [ ] README section in repo with install steps for Windhawk + mod
- [ ] Submit PR to `ramensoftware/windhawk-mods`; self-host the `.wh.cpp` as fallback if rejected

---

## v0.5.0+ — Future ideas (not committed)

- [ ] MSIX packaging + Microsoft Store submission
- [ ] Cloud sync (encrypted, BYO bucket — S3/B2/Dropbox via user-supplied creds)
- [ ] Rich-text + image snippets (HTML, PNG via `CF_DIB`)
- [ ] Snippet templates w/ form-fill prompts (`{{ask:Recipient}}`)
- [ ] Code-signing cert + signed releases
- [ ] Light theme polish (Catppuccin Latte)

---

## Out-of-scope (decided NO, not deferred)

- **Deskband / `IDeskBand` shell extension** — removed in Win11, dead end
- **`IContextMenu` / `IShellExtInit` shell extension** — wrong surface (targets files/folders, not taskbar)
- **Overriding `Win+V`** — owned by Microsoft, low-level keyboard hook is AV-flagged
- **Fork of ExplorerPatcher** — legal issues (taskbar reimpl is GPL-2.0 + author has restricted derivative distribution)
- **Tests** — per CLAUDE.md, no tests unless explicitly requested
