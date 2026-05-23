# TaskCopy — Roadmap

Single-click clipboard snippet manager for Windows. Right-click the tray icon (or hit a hotkey) to open a flyout near the cursor with your stored snippets; click one to copy + auto-paste into the previously focused window.

**Stack:** C# / .NET 9 WPF · H.NotifyIcon.Wpf · NHotkey.Wpf · SQLite · CommunityToolkit.Mvvm · Catppuccin Mocha
**Reality check:** literally extending the Windows 11 taskbar right-click menu requires Windhawk DLL injection. The tray + cursor-flyout pattern delivers the same UX with zero injection. A Windhawk companion mod can add the literal taskbar trigger as a v0.2+ power-user add-on.

---

## v0.1.0 — Minimal viable snippet menu (target: first delivery)

Goal: open a popup near the cursor, see a list of snippets, click to copy.

- [ ] Project scaffold — `TaskCopy.sln`, `src/TaskCopy/TaskCopy.csproj` (`net9.0-windows`, `UseWPF=true`, `WindowsAppSDK`-free WPF), `.editorconfig`, NuGet refs (H.NotifyIcon.Wpf, NHotkey.Wpf, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite)
- [ ] App icon — 256×256 + 16/32/48 ICO at `src/TaskCopy/Assets/app.ico`; tray-icon variant w/ alpha
- [ ] Tray host — `TaskbarIcon` from `H.NotifyIcon.Wpf` w/ `ContextMenuMode="SecondWindow"` for modern XAML popup (not native `TrackPopupMenuEx`)
- [ ] Snippet store — SQLite at `%LOCALAPPDATA%\TaskCopy\snippets.db`; table `snippets(id INTEGER PK, title TEXT, body TEXT, sort_order INTEGER, created_at INTEGER)`
- [ ] Repository layer — `SnippetRepository` w/ `GetAll() / Add() / Update() / Delete() / Reorder()`
- [ ] Flyout view — `SnippetMenuView` (WPF `Window`, `WindowStyle=None`, `AllowsTransparency=true`, drop-shadow, Catppuccin Mocha) listing snippets w/ title; hover-highlight, single-click copies
- [ ] Cursor-anchored positioning — open flyout above/left of cursor; clamp to monitor work area
- [ ] Copy action — `Clipboard.SetText(body)` w/ COMException retry loop (3 attempts, 50 ms backoff)
- [ ] Pre-flyout HWND capture — `GetForegroundWindow()` before show, so auto-paste later (v0.2) targets the right window
- [ ] Tray right-click → same flyout as hotkey
- [ ] Global hotkey — `Ctrl+Alt+V` default via `NHotkey.Wpf`; first-run banner if `RegisterHotKey` returns false
- [ ] Settings window — list of snippets, add/edit/delete/reorder, hotkey rebind, "Start with Windows" toggle (HKCU `\Software\Microsoft\Windows\CurrentVersion\Run`)
- [ ] Catppuccin Mocha theme — ResourceDictionary at `src/TaskCopy/Themes/Mocha.xaml`; surface colors, hover/pressed states; honor "no pill backdrops" rule (4–8 px corner radii only)
- [ ] Single-instance enforcement — named mutex `Global\TaskCopy_SingleInstance`; second launch raises settings window
- [ ] Crash log — `%LOCALAPPDATA%\TaskCopy\logs\crash.log`, unhandled exception handler writes + shows MessageBox
- [ ] README — badges (version, license MIT, platform Win11), screenshots, install/build steps
- [ ] LICENSE (MIT), `.gitignore` (VS/C# standard + `CLAUDE.md` + `.claude/` + `CODEX_CHANGELOG.md`), CHANGELOG.md
- [ ] Version sync — `TaskCopy.csproj` `<Version>0.1.0</Version>`, `AssemblyVersion`, README badge, CHANGELOG entry

**Definition of done for v0.1.0:** clean build (`dotnet build -c Release`), launch on a fresh 125% DPI Win11 box, add 3 snippets via settings, hit hotkey + right-click tray, click a snippet, paste with Ctrl+V into Notepad and confirm match. Screenshots captured.

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
