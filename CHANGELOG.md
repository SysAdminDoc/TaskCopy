# Changelog

All notable changes to TaskCopy will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] — 2026-05-23

### Added
- WPF tray-resident app on **.NET 10 / WPF** with Catppuccin Mocha theme throughout
- Tray icon via `H.NotifyIcon.Wpf 2.4.1` — left-click and right-click both open the snippet flyout at the cursor; double-click opens Settings
- Global hotkey `Ctrl+Alt+V` (rebindable) via `NHotkey.Wpf 3.0.0` — opens the same flyout from anywhere
- Cursor-anchored, monitor-clamped, DPI-aware `SnippetMenuWindow` (borderless, transparent, drop-shadow, 10 px corner radius)
- Single-click copy to clipboard with COMException retry
- Pre-flyout `GetForegroundWindow()` capture, ready for v0.2 auto-paste
- SQLite snippet + settings store at `%LOCALAPPDATA%\TaskCopy\snippets.db`
- `SnippetDatabase` repository with `GetAll / Insert / Update / Delete / Reorder`
- `SettingsWindow` — add / edit / delete / reorder snippets, hotkey rebind (live capture), Start-with-Windows toggle (`HKCU\…\Run`)
- Multi-resolution PNG-encoded `app.ico` (16/32/48/64/128/256) generated via `tools/generate-icon.ps1`
- Single-instance enforcement via `Global\TaskCopy_SingleInstance` mutex
- Crash log at `%LOCALAPPDATA%\TaskCopy\logs\crash.log` (AppDomain + Dispatcher + UnobservedTask handlers)
- README + ROADMAP + research notes (`research/architecture-research.md`) explaining why we use the tray + hotkey pattern instead of literally extending the Win11 taskbar context menu

### Architecture
- The literal "right-click the taskbar" UX requires Windhawk DLL injection on Win11 — scoped for v0.4 as an optional companion mod (see [ROADMAP.md](ROADMAP.md))
- Tray + cursor flyout is the same pattern Ditto and CopyQ ship; delivers the same value with no injection, no AV-flagged hooks, and no Win11-update fragility

### Build verification
- `dotnet build -c Release` — 0 warnings, 0 errors
- Smoke test: `TaskCopy.exe` launches, registers tray icon, creates SQLite store at `%LOCALAPPDATA%\TaskCopy\snippets.db`, survives 5+ seconds, terminates cleanly
- Interactive UI exercise (click tray, open menu, copy snippet) deferred to user-side validation on first launch
