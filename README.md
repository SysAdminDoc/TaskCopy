# TaskCopy

[![Version](https://img.shields.io/badge/version-0.3.0-blue)](CHANGELOG.md)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4)](https://www.microsoft.com/windows)
[![Stack](https://img.shields.io/badge/stack-.NET%2010%20%2F%20WPF-512BD4)](https://dotnet.microsoft.com)

Single-click clipboard snippet menu for Windows. Hit a hotkey or right-click the tray icon and a Catppuccin Mocha flyout pops up at your cursor with every snippet you've saved. Click one and it's copied (and optionally auto-pasted into the window you were just in).

> **About the name:** TaskCopy was originally specced as "right-click the bottom taskbar to see snippets." Windows 11's taskbar is a XAML Islands surface inside `explorer.exe` with no third-party context-menu API — direct extension requires DLL injection via [Windhawk](https://windhawk.net). The default build delivers the same UX through a tray icon + global hotkey (the same pattern Ditto and CopyQ use); a Windhawk companion mod is on the roadmap for v0.4 to add the literal taskbar trigger for power users. See [research/architecture-research.md](research/architecture-research.md) for the full rationale.

## Status

**v0.3.0** — "Snippet brain." Placeholders, groups, per-snippet quick hotkeys, frecency + pin, JSON import/export with rotated backups, monospace editor, soft-delete trash, drag-reorder, optional clipboard auto-capture, light theme (Catppuccin Latte). See [CHANGELOG.md](CHANGELOG.md) for what landed and [ROADMAP.md](ROADMAP.md) for what's next.

## Features (v0.3.0)

### Picker & paste
- **Tray icon** — left-click opens snippet flyout at the cursor; right-click opens a native Mocha/Latte menu (Open snippets / Settings / About / Quit); double-click opens Settings.
- **Global hotkey** (`Ctrl+Alt+V` default) — opens the same flyout from anywhere; rebindable in Settings (safe-fail: the previous combo stays active if a new one can't be registered).
- **Flyout search + keyboard nav** — type to filter on Title/Body; Up/Down moves the highlight; Enter copies; Esc clears the filter then closes on a second press.
- **Alt+1..9 quick-pick** in the open flyout; **Ctrl+Alt+1..9 per-snippet hotkeys** for direct copy from anywhere.
- **Auto-paste** — after copy, TaskCopy restores the previously focused window and synthesises `Ctrl+V`. Default ON; toggle in Settings.

### Snippet content
- **Placeholders** — `{{date}}` `{{date:format}}` `{{time}}` `{{time:format}}` `{{clipboard}}` `{{cursor}}` `{{ask:Field}}`. Insert-token buttons in the editor toolbar.
- **Groups** — organize snippets via the Manage groups dialog; per-snippet Group dropdown.
- **Pin to top** + flyout sort modes — Manual / Most used (pinned on top) / Recently used (pinned on top).
- **Monospace toggle** per snippet — editor switches to Cascadia Mono for code.
- **Frecency** — TaskCopy tracks `used_count` + `last_used_at` to drive Most-used / Recently-used ordering.

### Reliability & data safety
- **SQLite store** at `%LOCALAPPDATA%\TaskCopy\snippets.db` — schema migrations via `PRAGMA user_version`, `journal_mode = WAL`, FK enforcement.
- **JSON import/export** + **automatic 3-deep on-startup backup** via `VACUUM INTO`.
- **Soft-delete trash** — confirm-delete then 30-day auto-purge.
- **Single-instance handoff** via named pipe.
- **Crash log** at `%LOCALAPPDATA%\TaskCopy\logs\crash.log` with 1 MB rotation; one-click "Open log folder" / "Open data folder" in Settings.

### Optional
- **Clipboard auto-capture** (opt-in) — keeps the last ~50 plain-text clips; respects `ExcludeClipboardContentFromMonitors`.
- **Theme: Mocha / Latte / Follow system** (`AppsUseLightTheme` registry follow). Restart to apply.

### Plumbing
- **First-run welcome** seeds five example snippets and opens Settings.
- **Catppuccin Mocha or Latte** theme throughout — full dark/light parity.
- **Drag-reorder** in the Settings snippet list (or Up/Down buttons).
- **AutomationProperties** on flyout + Settings for screen readers.

## Stack

- .NET 10 WPF (single project)
- [H.NotifyIcon.Wpf 2.4.1](https://github.com/HavenDV/H.NotifyIcon) — tray icon
- [NHotkey.Wpf 3.0.0](https://github.com/thomaslevesque/NHotkey) — global hotkey
- [CommunityToolkit.Mvvm 8.4.0](https://github.com/CommunityToolkit/dotnet) — MVVM primitives
- [Microsoft.Data.Sqlite 9.0.0](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/) — snippet store

## Build

```powershell
dotnet build src\TaskCopy\TaskCopy.csproj -c Release
.\src\TaskCopy\bin\Release\net10.0-windows\TaskCopy.exe
```

The icon is checked in. To regenerate it:

```powershell
powershell -ExecutionPolicy Bypass -File tools\generate-icon.ps1
```

## Usage

1. Launch `TaskCopy.exe`. A tray icon appears with a one-time notification confirming the hotkey.
2. **Double-click the tray icon** to open Settings; add a few snippets.
3. **Right-click the tray icon** (or press **Ctrl+Alt+V**) — a Catppuccin Mocha flyout appears at the cursor with your snippets.
4. **Click a snippet** to copy it to the clipboard; the flyout closes automatically.

## License

MIT — see [LICENSE](LICENSE).
