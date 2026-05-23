# TaskCopy

[![Version](https://img.shields.io/badge/version-0.1.0-blue)](CHANGELOG.md)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4)](https://www.microsoft.com/windows)
[![Stack](https://img.shields.io/badge/stack-.NET%2010%20%2F%20WPF-512BD4)](https://dotnet.microsoft.com)

Single-click clipboard snippet menu for Windows. Hit a hotkey or right-click the tray icon and a Catppuccin Mocha flyout pops up at your cursor with every snippet you've saved. Click one and it's copied (and optionally auto-pasted into the window you were just in).

> **About the name:** TaskCopy was originally specced as "right-click the bottom taskbar to see snippets." Windows 11's taskbar is a XAML Islands surface inside `explorer.exe` with no third-party context-menu API — direct extension requires DLL injection via [Windhawk](https://windhawk.net). The default build delivers the same UX through a tray icon + global hotkey (the same pattern Ditto and CopyQ use); a Windhawk companion mod is on the roadmap for v0.4 to add the literal taskbar trigger for power users. See [research/architecture-research.md](research/architecture-research.md) for the full rationale.

## Status

**v0.1.0** — clean Release build, app launches in tray, SQLite snippet store + settings UI shipped. See [CHANGELOG.md](CHANGELOG.md) for what landed and [ROADMAP.md](ROADMAP.md) for what's next.

## Features (v0.1.0)

- Tray icon — right-click (or left-click) opens snippet flyout at the cursor; double-click opens settings
- Global hotkey (`Ctrl+Alt+V` default) — opens the same flyout from anywhere
- Single-click copy to clipboard (with COMException retry)
- Pre-flyout HWND capture (ready for v0.2 auto-paste)
- SQLite snippet store at `%LOCALAPPDATA%\TaskCopy\snippets.db`
- Settings window — add / edit / delete / reorder snippets, rebind hotkey, "Start with Windows" toggle
- Catppuccin Mocha dark theme throughout
- Single-instance enforcement via named mutex
- Crash log at `%LOCALAPPDATA%\TaskCopy\logs\crash.log`

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
