# TaskCopy

[![Version](https://img.shields.io/badge/version-0.5.9-blue)](CHANGELOG.md)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4)](https://www.microsoft.com/windows)
[![Stack](https://img.shields.io/badge/stack-.NET%2010%20%2F%20WPF-512BD4)](https://dotnet.microsoft.com)

Single-click clipboard snippet menu for Windows. Hit a hotkey or right-click the tray icon and a Catppuccin Mocha flyout pops up at your cursor with every snippet you've saved. Click one and it's copied (and optionally auto-pasted into the window you were just in).

> **About the name:** TaskCopy was originally specced as "right-click the bottom taskbar to see snippets." Windows 11's taskbar is a XAML Islands surface inside `explorer.exe` with no third-party context-menu API — direct extension requires DLL injection via [Windhawk](https://windhawk.net). The default build delivers the same UX through a tray icon + global hotkey (the same pattern Ditto and CopyQ use); a Windhawk companion mod remains a future power-user add-on for the literal taskbar trigger. See [research/architecture-research.md](research/architecture-research.md) for the full rationale.

## Status

**v0.5.9** — syntax-highlighted code editor. Monospace snippets now use AvalonEdit with line numbers and built-in highlighting in Settings, while normal snippets keep the plain text editor. The v0.5 line also includes opt-in shell placeholders, image snippets, reusable form prompts, Espanso YAML import, encrypted backups, per-app rules, multi-clip paste, edit history, usage stats, sticky flyout position, high-contrast mode, external editor integration, and GitHub issue filing. See [CHANGELOG.md](CHANGELOG.md) for the full list and [ROADMAP.md](ROADMAP.md) for what's next.

## Features (v0.5.9)

### Picker & paste
- **Tray icon** — left-click opens snippet flyout at the cursor; right-click opens a native Mocha/Latte menu (Open snippets / Settings / About / Quit); double-click opens Settings.
- **Global hotkey** (`Ctrl+Alt+V` default) — opens the same flyout from anywhere; rebindable in Settings (safe-fail: the previous combo stays active if a new one can't be registered).
- **Flyout search + keyboard nav** — type to filter on Title/Body; Up/Down moves the highlight; Enter copies; Esc clears the filter then closes on a second press. Fuzzy/prefix-weighted ranking so `sig` finds `Email signature` before `design rationale`.
- **Alt+1..9 quick-pick** in the open flyout; **per-snippet hotkeys** (any combo, e.g. `Ctrl+Alt+S`) for direct copy + auto-paste from anywhere.
- **Group chips** in the flyout when ≥1 group is defined — click to filter, persists across opens.
- **Auto-paste** — after copy, TaskCopy restores the previously focused window and synthesises `Ctrl+V`. Per-snippet "Type characters" mode for apps that swallow `Ctrl+V` (legacy terminals, RDP sessions, password fields). Default ON; toggle in Settings.

### Snippet content
- **Placeholders** — `{{date}}` `{{date:format}}` `{{time}}` `{{time:format}}` `{{clipboard}}` `{{cursor}}` `{{ask:Field}}` `{{form:Field1|Field2}}`, plus opt-in `{{shell:cmd}}`. A form token prompts once and reuses values through matching `{{ask:Field}}` tokens. Pipe-chained transforms: `{{clipboard|upper}}`, `{{clipboard|trim|lower}}`, `{{clipboard|jsonpretty}}`, `{{clipboard|urldecode}}`, `{{clipboard|base64decode}}`, `{{clipboard|sha256}}`. Live preview in the editor.
- **Groups** — organize snippets via the Manage groups dialog; per-snippet Group dropdown; flyout chip strip.
- **Image snippets** — Settings → Add image captures the current clipboard image into an explicit image snippet, shows thumbnails in Settings and the flyout, and copies/pastes the image back when picked. Background clipboard capture remains text-only.
- **Pin to top** + flyout sort modes — Manual / Most used (decay-weighted frecency, pinned on top) / Recently used (pinned on top).
- **Monospace/code mode** per snippet — Settings switches to an AvalonEdit code editor with line numbers and syntax highlighting; flyout tooltips use Cascadia Mono for aligned code.

### Reliability & data safety
- **SQLite store** at `%LOCALAPPDATA%\TaskCopy\snippets.db` — schema migrations via `PRAGMA user_version`, `journal_mode = WAL`, FK enforcement, startup `PRAGMA quick_check` integrity check with one-click restore.
- **JSON / `.taskpack` / Espanso YAML import** + **JSON export**. JSON export round-trips text and image snippets; Espanso static `matches:` entries become text snippets while unsupported dynamic matches are skipped. Automatic on-startup backup uses `VACUUM INTO` (3-deep rotation, throttled to once per 24 h).
- **Restore from backup…** — Settings → Diagnostics lists the available snapshots and swaps them in atomically (with a pre-restore rollback snapshot in case the user changes their mind).
- **Soft-delete trash** — confirm-delete then 30-day auto-purge. Trash window in Settings shows trashed snippets with per-row Restore / Delete Permanently / Empty Trash.
- **Single-instance handoff** via named pipe; subsequent launches focus an existing window or take a CLI directive.
- **Crash log** at `%LOCALAPPDATA%\TaskCopy\logs\crash.log` with 1 MB rotation; one-click "Open log folder" / "Open data folder" / "Copy diagnostics" in Settings.

### Optional
- **Clipboard auto-capture** (opt-in) — keeps the last ~50 plain-text clips and shows them in a flyout "Recent" section above your snippets. Respects `ExcludeClipboardContentFromMonitors` and reads the 4-byte `CanIncludeInClipboardHistory` payload correctly (0 = exclude, 1 = include).
- **Theme: Mocha / Latte / Follow system** (`AppsUseLightTheme` registry follow). Live swap via prompt-to-relaunch.

### Plumbing
- **First-run welcome** seeds five generic example snippets (no signature identity leakage) and opens Settings.
- **Catppuccin Mocha or Latte** theme throughout — full dark/light parity with keyboard focus outlines.
- **Drag-reorder** in the Settings snippet list (or Up/Down buttons).
- **AutomationProperties** on flyout + Settings for screen readers.
- **Per-monitor DPI v2** manifest for sharp rendering across mixed-DPI setups.

## Stack

- .NET 10 WPF (single project)
- [H.NotifyIcon.Wpf 2.4.1](https://github.com/HavenDV/H.NotifyIcon) — tray icon
- [NHotkey.Wpf 3.0.0](https://github.com/thomaslevesque/NHotkey) — global hotkey
- [CommunityToolkit.Mvvm 8.4.0](https://github.com/CommunityToolkit/dotnet) — MVVM primitives
- [Microsoft.Data.Sqlite 9.0.0](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/) — snippet store
- [YamlDotNet 16.1.0](https://github.com/aaubry/YamlDotNet) — Espanso YAML import
- [AvalonEdit 6.3.1](https://github.com/icsharpcode/AvalonEdit) — code editor

## Install

### Prebuilt binary (recommended)
1. Grab the latest `TaskCopy-<version>-win-x64.zip` from [Releases](https://github.com/SysAdminDoc/TaskCopy/releases).
2. Unzip somewhere persistent (e.g. `%LOCALAPPDATA%\Programs\TaskCopy\`).
3. Double-click `TaskCopy.exe`. The first launch shows a SmartScreen "Don't run" prompt because the binary is unsigned — click **More info** → **Run anyway**. (We're working on a signing cert.)
4. Optional: enable **Start TaskCopy with Windows** in Settings.

The single-file build needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) on the box. If you'd rather download a fully self-contained binary (~80 MB, no runtime required), grab `TaskCopy-<version>-win-x64-selfcontained.zip` instead.

### winget
```powershell
winget install SysAdminDoc.TaskCopy
```
(Once the package is approved into the public manifest repo.)

### Build from source
```powershell
git clone https://github.com/SysAdminDoc/TaskCopy.git
cd TaskCopy
dotnet build src\TaskCopy\TaskCopy.csproj -c Release
.\src\TaskCopy\bin\Release\net10.0-windows\TaskCopy.exe
```

The icon is checked in. To regenerate it:

```powershell
powershell -ExecutionPolicy Bypass -File tools\generate-icon.ps1
```

## Usage

1. Launch `TaskCopy.exe`. A tray icon appears with a one-time notification confirming the hotkey.
2. **Double-click the tray icon** (or pick "Settings…" from the right-click menu) to open Settings; add a few snippets.
3. **Right-click the tray icon** (or press **Ctrl+Alt+V**) — a Catppuccin flyout appears at the cursor with your snippets.
4. **Click a snippet** (or press its number key) to copy + auto-paste it into the window you were just in.

## Keyboard cheatsheet

| Where | Key | What it does |
|---|---|---|
| Anywhere | `Ctrl+Alt+V` *(default)* | Open the snippet flyout at the cursor |
| Anywhere | Your per-snippet hotkey | Copy + auto-paste that snippet directly |
| Flyout | type | Filter snippets live |
| Flyout | `↑` / `↓` | Move selection |
| Flyout | `PgUp` / `PgDn` | Jump 8 rows |
| Flyout | `Enter` | Copy the highlighted row |
| Flyout | `Esc` *(once)* | Clear the filter |
| Flyout | `Esc` *(twice)* | Close the flyout |
| Flyout | `Alt+1`..`Alt+9` | Pick the matching visible row |
| Settings list | `Del` | Delete the selected snippet |
| Settings list | `Ctrl+N` | Add a new snippet |
| Settings list | `F2` | Focus the Title editor (rename) |
| Settings list | drag | Reorder snippets |
| Tray | left-click | Open snippet flyout at cursor |
| Tray | right-click | Open the tray context menu |
| Tray | double-click | Open Settings |

## CLI flags

TaskCopy is a single-instance app. Launching the exe a second time signals the first instance:

```powershell
TaskCopy.exe                 # default: bring Settings forward
TaskCopy.exe --settings      # explicit Settings
TaskCopy.exe --flyout        # open the snippet picker at the cursor
TaskCopy.exe --copy <id|title>   # copy that snippet to the clipboard (no paste)
TaskCopy.exe --paste <id|title>  # copy + auto-paste into the foreground window
TaskCopy.exe --list          # write all snippets as "id\tTitle" lines to %LOCALAPPDATA%\TaskCopy\snippets.list
```

Useful from PowerToys Run, Flow Launcher, the Win+R dialog, or any task scheduler.

## License

MIT — see [LICENSE](LICENSE).
