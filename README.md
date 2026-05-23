# TaskCopy

[![Version](https://img.shields.io/badge/version-0.1.0--planning-blue)](CHANGELOG.md)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4)](https://www.microsoft.com/windows)
[![Stack](https://img.shields.io/badge/stack-.NET%209%20%2F%20WPF-512BD4)](https://dotnet.microsoft.com)

Single-click clipboard snippet menu for Windows. Hit a hotkey or right-click the tray icon and a Catppuccin Mocha flyout pops up at your cursor with every snippet you've saved. Click one and it's copied (and optionally auto-pasted into the window you were just in).

> **About the name:** TaskCopy was originally specced as "right-click the bottom taskbar to see snippets." Windows 11's taskbar is a XAML Islands surface inside `explorer.exe` with no third-party context-menu API — direct extension requires DLL injection via [Windhawk](https://windhawk.net). The default build delivers the same UX through a tray icon + global hotkey (the same pattern Ditto and CopyQ use); a Windhawk companion mod is on the roadmap for v0.4 to add the literal taskbar trigger for power users. See [research/architecture-research.md](research/architecture-research.md) for the full rationale.

## Status

**Pre-implementation.** Architecture researched, roadmap drafted. Building from v0.1.0 next.

See [ROADMAP.md](ROADMAP.md) for the phased plan.

## Planned features (v0.1.0)

- Tray icon with right-click → snippet flyout at cursor
- Global hotkey (`Ctrl+Alt+V` default) → same flyout
- SQLite-backed snippet store at `%LOCALAPPDATA%\TaskCopy\snippets.db`
- Settings window: add/edit/delete/reorder snippets, rebind hotkey, start-with-Windows
- Catppuccin Mocha dark theme
- Single-instance + crash log

## Stack

- .NET 9 WPF (single project)
- [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) — tray icon w/ modern XAML flyout
- [NHotkey.Wpf](https://github.com/thomaslevesque/NHotkey) — global hotkey
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM primitives
- [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/) — snippet store

## Build (once v0.1.0 lands)

```powershell
dotnet build src\TaskCopy\TaskCopy.csproj -c Release
dotnet run --project src\TaskCopy\TaskCopy.csproj
```

## License

MIT — see [LICENSE](LICENSE).
