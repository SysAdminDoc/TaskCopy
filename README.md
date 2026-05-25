# TaskCopy

[![Version](https://img.shields.io/badge/version-0.2.0-blue)](CHANGELOG.md)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4)](https://www.microsoft.com/windows)
[![Stack](https://img.shields.io/badge/stack-.NET%2010%20%2F%20WPF-512BD4)](https://dotnet.microsoft.com)

Single-click clipboard snippet menu for Windows. Hit a hotkey or right-click the tray icon and a Catppuccin Mocha flyout pops up at your cursor with every snippet you've saved. Click one and it's copied (and optionally auto-pasted into the window you were just in).

> **About the name:** TaskCopy was originally specced as "right-click the bottom taskbar to see snippets." Windows 11's taskbar is a XAML Islands surface inside `explorer.exe` with no third-party context-menu API — direct extension requires DLL injection via [Windhawk](https://windhawk.net). The default build delivers the same UX through a tray icon + global hotkey (the same pattern Ditto and CopyQ use); a Windhawk companion mod is on the roadmap for v0.4 to add the literal taskbar trigger for power users. See [research/architecture-research.md](research/architecture-research.md) for the full rationale.

## Status

**v0.2.0** — power-user core. Auto-paste, search + type-ahead, Alt+1-9 quick-pick, native tray context menu, second-instance handoff, schema migrations, first-run welcome. See [CHANGELOG.md](CHANGELOG.md) for what landed and [ROADMAP.md](ROADMAP.md) for what's next.

## Features (v0.2.0)

- **Tray icon** — left-click opens snippet flyout at the cursor; right-click opens a native Catppuccin Mocha menu (Open snippets / Settings / About / Quit); double-click opens Settings.
- **Global hotkey** (`Ctrl+Alt+V` default) — opens the same flyout from anywhere; rebindable in Settings (safe-fail: the previous combo stays active if a new one can't be registered).
- **Flyout search + keyboard nav** — type to filter on Title/Body; Up/Down moves the highlight; Enter copies; Esc clears the filter then closes on a second press.
- **Alt+1..9 quick-pick** — the first nine visible rows are numbered; `Alt+<digit>` copies that row instantly.
- **Auto-paste** — after copy, TaskCopy restores the previously focused window and synthesises `Ctrl+V`. Default ON; toggle in Settings.
- **Single-click copy to clipboard** (with COMException retry).
- **Curated snippets** — SQLite store at `%LOCALAPPDATA%\TaskCopy\snippets.db`, with schema migrations tracked via `PRAGMA user_version` and `journal_mode = WAL` for safer concurrent reads.
- **Settings window** — add / edit / delete / reorder snippets (debounced writes), rebind hotkey, "Start with Windows" toggle, "Open log folder" / "Open data folder" diagnostics buttons.
- **First-run welcome** — fresh installs get five example snippets and Settings opens automatically.
- **Second-instance handoff** — running TaskCopy.exe a second time signals the first instance via named pipe instead of dying silently; defaults to opening Settings.
- **Catppuccin Mocha dark theme** throughout.
- **Single-instance enforcement** via per-user named mutex.
- **Crash log** at `%LOCALAPPDATA%\TaskCopy\logs\crash.log`, with 1 MB rotation.

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
