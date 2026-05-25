# TaskCopy — Research and Feature Plan

**Date:** 2026-05-24
**Branch / head:** `master` @ `61db896` (v0.1.0 ship commit)
**Reviewer:** autonomous research pass (no implementation in this pass)
**Companion to:** [`ROADMAP.md`](ROADMAP.md), [`research/architecture-research.md`](research/architecture-research.md), [`CHANGELOG.md`](CHANGELOG.md)

This file is the **second-pass research deliverable**: a deep code-grounded feature & improvement plan for a coding agent to implement next, without re-doing the discovery work. It does not duplicate the architecture decisions already captured in `research/architecture-research.md`; it builds on them.

---

## Executive Summary

TaskCopy v0.1.0 is a tight, idiomatic WPF tray utility (~1,500 LOC of C# / ~250 LOC of XAML / 1 PS script, single project, four NuGet deps) that delivers a single-click snippet flyout via tray icon and `Ctrl+Alt+V`. The architecture is sound — every choice is defended in `research/architecture-research.md` — and the v0.1.0 ship commit is genuinely complete: clean Release build, single-instance mutex, DPI-aware cursor-anchored flyout, SQLite repo, hotkey-rebind UX, Catppuccin Mocha theme. The strongest current shape is "minimal Ditto-clone for hand-curated snippets, dark-mode-first, distraction-free." The highest-value next direction is **finishing the half-built auto-paste flow + adding flyout-side search, type-ahead, keyboard nav, and number-key quick-pick** — together these turn TaskCopy from "fancy clipboard" into "muscle-memory text expander," which is the actual reason a power user keeps Ditto on their machine.

**Top 10 opportunities (priority order):**

1. **P0 — Finish auto-paste** (HWND is already captured; just restore + `SendInput` Ctrl+V). The `AutoPaste` settings checkbox already exists but is disabled. (`SettingsViewModel.cs:72`, `ForegroundWindowCapture.cs`, `App.xaml.cs:90`)
2. **P0 — Search box + type-ahead + keyboard nav in the flyout** (`SnippetMenuWindow.xaml` has none; current model.Preview/title strings are already filter-ready).
3. **P0 — Number-key quick-pick (`1`..`9`) in the flyout** (Ditto's killer feature; one event handler + label glyphs in `SnippetRow`).
4. **P0 — Add Settings / Quit / About to the tray right-click path** (today there is no way to quit without first opening the flyout; new users won't find "Quit TaskCopy"). Use `H.NotifyIcon`'s `ContextMenu` or `MenuFlyout` for native right-click and move "open snippet list" to left-click only, OR add a "Settings…" footer button to the flyout.
5. **P0 — Fix `SetHotkey` persistence-before-registration bug** (`SettingsViewModel.cs:162`): if `TryRegister` fails, the broken combo is still written to SQLite and will fail again on next launch.
6. **P1 — Snippet search + ordering by frecency / pinning / "recently used"** (groundwork for >50 snippets; `Snippet` model has no `used_count` / `last_used_at` columns yet).
7. **P1 — Second-instance signal: bring Settings forward instead of silent exit** (`App.xaml.cs:36`). A named-pipe ping is the minimal path; reuses the IPC primitive needed for v0.4 Windhawk mod.
8. **P1 — Schema versioning + JSON import/export + automatic backup** — none of these exist; the first schema change after v0.1.0 will be ad-hoc, and any disk corruption today loses the user's full snippet library.
9. **P1 — Placeholders (`{{date}}`, `{{time}}`, `{{clipboard}}`, `{{cursor}}`, `{{ask:Field}}`)** — already on the v0.2 roadmap; the substitution layer is a 30-line pure function in `ClipboardService.TryCopy` before `SetDataObject`.
10. **P2 — Distribution maturity**: signed single-file publish, MSIX, Velopack/Squirrel auto-update, README screenshots, GitHub Actions release workflow. Today a non-developer literally cannot install TaskCopy.

Below: full evidence, inventory, audits, prioritized roadmap.

---

## Evidence Reviewed

### Local files and directories inspected (every source file in v0.1.0)

- Root docs: [`README.md`](README.md), [`CLAUDE.md`](CLAUDE.md), [`ROADMAP.md`](ROADMAP.md), [`CHANGELOG.md`](CHANGELOG.md), [`LICENSE`](LICENSE), [`.gitignore`](.gitignore), [`TaskCopy.sln`](TaskCopy.sln)
- Research: [`research/architecture-research.md`](research/architecture-research.md) (the v0 architecture deliverable; reuses its competitive analysis, Win11 taskbar facts, anti-pattern list)
- Tools: [`tools/generate-icon.ps1`](tools/generate-icon.ps1)
- Project: [`src/TaskCopy/TaskCopy.csproj`](src/TaskCopy/TaskCopy.csproj), [`src/TaskCopy/App.xaml`](src/TaskCopy/App.xaml), [`src/TaskCopy/App.xaml.cs`](src/TaskCopy/App.xaml.cs)
- Data: [`Data/SnippetDatabase.cs`](src/TaskCopy/Data/SnippetDatabase.cs), [`Data/SettingsStore.cs`](src/TaskCopy/Data/SettingsStore.cs)
- Models: [`Models/Snippet.cs`](src/TaskCopy/Models/Snippet.cs)
- Services: [`Services/ClipboardService.cs`](src/TaskCopy/Services/ClipboardService.cs), [`Services/CrashLog.cs`](src/TaskCopy/Services/CrashLog.cs), [`Services/ForegroundWindowCapture.cs`](src/TaskCopy/Services/ForegroundWindowCapture.cs), [`Services/HotkeyService.cs`](src/TaskCopy/Services/HotkeyService.cs), [`Services/NativeMethods.cs`](src/TaskCopy/Services/NativeMethods.cs), [`Services/StartupService.cs`](src/TaskCopy/Services/StartupService.cs)
- ViewModels: [`ViewModels/SnippetMenuViewModel.cs`](src/TaskCopy/ViewModels/SnippetMenuViewModel.cs), [`ViewModels/SettingsViewModel.cs`](src/TaskCopy/ViewModels/SettingsViewModel.cs)
- Views: [`Views/HotkeyHostWindow.cs`](src/TaskCopy/Views/HotkeyHostWindow.cs), [`Views/SnippetMenuWindow.xaml`](src/TaskCopy/Views/SnippetMenuWindow.xaml), [`Views/SnippetMenuWindow.xaml.cs`](src/TaskCopy/Views/SnippetMenuWindow.xaml.cs), [`Views/SettingsWindow.xaml`](src/TaskCopy/Views/SettingsWindow.xaml), [`Views/SettingsWindow.xaml.cs`](src/TaskCopy/Views/SettingsWindow.xaml.cs)
- Theme: [`Themes/Mocha.xaml`](src/TaskCopy/Themes/Mocha.xaml)
- Converters: [`Converters/BoolToVisibilityConverters.cs`](src/TaskCopy/Converters/BoolToVisibilityConverters.cs)
- Assets: `src/TaskCopy/Assets/app.ico` (binary, 5,676 bytes, generated by `tools/generate-icon.ps1`)

### Git history reviewed

- Two commits total: `d3a5e74` (initial scaffold — research + roadmap), `61db896` (v0.1.0 ship — full 27-file shipping commit). Author: `Matthew Parker <matt_parker@outlook.com>`. Local-only repo, no remote configured. *Verified.*

### Build / test / docs / release artifacts inspected

- Build: `dotnet build -c Release` — clean per CLAUDE.md verification (`CLAUDE.md:55`). No artifacts on disk on this machine (mirror).
- Tests: none. Per CLAUDE.md global rule "no tests unless explicitly requested." *Verified.*
- Docs: README (build + usage), ROADMAP (v0.1 shipped + v0.2/0.3/0.4/0.5 planned), CHANGELOG (Keep-a-Changelog format, 0.1.0 entry), research/architecture-research.md (architecture decisions and rejected paths). No `docs/` content even though the directory exists; no screenshots committed; no contributors guide; no install docs for non-developers.
- Release: no GitHub Actions, no `.github/`, no published binaries, no release tags, no signing.

### External sources

The architecture-research.md document already enumerates every primary source consulted during v0 (Microsoft docs on Win11 taskbar API, Windhawk wiki, all relevant OSS projects). This research pass does **not** re-do that landscape work; it cites it where relevant and focuses on what `research/architecture-research.md` did not cover: in-app UX patterns, data model evolution, distribution. The clipboard-manager landscape (Ditto, CopyQ, ClipClip, ClipboardFusion, Win+V) and text-expansion landscape (Espanso, TextExpander, PhraseExpress, aText) are summarized below from prior shared knowledge; flag for live re-validation if any specific product capability becomes a load-bearing requirement.

### Areas that could not be verified from this environment

- **Interactive UI behavior** — tray-icon visibility, flyout look, hotkey trigger, copy/paste round-trip, settings interactivity, hotkey rebind. CLAUDE.md notes these are pending user-side. This research pass intentionally relies on code reading + standard Win32/WPF behavior; mark anything depending on observed runtime as "Needs live validation."
- **Defender / SmartScreen behavior on unsigned binary** — not tested.
- **Windhawk mod build path** — out of scope until v0.4.
- **WPF DPI scaling on per-monitor mixed DPI** — code uses `GetDpiForMonitor` (`NativeMethods.cs:53`), correct in principle, but flyout `Measure` call may underreport before content templates render. Needs live validation.

---

## Current Product Map

### Core workflows (today, v0.1.0)

1. **Open snippet flyout** → click snippet → text on clipboard.
   - Triggers: tray left-click, tray right-click, global hotkey (`Ctrl+Alt+V`).
   - Steps: capture foreground HWND → measure → DPI-correct anchor at cursor → show borderless dark popup → click row → `Clipboard.SetDataObject(text, copy: true)` with retry → close on click or `Esc` or focus loss.
2. **Curate snippets** → settings window.
   - Add / select / edit (title + body, in-place auto-save on each keystroke) / delete (no confirm) / move up / move down.
3. **Configure hotkey & autostart**.
   - Rebind hotkey via live-capture textbox; "Start with Windows" toggles a `HKCU\…\Run` value.
4. **Quit** — only from the flyout's "Quit TaskCopy" row. No tray menu, no graceful exit elsewhere.

### Existing features (one-line each)

- Tray icon (`H.NotifyIcon.Wpf`), persistent ToolTip, survives Explorer restart automatically.
- Global hotkey via `NHotkey.Wpf`, host window is a hidden 0×0 off-screen `Window` whose HWND owns the hotkey.
- Cursor-anchored, monitor-clamped, DPI-aware flyout (`SnippetMenuWindow.ShowAtCursor`).
- Catppuccin Mocha theme (single resource dictionary, ~250 lines, 6 styles).
- Single-instance enforcement via named mutex `Global\TaskCopy_SingleInstance`.
- SQLite at `%LOCALAPPDATA%\TaskCopy\snippets.db` with two tables: `snippets(id, title, body, sort_order, created_at)` and key/value `settings(key, value)`.
- Crash log at `%LOCALAPPDATA%\TaskCopy\logs\crash.log` with `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, and `DispatcherUnhandledException` handlers.
- Foreground-window capture pre-flyout (the only piece of v0.2 auto-paste already implemented).
- Settings KV store wraps four keys: hotkey key, hotkey modifiers, start-with-Windows, autopaste (read but never written by code beyond settings UI).
- Multi-resolution PNG-encoded `app.ico` (16/32/48/64/128/256) generated by a self-contained PowerShell script.

### User personas (inferred — none documented)

- **Power-user developer / sysadmin / support engineer** who pastes the same blocks ten times a day (signatures, ticket templates, code snippets, common commands). Wants speed; tolerates a single hotkey; reads ROADMAP files in repos.
- **CLAUDE.md / Claude-Code user** (the author) running on Windows 11, 125% DPI, dark theme everywhere, comfortable with `dotnet build` + PowerShell.
- **Likely future:** non-technical employee given a pre-curated snippet library (canned replies, common URLs). They will not build from source. Today's distribution story (clone repo, install .NET 10 SDK, run `dotnet build`) excludes them entirely.

### Platforms / distribution / integrations / permissions / storage

- **Platform:** Windows 11 (`net10.0-windows`). Win10 likely works but untested.
- **Distribution:** none — local build only. Not on Microsoft Store, not on GitHub Releases, not on winget, not signed.
- **Integrations:** Win32 only (`user32` / `shcore`), Win32 clipboard, `HKCU\…\Run` registry.
- **Permissions:** standard user. No admin needed. Registry writes are HKCU. Mutex is `Global\…` but only need `Local\…` for current behavior.
- **Storage:** all in `%LOCALAPPDATA%\TaskCopy\` — SQLite DB and crash log. No network, no telemetry, no cloud.

---

## Feature Inventory

| # | Feature | User value | Entry point | Main code | Maturity | Tests / docs | Improvement opps |
|---|---|---|---|---|---|---|---|
| 1 | **Tray icon (single-instance)** | Always-on launcher | `App.OnStartup` | `App.xaml.cs:69`, `H.NotifyIcon` | Complete | README + CLAUDE.md cover it | Add native context menu for Settings/Quit/About (today right-click opens flyout — discoverability of Quit is poor). |
| 2 | **Global hotkey** | Cursor-free launcher | `App.OnStartup` | `Services/HotkeyService.cs`, `Views/HotkeyHostWindow.cs` | Complete | README | (a) Persist-before-register bug (see Bugs §). (b) On registration failure provide retry UI inline instead of just a toast. (c) Detect Win+V collision specifically (cannot register; warn). |
| 3 | **Cursor-anchored flyout** | Predictable popup near pointer | tray click / hotkey | `Views/SnippetMenuWindow.xaml(.cs)` | Complete | none | (a) No search/filter. (b) No keyboard nav. (c) No number-key quick-pick. (d) No "Settings…" affordance in flyout — must double-click tray. (e) Measure-before-Show may underreport size of dynamic content; consider `UpdateLayout()` then re-measure. |
| 4 | **Single-click copy** | Zero-mouse-friction | snippet row | `Services/ClipboardService.cs` | Complete | none | (a) No success/failure surface to user. (b) Retries 5× with linearly-backed delays but does not distinguish "clipboard owner refused" from "transient" — surface a toast on permanent failure. (c) No "copy as plain text" option (snippet body is already plain text but future RTF support will need this). |
| 5 | **Pre-flyout HWND capture** | Foundation for v0.2 auto-paste | `App.ShowSnippetMenu` | `Services/ForegroundWindowCapture.cs` | **Partial — captured but never used** | none | Wire auto-paste: `SetForegroundWindow` + `SendInput` keystrokes. See `AutoPaste` setting (`SettingsStore.cs:36`, `SettingsViewModel.cs:72`) — checkbox already in Settings UI, disabled with "(v0.2 preview)". |
| 6 | **SQLite snippet store** | Persistence | `App.OnStartup` | `Data/SnippetDatabase.cs` | Complete (very simple) | none | (a) No schema versioning (`PRAGMA user_version`). (b) Each call opens a new connection — fine for current scale; ADO.NET pool helps. (c) `WAL` mode not enabled; reduces durability and parallel-read perf when sync arrives. (d) Insert `nextOrder = MAX + 1` not atomic (UI thread serializes, but still racy under any future async). (e) No FTS5 virtual table for full-text search at scale. |
| 7 | **Settings KV store** | Persisted prefs | `App.OnStartup` | `Data/SettingsStore.cs` | Complete | none | (a) `Key`/`ModifierKeys` enum names are persisted as strings — adequate but brittle to .NET enum renames; consider numeric persistence with a typed adapter. (b) `AutoPaste` getter/setter reads/writes DB on every UI binding tick — consider in-memory cache. |
| 8 | **Settings window** | Snippet CRUD + prefs | tray double-click / flyout "Edit snippets…" | `Views/SettingsWindow.xaml(.cs)`, `ViewModels/SettingsViewModel.cs` | Complete | none | (a) Every keystroke in Title/Body persists to SQLite (`UpdateSourceTrigger=PropertyChanged` → `EditTitle`/`EditBody` setters call `_db.Update`). (b) Delete has no confirm dialog and no undo. (c) Reorder is button-only — no drag. (d) No "Duplicate snippet" action. (e) Hotkey display is a `TextBlock` next to a "Rebind…" button; could be a click-to-rebind chip. (f) Auto-paste checkbox is disabled and labeled "(v0.2 preview)" — wire it once auto-paste lands. (g) No keyboard shortcuts (Del to delete, Ctrl+N to add). |
| 9 | **Hotkey rebind UX** | Live capture, Esc cancels | "Rebind…" button | `Views/SettingsWindow.xaml.cs:30` | Complete (with bugs) | none | Reject bare modifier (good); requires modifier (good); but the persisted-before-registered bug below corrupts persisted state. Also, single-key hotkeys (`F12`) cannot be assigned even when valid. |
| 10 | **Start-with-Windows** | Convenience | Settings checkbox | `Services/StartupService.cs` | Complete | none | (a) Uses `MainModule.FileName` which for self-contained MSIX/single-file is correct. (b) No "started minimized to tray" verification — Run key launches it, but if `OnStartup` toast fires every launch the user gets a daily notification (see #11). (c) Should support per-user vs all-users — today HKCU only, fine for v1. |
| 11 | **Launch toast** | First-run hint of hotkey | `App.OnStartup` | `App.xaml.cs:80` | Complete | none | Fires *every* launch; with Start-with-Windows enabled, this is a daily nag. Convert to first-run-only (track via Settings KV `firstrun.shown=1`). |
| 12 | **Crash log** | Diagnostics | `App.OnStartup` | `Services/CrashLog.cs` | Complete | none | (a) Unbounded append-only file — could grow large. Add 1 MB rotation. (b) `MessageBox` on Dispatcher exception steals foreground from whatever the user was just doing. (c) No way to "open log folder" from Settings (would aid bug reports). |
| 13 | **Catppuccin Mocha theme** | Cohesive dark look | App-wide | `Themes/Mocha.xaml` | Complete | CLAUDE.md notes "no pill backdrops, 6–10 px corners" | (a) Hardcoded — no light theme, no system-theme follow. (b) No accent-color override. (c) `Foreground` not set on `Mocha.Button` — relies on `TextElement.Foreground` inheritance via `Mocha.Text.Brush` — would benefit from explicit setter to prevent surprises on `Button.Content` of non-Text types. (d) ScrollBar style narrows to 6 px globally — fine in flyout, may be uncomfortable in Settings TextBox. |
| 14 | **Single-instance enforcement** | Prevent double-launch | `App.OnStartup` | `App.xaml.cs:35` | Complete (silent) | none | Second launch disposes mutex and dies silently — should signal the first instance to bring Settings forward (named-pipe / `SetForegroundWindow` of `_settingsWindow`). |
| 15 | **Multi-resolution `.ico` generator** | Icon for taskbar / .exe / tray | `tools/generate-icon.ps1` | manual run | Complete | README | (a) System.Drawing.Common is deprecated on non-Windows (we are Windows-only, fine). (b) Hardcoded colors duplicate Catppuccin palette in `Mocha.xaml`. (c) No CLI flag for alternate glyph / palette. (d) Not run by build — committed bytes only; could be a `<Target>` if Windows-only. |

**Hidden / disabled / undocumented features:**

- `AutoPaste` setting persisted but checkbox disabled (`SettingsWindow.xaml:131-136`).
- `ContextMenuMode="SecondWindow"` from `H.NotifyIcon` mentioned in research doc but **not used** in code — no `ContextMenu` or `ContextFlyout` is set on `TaskbarIcon`.
- `windhawk/build/` excluded in `.gitignore` but no `windhawk/` directory exists yet — placeholder for v0.4.
- Flyout footer has "Edit snippets…" and "Quit TaskCopy" rows; no Settings button labeled as such (only "Edit snippets…" maps to Settings).

---

## Competitive and Ecosystem Research

The architecture-research.md document already covers the **build-time** competitive landscape (which framework, which packages). This section focuses on **user-facing patterns** TaskCopy hasn't fully adopted yet. Categories are filtered to those whose UX maps to TaskCopy's "curated snippets" niche, not generic clipboard-history.

| Product | Source | Notable capabilities relevant here | Steal | Avoid |
|---|---|---|---|---|
| **Ditto** | [github.com/sabrogden/Ditto](https://github.com/sabrogden/Ditto) (GPL-3.0, C++/MFC) | (1) `Win+\`` opens overlay; (2) **type-ahead filters live**; (3) `1..9` paste slot; (4) Pinned + "MRU on top"; (5) Optional encryption; (6) Network sync between machines; (7) Per-clip metadata (timestamp, source app); (8) "Always copy as plain text" toggle. | All of #1–4, #6–8 over time. #2 + #3 are killer features for muscle memory. | UI is dated (MFC). Don't copy the dialog density — Catppuccin minimalism is TaskCopy's identity. |
| **CopyQ** | [github.com/hluk/CopyQ](https://github.com/hluk/CopyQ) (GPL-3.0, Qt) | (1) Lua-style scripting; (2) Tabs (multiple lists); (3) Image storage with thumbnails; (4) Per-tab commands. | "Tabs" → **Groups/Folders** (already on v0.2 roadmap; matches `category` column). | Scripting language is overkill for v0–v1; revisit only if user demand emerges. |
| **Win+V (Windows Clipboard History)** | OS built-in | (1) Pin items; (2) Cloud sync; (3) Image support; (4) Plain-text paste; (5) Search | (1), (4) immediately. (3) is v0.5 in roadmap. (5) directly drives Search opportunity. | The "all clipboards forever" model — TaskCopy's curated story is the *differentiator*. |
| **TextExpander / PhraseExpress / aText** | various, commercial | (1) Type-trigger expansion (`;sig` expands); (2) Placeholders `{date}` `{cursor}` `{ask:Name}`; (3) Per-app rules; (4) Form-fill snippets; (5) Statistics ("you saved 4 minutes today"). | (2) — already on v0.2 roadmap; pure-function substitution layer in `ClipboardService`. (5) — a tiny gimmick with high stickiness. | (1) requires keyboard hooks (`SetWindowsHookEx`) — AV-flagged, brittle; architecture-research.md already rejects. Skip. |
| **Espanso** | [github.com/espanso/espanso](https://github.com/espanso/espanso) (GPL-3.0, Rust) | YAML-defined snippets, Markdown-to-HTML, regex/date triggers, cross-platform. | YAML-defined snippet bundles (sharable, version-controllable). | Keyboard-hook trigger model — same rejection as above. |
| **Snippetstore / Lepton (Boostnote ecosystem)** | OSS | Code syntax highlighting, tags, Markdown body. | Monospace body font toggle for code snippets. Tags. | Full IDE feel — overkill. |
| **PowerToys Run / Raycast / Alfred** | various | Launcher-style picker (`Alt+Space` → type → pick). | Type-to-filter pattern: works without a visible search box. The flyout can grab keystrokes while open and filter in real time. | A second "launcher" surface — don't reinvent. The flyout *is* the launcher. |
| **H.NotifyIcon samples** | [github.com/HavenDV/H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon) | `ContextMenuMode="SecondWindow"` for modern XAML popup; `EfficiencyMode` for background CPU/IO throttling; `Wpf` examples for `ContextFlyout` binding. | Use `SecondWindow` mode for native right-click → Settings/Quit/About. Enable Efficiency Mode after a few seconds idle (we explicitly opt out today). | Default `PopupMenu` mode (looks dated). |

**Implied non-goal from competitive scan:** TaskCopy should resist becoming a clipboard-history app. The name and ROADMAP make this explicit ("snippet-only" v1, "optional clipboard auto-capture" *only* if explicitly enabled, behind v0.3). Keep that posture; "history" mode should be an opt-in feature, not the default.

---

## Highest-Value New Features

### F1 — Auto-paste (finish the v0.2 half-built path)

- **User problem:** today the snippet hits the clipboard but the user still has to switch back and `Ctrl+V`. Half the value proposition.
- **Evidence:** `Services/ForegroundWindowCapture.cs` exists and is called at `App.xaml.cs:90`. `ForegroundWindowCapture.TryRestore` exists but is never invoked. `SettingsStore.AutoPaste` is persisted (`SettingsStore.cs:36`); checkbox is in Settings UI but `IsEnabled="False"` (`SettingsWindow.xaml:135`). The whole feature is wired up to a stub.
- **Proposed behavior:** when `AutoPaste=true`, after a successful `Clipboard.SetDataObject`: (a) call `_foreground.TryRestore`, (b) sleep 30 ms (let foreground settle), (c) `SendInput` `Ctrl down, V down, V up, Ctrl up` via `INPUT` structs (modern; `keybd_event` is legacy), (d) suppress on certain known windows (TaskCopy's own flyout, settings). Setting defaults ON per `architecture-research.md` recommendation (#7.3).
- **Implementation areas:** new `Services/AutoPasteService.cs` (composes `ForegroundWindowCapture` + `SendInput` P/Invoke); `App.xaml.cs` invokes after `Copy`; `SettingsWindow.xaml` `IsEnabled="True"`; `SnippetMenuViewModel.Copy` raises a "copied" event that App handles. Add 6 `SendInput`-related entries to `NativeMethods.cs`.
- **Data model / API / UI implications:** No schema change. Settings already has `AutoPaste`. UI: enable checkbox, drop "(v0.2 preview)" label. Add a per-snippet "Copy only — do not auto-paste" flag in v0.3 if needed.
- **Risks & edges:** UAC-elevated foreground app — `SetForegroundWindow` refuses cross-elevation; we lose the paste silently. Window minimized between capture and restore — `SetForegroundWindow` returns false. Foreground was a TaskCopy window because the user clicked the flyout from a tray click while the previous app was already inactive — guard by storing only `Capture()` if the HWND is *not* in our own process. Apps that suppress `Ctrl+V` (password fields, secured apps) — accept the paste won't happen; we still left the text on the clipboard.
- **Verification plan:** (a) live: open Notepad, hit hotkey, click snippet, expect text auto-pastes; (b) edge: open elevated Notepad (Run as admin), expect graceful fallback (no crash, text on clipboard); (c) toggle off, repeat (a), expect no auto-paste; (d) capture+restore on dual-monitor.
- **Complexity:** S
- **Priority:** **P0**

### F2 — Search box + type-ahead filter in flyout

- **User problem:** Above ~15 snippets the flyout becomes a scrolling list — defeats the muscle-memory promise.
- **Evidence:** `SnippetMenuWindow.xaml` has zero filter input. `Snippet.Title`/`Body` are already plain-text observable strings; `Preview` is computed. `ItemsControl` rebinds cheap.
- **Proposed behavior:** Top row of the flyout is a `TextBox` with caret already focused. Typing filters the list to titles+body containing the query (case-insensitive, contiguous substring). Up/Down moves the highlighted row; `Enter` copies the highlighted row; `Esc` clears the filter first, then closes the flyout. Implement *type-ahead-without-focus* by handling `PreviewTextInput` on the window — if no search box is focused, redirect the keystroke to it.
- **Implementation areas:** `SnippetMenuViewModel` gets `Filter` string and `FilteredSnippets` (CollectionViewSource or hand-rolled `ObservableCollection<Snippet>`). `SnippetMenuWindow.xaml` adds a `TextBox` row above the separator. Window's `PreviewKeyDown` already handles `Esc`; extend with `Up`/`Down`/`Enter`. `Snippet.Matches(string filter)` helper on the model.
- **Data model / API / UI implications:** None at DB level. UI: 32-px taller flyout for the search field. Selection state model needed in VM.
- **Risks & edges:** Case-folding for non-ASCII; use `StringComparison.OrdinalIgnoreCase`. Very long body strings — search whole body, but cap match preview to first 80 chars same as today. Filter persists between flyout opens? — recommended: reset on every open (the flyout is "ephemeral").
- **Verification plan:** seed 30 snippets, open flyout, type "foo", expect ≤30 filtered rows immediate; ↑/↓ navigates highlight; Enter copies & closes; Esc once clears filter; Esc twice closes.
- **Complexity:** S
- **Priority:** **P0**

### F3 — Number-key quick-pick (`1`..`9`)

- **User problem:** even with type-ahead, the fastest path for the top-9 snippets is "open + press number." Ditto's most-cited feature.
- **Evidence:** `SnippetMenuWindow.xaml.cs` `OnKeyDown` only handles `Esc`. The first 9 items are visually first by `sort_order`.
- **Proposed behavior:** With flyout open, pressing `1`..`9` copies snippet #N from the **filtered** list. Each visible row gets a small leading `1.`/`2.` glyph in `Mocha.Body.Subtle` style.
- **Implementation areas:** `SnippetMenuWindow.xaml.cs` extend `OnKeyDown`. `SnippetRow` template adds a small `TextBlock` for the index. VM exposes `IndexLabel` per snippet (1..9 or empty).
- **Data model / API / UI implications:** None.
- **Risks & edges:** Conflicts with the search text input — number keys must go to picker only when the search box is empty *or* the row index is unambiguous. Recommend: number keys *always* trigger pick (search box still gets the digit if user wants to filter "1" specifically, but they can also `Shift+1`); document the tradeoff. Or: digits go to picker only when prefixed with `Alt`.
- **Verification plan:** seed 10 snippets, open flyout, press `3`, expect row 3 copied & flyout closed.
- **Complexity:** S
- **Priority:** **P0**

### F4 — Tray right-click context menu (Settings / Quit / About / Open snippets)

- **User problem:** today right-click opens the snippet flyout. There is no graceful way to open Settings (must double-click the tray) or to Quit (must open flyout then click "Quit TaskCopy"). New users won't discover either.
- **Evidence:** `App.xaml.cs:76` binds `TrayRightMouseUp` to `ShowSnippetMenu`. No `ContextMenu` or `ContextFlyout` is set on `TaskbarIcon`. `H.NotifyIcon` supports `ContextMenuMode="SecondWindow"` per its sample app.
- **Proposed behavior:** Move snippet-flyout trigger to **left-click only**; right-click opens a small native context menu: "Open snippets", "Settings…", "About", "Quit TaskCopy". (Global hotkey remains the primary "open snippets" path.) Alternatively, leave both clicks opening the flyout but add a "Settings…" footer button to the flyout itself (the current "Edit snippets…" already maps to Settings — rename to "Settings…" and add a separate "Quit").
- **Implementation areas:** `App.xaml.cs` (remove TrayRightMouseUp handler OR add ContextMenu); `Themes/Mocha.xaml` adds `Mocha.ContextMenu` + `Mocha.MenuItem` styles. If using H.NotifyIcon ContextMenuMode, no XAML — uses native popup.
- **Data model / API / UI implications:** None.
- **Risks & edges:** Users already trained on "right-click → flyout" from the README — keep README in sync. Choose the *minimal-friction* path: add Settings/About/Quit to the **flyout footer** (faster, no behavior change), and *also* set H.NotifyIcon ContextMenu for right-click (additional discoverability path). Both can coexist.
- **Verification plan:** right-click tray, expect menu with Settings/About/Quit; click Quit, expect clean shutdown; click Settings, expect settings open or restored from minimized.
- **Complexity:** S
- **Priority:** **P0**

### F5 — Placeholders (`{{date}}`, `{{time}}`, `{{clipboard}}`, `{{cursor}}`, `{{ask:Field}}`)

- **User problem:** snippets with today's date / current ticket / live values require either manual editing after paste or rote text duplication.
- **Evidence:** ROADMAP v0.2 lists this. No code today.
- **Proposed behavior:** Substitution layer in `ClipboardService` before `SetDataObject`. Supported tokens (case-insensitive, deterministic): `{{date}}` → `2026-05-24` (ISO), `{{date:format}}` → custom (`{{date:MMM d, yyyy}}` → `May 24, 2026`), `{{time}}` → `14:32:08`, `{{clipboard}}` → previous clipboard contents (captured before flyout open), `{{cursor}}` → no substitution, but final caret position is recorded; on auto-paste, send `Left` arrow keys to place caret there, `{{ask:Field}}` → small modal asks for value before paste.
- **Implementation areas:** `Services/SnippetTemplating.cs` — pure function `string Expand(string body, TemplatingContext ctx)`. `App.ShowSnippetMenu` captures `prevClipboard` (read `Clipboard.GetText()` before opening flyout). `ClipboardService.TryCopy` accepts the expanded string. For `{{ask:…}}`, a minimal `AskWindow.xaml` modal.
- **Data model / API / UI implications:** None mandatory; optionally a `is_template` flag on `snippets` to skip processing for snippets the user wants literal. Reasonable v1 rule: always run templating; tokens that aren't recognized are left as-is.
- **Risks & edges:** `{{clipboard}}` recursion if a templated snippet itself contains the same token (run only one pass). Escape syntax `\{{date}}` if user truly wants the literal text. Date locale — default to invariant ISO; let `{{date:format}}` use current culture.
- **Verification plan:** snippet body `Hi, today is {{date}}.` → copy → paste → see `Hi, today is 2026-05-24.`. `{{ask:Name}}` → modal pops, type "Pat", Enter, see "Hi Pat" pasted.
- **Complexity:** M
- **Priority:** **P1**

### F6 — Groups / folders / tags + filterable flyout sections

- **User problem:** flat snippet list becomes a swamp at 50+. CopyQ "tabs" and ClipClip "folders" both solve this.
- **Evidence:** ROADMAP v0.2 lists `category TEXT` column + collapsible sections.
- **Proposed behavior:** Two-tier model: snippets can belong to 0 or 1 group. Settings UI has a left-side group list with an "All" pseudo-group. Flyout shows a single-line group switcher (numeric `1..n` keys for groups when a modifier is held, e.g. `Ctrl+1..Ctrl+9`). Alternatively: tag-based (many-to-many) but adds a join table; group-based is simpler for v1.
- **Implementation areas:** Add `groups(id, name, sort_order)` table + nullable `group_id` on `snippets`. Schema migration logic (see F12). Settings: panel for group CRUD. Flyout: pivot row when groups exist.
- **Data model / API / UI implications:** DB schema +1 table; `Snippet` model gains `GroupId`; backward-compatible NULL = "Ungrouped".
- **Risks & edges:** Settings UI becomes denser; lay out carefully. Move-snippet-between-groups UX needs drag or right-click.
- **Verification plan:** create groups "Work" / "Personal"; assign snippets; flyout shows the group pivot; click group switches the visible list.
- **Complexity:** M
- **Priority:** **P1**

### F7 — Per-snippet hotkey (`Ctrl+Alt+1`..`9` direct copy)

- **User problem:** the global hotkey is the *gateway*; expert users want direct one-keystroke paste of their top-N snippets.
- **Evidence:** ROADMAP v0.2 lists `Ctrl+Alt+1..9` direct-copy.
- **Proposed behavior:** Settings: per-snippet "Quick hotkey" picker (none / Ctrl+Alt+1 / Ctrl+Alt+2 / … / Ctrl+Alt+9). On match, copy + auto-paste (using the same code path as flyout pick). Limit to a curated set to avoid global keyboard conflicts.
- **Implementation areas:** `snippets` table gains `quick_hotkey TEXT NULL` (or smallint 1..9). `HotkeyService.RegisterMany` to register 0..9 additional hotkeys. Persist on snippet save.
- **Data model / API / UI implications:** Schema +1 column; uniqueness constraint per quick-hotkey slot.
- **Risks & edges:** registration failure for one slot must not block other slots. Display registration errors in Settings inline.
- **Verification plan:** assign `Ctrl+Alt+1` to snippet A; press it from anywhere → A pasted.
- **Complexity:** M
- **Priority:** **P1**

### F8 — Frecency / Pin / "Recently used" ordering

- **User problem:** static `sort_order` doesn't reflect what you *actually* use. Top-of-list deserves your top-used items.
- **Evidence:** `Snippet` has no `last_used_at` / `used_count`. Today the flyout is purely `sort_order`-driven.
- **Proposed behavior:** Track `used_count` + `last_used_at` on each copy. Settings has a "Sort flyout by" radio: "Manual order", "Most used first", "Recently used first", "Pinned + recent". A "Pin to top" action on a snippet promotes it above unpinned items in any mode.
- **Implementation areas:** Schema migration `ALTER TABLE snippets ADD COLUMN used_count INTEGER NOT NULL DEFAULT 0; … last_used_at INTEGER; pinned INTEGER NOT NULL DEFAULT 0`. `SnippetDatabase.Copy(long id)` increments + timestamps. VM sort.
- **Data model / API / UI implications:** Schema +3 columns; settings KV `flyout.sort_mode`.
- **Risks & edges:** With "Most used first" the muscle-memory-by-position promise breaks every few uses. Keep "Manual order" as default. Frecency formula (decay-weighted) better than raw count for stable feel: `score = count * exp(-(now - last_used_at) / τ)`, τ = 7 days.
- **Verification plan:** flip to "Most used"; copy snippet B several times; reopen → B at top.
- **Complexity:** M
- **Priority:** **P1**

### F9 — JSON import/export + automatic on-startup backup

- **User problem:** today the entire snippet library lives in one SQLite file with zero copies. Any corruption = total loss. Also blocks cross-machine seeding.
- **Evidence:** ROADMAP v0.2 lists "Import/export — JSON file for backup + sync between machines." No backup code. No DB integrity check.
- **Proposed behavior:** Settings → "Export snippets…" writes `snippets-YYYYMMDD.json` (chosen via `Microsoft.Win32.SaveFileDialog`). "Import…" merges (skip duplicates by title-hash, or "replace all" toggle). Also: on every app startup, copy `snippets.db` → `snippets.db.bak.{0..2}` (3-deep rotation) before opening for writes.
- **Implementation areas:** `Services/SnippetIO.cs` — `Export(string path)`, `Import(string path, ImportMode mode)`. `App.OnStartup` calls `BackupRotator.Rotate(dbPath)` before opening DB. JSON format: `{ version: 1, snippets: [{ title, body, sort_order, created_at, group, ... }] }`.
- **Data model / API / UI implications:** None to schema. Add Settings UI section "Backup & Sync".
- **Risks & edges:** Import collisions — choose "skip-by-title" as default. Backup of an open SQLite file should use `VACUUM INTO` for transactional snapshot, not raw `File.Copy`.
- **Verification plan:** export → JSON has all snippets with bodies; delete DB; import → snippets restored. Crash mid-write of DB; reopen, expect backup rollback path available.
- **Complexity:** M
- **Priority:** **P1**

### F10 — Schema versioning + forward migrations

- **User problem:** v0.2 will add columns and v0.3 will add tables. Without `PRAGMA user_version` and migration code, every upgrade is risky for users with existing DBs.
- **Evidence:** `SnippetDatabase.EnsureSchema` is `CREATE TABLE IF NOT EXISTS` only — no version tracking. No `PRAGMA user_version` set anywhere.
- **Proposed behavior:** On open, read `PRAGMA user_version`. If `0`, run v1 schema (current). If `1`, run v2 migrations (e.g. add columns from F7/F8/F6). Each migration is idempotent and wrapped in a transaction. Set `PRAGMA user_version=N` at end.
- **Implementation areas:** `Data/Migrations/` folder; `SnippetDatabase.RunMigrations()` invoked from constructor. Each migration a static class.
- **Data model / API / UI implications:** Adds discipline; supports future evolution. Surface migration status in crash log.
- **Risks & edges:** Failed migration must leave DB in pre-migration state — use `BEGIN; … COMMIT;`. Keep migrations append-only; never edit a shipped migration.
- **Verification plan:** start with v0.1 DB, run v0.2 build, expect new columns present, `user_version=2`, snippets intact.
- **Complexity:** S
- **Priority:** **P1**

### F11 — Second-instance handoff (bring Settings forward, optionally open flyout)

- **User problem:** running the .exe twice (double-click, taskbar shortcut, autostart + manual launch) silently does nothing. Looks broken.
- **Evidence:** `App.xaml.cs:36` — `if (!createdNew) { Shutdown(0); }`.
- **Proposed behavior:** First instance creates a named pipe `\\.\pipe\TaskCopy` and a listener. Second instance: if mutex taken, connect to pipe, send `"open-settings"` or `"open-flyout"` (CLI arg-driven), then exit. First instance handles message on UI thread.
- **Implementation areas:** New `Services/SingleInstanceServer.cs` (`NamedPipeServerStream` + JSON line protocol). Update `App.OnStartup`. CLI: `TaskCopy.exe --settings`, `TaskCopy.exe --flyout`.
- **Data model / API / UI implications:** Sets the IPC primitive needed by the v0.4 Windhawk mod (already planned in ROADMAP). Two birds.
- **Risks & edges:** Pipe creation race on startup — accept that very early second-launch may still see no listener and exit silently; that's fine. Anti-virus heuristics around named pipes — modern Defender doesn't flag them.
- **Verification plan:** start app; double-click exe; expect Settings to come forward (or focus if open).
- **Complexity:** S
- **Priority:** **P1**

### F12 — First-run welcome (seed 3-5 example snippets + hotkey hint)

- **User problem:** today first launch shows an empty flyout that says "No snippets yet. Open Edit snippets to add your first one." Zero discoverability of placeholders, search, hotkey rebind, or what "Edit snippets" leads to.
- **Evidence:** `SnippetMenuWindow.xaml:91-98` empty state. `App.OnStartup` shows a transient toast about the hotkey on *every* launch.
- **Proposed behavior:** On first launch only (`firstrun.shown=0`), populate the DB with 3-5 example snippets ("Hello! Your name here.", "{{date}}", "https://", a code block, a signature line) and open Settings rather than just showing the toast. Then set `firstrun.shown=1`. Also: the recurring toast becomes first-run-only.
- **Implementation areas:** `App.OnStartup` checks `_settings.IsFirstRun` (new property in `SettingsStore`). Seed list in a constant.
- **Data model / API / UI implications:** Settings KV: `firstrun.shown`. Optional `system.seeded_version` for re-seeding when shipping new examples.
- **Risks & edges:** Power users may dislike auto-seeded snippets — offer "Skip welcome" in the launch toast; if they delete the seeds, never re-add. The toast "every launch" annoyance is a separate fix.
- **Verification plan:** delete `%LOCALAPPDATA%\TaskCopy`, launch, expect Settings opens with seeded snippets.
- **Complexity:** S
- **Priority:** **P2**

### F13 — Signed single-file publish + GitHub Actions release workflow + README screenshots + winget manifest

- **User problem:** today nobody who isn't a .NET developer can install TaskCopy. Defender SmartScreen will flag the unsigned binary. The README references screenshots that don't exist (CLAUDE.md notes them pending).
- **Evidence:** No `.github/`. `<PublishSingleFile>false</PublishSingleFile>` in `.csproj`. No screenshots in repo. CLAUDE.md "Ship screenshots" recipe explicitly notes screenshots pending.
- **Proposed behavior:** (a) `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true` to produce a single-exe (still needs .NET 10 desktop runtime — fine if linking to runtime installer in README) OR `--self-contained true` for fully portable. (b) GitHub Actions workflow `release.yml` triggered on tags `v*` → build → publish → upload `.exe` + `.zip` to Release. (c) Cap screenshots to `assets/screenshots/` and link from README. (d) Once signing cert acquired (budget per architecture-research.md §7.8), add `signtool` step. (e) winget manifest in [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).
- **Implementation areas:** New `.github/workflows/release.yml`. `assets/screenshots/`. README rewrite for install section. csproj edits.
- **Data model / API / UI implications:** None.
- **Risks & edges:** Self-contained single-file ships ~70 MB of WPF runtime — acceptable but worth noting. Tray icon GUID persistence (mentioned in architecture-research.md §7.8) requires Authenticode signing for stability across path changes — until signed, accept that users may see a duplicate tray icon if they move the .exe.
- **Verification plan:** tag `v0.2.0` → workflow uploads `TaskCopy-0.2.0-win-x64.zip`; download → unzip → run → tray works.
- **Complexity:** L
- **Priority:** **P2**

### F14 — Snippet preview pane + monospace body toggle

- **User problem:** the flyout shows title + first 80 chars only. Long snippets are blind-picks. Code snippets in a proportional font are unreadable.
- **Evidence:** `Snippet.Preview` truncates at 80 chars (`Models/Snippet.cs:28`). `SettingsWindow` `TextBox` for body uses `Mocha.Font` (Segoe UI Variable Text / Segoe UI) — proportional.
- **Proposed behavior:** (a) Hovering a row in the flyout shows the full body in a side `ToolTip`-style panel (or expand-on-hover row). (b) Settings adds a per-snippet "Monospace body" checkbox; when set, the body editor switches to `Cascadia Mono` (already on Win11). Also flyout preview wraps at 8-12 lines.
- **Implementation areas:** Tooltip `Style` in `Mocha.xaml`. `Snippet.IsMonospace` column. Settings UI gains a small toggle next to body.
- **Data model / API / UI implications:** Schema +1 column.
- **Risks & edges:** Tooltip can flicker when moving over a list — use a custom popup with hover delay 350 ms.
- **Verification plan:** seed a 20-line snippet; hover → see full body in popup; toggle monospace → editor switches font.
- **Complexity:** M
- **Priority:** **P2**

### F15 — Optional clipboard auto-capture (v0.3 ROADMAP item with safety)

- **User problem:** sometimes users want clipboard *history* alongside curated snippets.
- **Evidence:** ROADMAP v0.3 already plans this with `AddClipboardFormatListener`. Caveat already there: sensitive-content filter, exclude lists.
- **Proposed behavior:** Off by default. When on, a separate `recent_clips(id, body, copied_at, source_app)` table records the last 50 clipboard text items. Flyout shows "Recent" section above curated snippets. Per-app exclude (skip when source = `1Password.exe`, etc.). Skip items > 10 KB. Honor `Clipboard.IsCurrentFormatContent(ExcludeClipboardContentFromMonitors)` MIME flag (Windows convention used by password managers — `CF_PRIVATE` / "ExcludeFromClipboardHistory" format).
- **Implementation areas:** `Services/ClipboardWatcher.cs` (registers via `HotkeyHostWindow`'s HWND), new table, flyout pivot.
- **Data model / API / UI implications:** +1 table. Settings: enable/disable, per-app excludes, cap N.
- **Risks & edges:** Capturing password-manager content would be a security incident — must respect `ExcludeClipboardContentFromMonitors`. EFAPI / DRM apps may still leak; default exclude list for known password managers.
- **Verification plan:** enable; copy text from Notepad → appears in flyout's "Recent"; copy from 1Password → does not appear.
- **Complexity:** M
- **Priority:** **P2**

### F16 — Light / system-theme follow + accent override

- **User problem:** Catppuccin Mocha is the author's preference, but a Light-system user will be surprised; a screen-share in a meeting room with bright lighting will be glaring.
- **Evidence:** `Themes/Mocha.xaml` is the only theme. Hardcoded everywhere.
- **Proposed behavior:** Add `Themes/Latte.xaml` (Catppuccin Latte palette). Settings: "Theme: Auto (system) / Mocha / Latte". On startup, read `HKCU\…\Themes\Personalize\AppsUseLightTheme`. Listen for `WM_SETTINGCHANGE` to live-swap.
- **Implementation areas:** Replace hardcoded `Themes/Mocha.xaml` merge in `App.xaml` with runtime swap. Both palettes use the same XAML resource keys.
- **Data model / API / UI implications:** Settings KV: `theme`.
- **Risks & edges:** Settings KV migration to Latte first run on a Light system. Hotkey rebind capture still works in both.
- **Verification plan:** flip OS to Light, restart, expect Latte; manual override to Mocha persists across launches.
- **Complexity:** M
- **Priority:** **P3** (nice-to-have; original architectural direction is Mocha-first)

### F17 — Velopack / Squirrel.Windows in-app auto-update

- **User problem:** v0.2/0.3 will ship faster than users will manually re-download.
- **Evidence:** No update mechanism.
- **Proposed behavior:** [Velopack](https://github.com/velopack/velopack) (modern Squirrel successor, MIT, .NET 8+). On startup, async check `https://github.com/SysAdminDoc/TaskCopy/releases/latest`. If newer, prompt in Settings ("Update available — restart to install"). Velopack handles diff downloads and atomic restart.
- **Implementation areas:** NuGet `Velopack` (~1 MB). `App.OnStartup` async update check. Settings UI a "Check for updates" button + "Updates installed automatically" toggle.
- **Data model / API / UI implications:** None to schema. Release workflow must produce Velopack manifest.
- **Risks & edges:** Requires HTTPS network — first-run firewall prompt. Update check should never block startup.
- **Verification plan:** stage v0.2.1 release; launch v0.2.0; expect toast + Settings shows update.
- **Complexity:** M (Velopack hides most complexity)
- **Priority:** **P3**

---

## Existing Feature Improvements

### I1 — `SetHotkey` persists settings *before* checking registration result

- **Current behavior:** `SettingsViewModel.SetHotkey` (`ViewModels/SettingsViewModel.cs:162-178`) calls `_settings.HotkeyKey = key; _settings.HotkeyModifiers = modifiers;` *unconditionally*, then `if (_hotkeys.TryRegister(...))` — but the failure branch only updates `StatusMessage`. The bad combo is now persisted; next launch reads it and fails to register again, and the old working combo is gone.
- **Problem:** silent corruption of user state on registration failure.
- **Recommended change:** Order: `TryRegister` first → on success, persist; on failure, restore previous combo to `HotkeyKey`/`HotkeyModifiers`, do NOT touch `_settings.*`, and re-register the previous combo.
- **Code locations:** `src/TaskCopy/ViewModels/SettingsViewModel.cs:162-178`.
- **Backward compat:** none — pure bug fix.
- **Verification:** attempt to bind `Win+V` (reserved); expect previous hotkey still active and persisted.
- **Complexity:** XS
- **Priority:** **P0**

### I2 — `EditTitle` / `EditBody` write to SQLite on every keystroke

- **Current behavior:** `EditTitle` and `EditBody` setters call `_db.Update(...)` immediately (`SettingsViewModel.cs:39, 53`), bound to TextBox with `UpdateSourceTrigger=PropertyChanged`. Editing a 5 KB snippet body issues a SQLite write per character.
- **Problem:** unnecessary I/O; risks lock contention as DB grows; mediocre engineering hygiene.
- **Recommended change:** Debounce via a `DispatcherTimer` (300 ms idle) before writing. Or commit on focus-lost / selection-change / window-close. Or both.
- **Code locations:** `src/TaskCopy/ViewModels/SettingsViewModel.cs:31-57`.
- **Backward compat:** behaviorally identical from user perspective; just fewer disk writes.
- **Verification:** open Settings → type rapidly into a body field → observe disk I/O via Process Monitor: 1 write per ~300 ms instead of per keystroke.
- **Complexity:** S
- **Priority:** **P1**

### I3 — `Deactivated` handler closes the flyout even when the user clicks Settings

- **Current behavior:** `SnippetMenuWindow` constructor (`Views/SnippetMenuWindow.xaml.cs:18`): `Deactivated += (_, _) => Close();`. If the flyout opens, then `Edit snippets…` raises `EditRequested` which closes flyout *and* opens Settings — fine. But the "Quit" button raises `QuitRequested` which calls `Shutdown` — also fine. Edge case: clicking the flyout while focus is elsewhere can race the Deactivated close.
- **Problem:** intermittent "flyout flashes and disappears" reports likely; also breaks any future right-click context menu *inside* the flyout (e.g. for per-snippet actions).
- **Recommended change:** Use a global mouse hook OR track click-through with `WS_EX_NOACTIVATE`. Simpler: only auto-close on `Deactivated` if the **active** HWND is *not* the snippet menu's owner chain and *not* a TaskCopy-owned window. Reference Ditto's pattern (its overlay survives until explicit dismissal or click outside via mouse hook).
- **Code locations:** `Views/SnippetMenuWindow.xaml.cs:18`.
- **Backward compat:** behavior change — flyout no longer closes when user briefly tabs to another window. Acceptable if validated.
- **Verification:** open flyout, briefly alt-tab to another window → flyout *should* close (current). open flyout → start typing → flyout *should* stay open and accept type-ahead (post-F2). Tune accordingly.
- **Complexity:** S
- **Priority:** **P1** (tied to F2 search work)

### I4 — Crash log unbounded; no log rotation, no "Open log folder" button

- **Current behavior:** `CrashLog.Write` appends forever (`Services/CrashLog.cs:42-45`). No rotation. No way for user to find or share the log.
- **Recommended change:** When `crash.log` exceeds 1 MB, rename to `crash.log.1` (overwriting any prior `.1`) and start a fresh file. Add Settings → "Diagnostics" section with "Open log folder" (`Process.Start("explorer.exe", LogDirectory)`).
- **Code locations:** `Services/CrashLog.cs`, `Views/SettingsWindow.xaml`, `ViewModels/SettingsViewModel.cs`.
- **Backward compat:** none.
- **Verification:** delete `crash.log`, install with seeded errors, observe rotation at 1 MB.
- **Complexity:** S
- **Priority:** **P2**

### I5 — Launch toast fires on every launch ("TaskCopy is running")

- **Current behavior:** `App.OnStartup` always shows the "TaskCopy is running" notification (`App.xaml.cs:80-83`). With Start-with-Windows on, this nags daily.
- **Recommended change:** Show only on first run (`SettingsStore.IsFirstRun`); after that, suppress unless `HotkeyService.TryRegister` fails (in which case the warning toast is the existing failure path — already wired). Optional setting "Show launch notification" defaulting to off.
- **Code locations:** `src/TaskCopy/App.xaml.cs:80-83`, `Data/SettingsStore.cs`.
- **Backward compat:** behavior change. Document in CHANGELOG.
- **Verification:** restart → no toast; first launch on fresh `%LOCALAPPDATA%\TaskCopy` → toast appears.
- **Complexity:** XS
- **Priority:** **P2**

### I6 — Delete is destructive without confirm or undo

- **Current behavior:** `DeleteSnippet` immediately drops the row (`ViewModels/SettingsViewModel.cs:114-124`). No prompt, no trash.
- **Recommended change:** Add a confirm dialog with "Don't ask again" checkbox (persisted to `delete.confirm`). Better: soft-delete by adding `deleted_at INTEGER NULL`; show a "Trash" tab in Settings; auto-purge after 30 days.
- **Code locations:** `ViewModels/SettingsViewModel.cs:114-124`; schema (with F10 migrations).
- **Backward compat:** with soft-delete, schema migration needed.
- **Verification:** delete a snippet → confirm prompt → "Cancel" → snippet still present. "OK" with don't-ask → no future prompts.
- **Complexity:** S (confirm) / M (trash)
- **Priority:** **P2**

### I7 — Settings list has no drag-reorder

- **Current behavior:** only ↑/↓ buttons for reorder (`Views/SettingsWindow.xaml:60-61`). Painful for >10 snippets.
- **Recommended change:** Add drag-handle visual + WPF `Drag*` events on the `ListBox` items, persist new order via `_db.Reorder`. WPF lacks built-in `ListBox` drag-reorder; either hand-roll (~80 lines) or pull in a tiny helper.
- **Code locations:** `Views/SettingsWindow.xaml`, `Views/SettingsWindow.xaml.cs`, `ViewModels/SettingsViewModel.cs` (`Reorder` already exists in `SnippetDatabase`).
- **Backward compat:** none.
- **Verification:** drag snippet C above snippet A; reopen Settings; order persists.
- **Complexity:** M
- **Priority:** **P2**

### I8 — Snippet editor area lacks "monospace toggle" / line numbers / "Insert placeholder" buttons

- **Current behavior:** plain `TextBox` with `AcceptsReturn=True, AcceptsTab=True`. Proportional font. No clue about template syntax.
- **Recommended change:** (a) Title-bar inside the editor with small buttons: "Insert {{date}}", "Insert {{clipboard}}", "Insert {{ask:…}}". (b) Optional monospace toggle (see F14). (c) Char/line counter in the status bar.
- **Code locations:** `Views/SettingsWindow.xaml`, `ViewModels/SettingsViewModel.cs`.
- **Backward compat:** additive.
- **Verification:** click "Insert {{date}}" → editor caret position gains the token; status bar reflects new char count.
- **Complexity:** S
- **Priority:** **P2** (paired with F5)

### I9 — `Mocha.Body` `TextWrapping=NoWrap` + `TextTrimming=CharacterEllipsis` truncates snippet titles silently

- **Current behavior:** `Mocha.Body` style (`Themes/Mocha.xaml:44-50`) applies `NoWrap` + ellipsis globally. Long titles are cut without tooltip.
- **Recommended change:** Add a `ToolTip="{Binding Title}"` on the title `TextBlock` when truncated. Alternatively, allow titles to wrap in the Settings list (extra row when needed).
- **Code locations:** `Views/SnippetMenuWindow.xaml:74-78`, `Views/SettingsWindow.xaml:50-51`.
- **Backward compat:** none.
- **Verification:** add snippet with 120-char title; hover row → tooltip shows full title.
- **Complexity:** XS
- **Priority:** **P3**

### I10 — No `AutomationProperties` / no `AutomationPeer` on flyout — accessibility regression

- **Current behavior:** flyout has no `AutomationProperties.Name`/`HelpText`; rows are unlabeled `Button`s. Narrator reads them as "button" only.
- **Recommended change:** Add `AutomationProperties.Name="{Binding Title}"` and `AutomationProperties.HelpText="{Binding Preview}"` on row buttons. Set `AutomationProperties.AutomationId` on key controls. Provide an explicit screen-reader entry-point announcement when the flyout opens.
- **Code locations:** `Views/SnippetMenuWindow.xaml`, `Views/SettingsWindow.xaml`.
- **Backward compat:** none.
- **Verification:** run Narrator (`Ctrl+Win+Enter`), open flyout, navigate rows — each row reads title + preview.
- **Complexity:** S
- **Priority:** **P2**

### I11 — `Insert` race in `SnippetDatabase` (`MAX(sort_order)` not atomic with `INSERT`)

- **Current behavior:** `GetMaxSortOrder` then `INSERT` in two separate statements without transaction (`Data/SnippetDatabase.cs:71-86`). Today it's serialized by single-threaded UI access; will bite the moment we go async (F11 second-instance, watcher, etc.).
- **Recommended change:** Wrap in a transaction: `BEGIN; SELECT COALESCE(MAX(sort_order),-1)+1; INSERT(...) RETURNING id; COMMIT;` using `RETURNING id` to avoid the `last_insert_rowid()` race entirely.
- **Code locations:** `Data/SnippetDatabase.cs:71-86`, `125-130`.
- **Backward compat:** none.
- **Verification:** parallelize 100 inserts; expect 100 distinct sort_orders.
- **Complexity:** XS
- **Priority:** **P3** (defensive; no observed bug today)

### I12 — `Global\` mutex prefix when `Local\` is enough

- **Current behavior:** mutex name `Global\TaskCopy_SingleInstance` (`App.xaml.cs:15`). `Global\` namespace is shared across user sessions on the same machine, requiring `SeCreateGlobalPrivilege` (held by interactive users normally, but blocked in some kiosk/RDS contexts).
- **Recommended change:** Use `Local\TaskCopy_SingleInstance` — per-user-session, which matches the per-user nature of TaskCopy's storage (`LocalApplicationData`).
- **Code locations:** `App.xaml.cs:15`.
- **Backward compat:** none — first launch on upgrade creates new mutex name; old name has no effect.
- **Verification:** RDS / kiosk environment launches without `SecurityException`.
- **Complexity:** XS
- **Priority:** **P3**

### I13 — `H.NotifyIcon` `EfficiencyMode` opted **off** explicitly (`enablesEfficiencyMode: false`)

- **Current behavior:** `_trayIcon.ForceCreate(enablesEfficiencyMode: false);` (`App.xaml.cs:78`).
- **Problem:** TaskCopy is a long-lived background process. Win11 Efficiency Mode is *designed* for exactly this case (throttles CPU + memory priority when idle). The library author recommends `true`.
- **Recommended change:** Enable Efficiency Mode. Verify no impact on hotkey latency (Efficiency Mode does not affect message-pump priority for foreground events).
- **Code locations:** `App.xaml.cs:78`.
- **Backward compat:** none.
- **Verification:** observe Task Manager → TaskCopy.exe → "Efficiency Mode" leaf icon.
- **Complexity:** XS
- **Priority:** **P3**

### I14 — `Snippet.Preview` splits on `'\n'` only — Mac/CR-only files render as one line

- **Current behavior:** `Models/Snippet.cs:28` → `Body.Split('\n', 2)[0]`. If body uses bare `\r`, no split, preview includes raw CRs.
- **Recommended change:** Split on `IndexOfAny(['\r', '\n'])` and take the prefix.
- **Code locations:** `Models/Snippet.cs:22-30`.
- **Complexity:** XS
- **Priority:** **P3**

### I15 — No "About" surface; no version display anywhere in-app

- **Current behavior:** Version is in `TaskCopy.csproj` and CHANGELOG and README badge. In-app: nothing.
- **Recommended change:** Add an About row to the tray menu or Settings footer showing `Assembly.GetEntryAssembly()?.GetName().Version`, a small "Visit GitHub" link, and link to `LICENSE`.
- **Code locations:** `Views/SettingsWindow.xaml`, optional `Views/AboutWindow.xaml`.
- **Complexity:** XS
- **Priority:** **P3**

---

## Reliability, Security, Privacy, and Data Safety

### Bugs and risks

- **B1 (P0):** `SetHotkey` persists invalid combos on registration failure — see I1.
- **B2 (P1):** `Deactivated → Close` race; the flyout's focus discipline is too aggressive — see I3. Will break Search (F2) unless fixed alongside.
- **B3 (P1):** `Capture` may grab TaskCopy's own HWND if the user opens the flyout from an already-deactivated state (tray click from an inactive window) — guard against same-process foreground.
- **B4 (P1):** every Title/Body keystroke writes to SQLite — I2.
- **B5 (P2):** Crash log unbounded — I4.
- **B6 (P3):** `Insert` non-atomic sort_order — I11.
- **B7 (P3):** `Global\` mutex over-broad — I12.
- **B8 (Assumption):** flyout `Measure` before `Show` may underreport — confirm via runtime test.

### Missing guardrails

- **No `PRAGMA journal_mode=WAL`** — default `DELETE` mode means each write fsyncs the rollback journal; survives crashes but slower under future async load. WAL is the standard recommendation for SQLite under reads-while-writes.
- **No `PRAGMA foreign_keys=ON`** — moot today (no FKs) but becomes load-bearing as soon as F6 (groups) and F15 (recent_clips) ship.
- **No DB integrity check on startup** — add `PRAGMA quick_check;`, on fail surface a "Snippet store may be corrupted — restore from backup?" dialog.
- **No version-aware schema migration** — F10.
- **No automatic backup before write** — F9.
- **No way for user to find their data folder** — open-folder button in Settings → "Diagnostics".

### Permission / network / filesystem concerns

- **No network usage today.** Velopack update check (F17) introduces first outbound connection — add Settings toggle to disable.
- **`HKCU\…\Run`** registry write — already user-scope; correct.
- **Clipboard reads** for auto-paste's `{{clipboard}}` token (F5) — must respect `ExcludeClipboardContentFromMonitors` MIME flag (F15) to avoid grabbing password-manager contents.
- **`SendInput`** for auto-paste (F1) — purely synthesis; doesn't require elevation; but cannot inject into elevated foregrounds (intentional, security feature). Surface gracefully.
- **No PII collection** today; keep it that way. If telemetry ever ships, make it opt-in, document, no defaults-on.

### Recovery & rollback needs

- **Backup of `snippets.db`** before any schema migration (F10) and on rotating basis (F9). `VACUUM INTO 'backup.db'` is the SQLite-correct snapshot under live writes.
- **Crash log → ship-back path**: a "Copy diagnostics" button that bundles `crash.log` + DB version + app version to clipboard for issue reports.

### Logging & diagnostics

- Today: `crash.log` only; no info-level events.
- Recommend: a separate `events.log` (rotated 1 MB) when a `LogLevel=Verbose` setting is on. Capture: hotkey register/unregister, schema migrations applied, settings changes (key+old+new), auto-paste attempts (target HWND class+title). Not on by default; users enable when debugging.

---

## UX, Accessibility, and Trust

### Onboarding gaps

- **First-run empty state** is just text — no CTA, no examples — see F12.
- **README has no screenshot** — see F13.
- **No "what is TaskCopy?" 30-second tour** in Settings.
- **Hotkey toast** explains the hotkey but disappears in 5 seconds; no "where do I see it again?" affordance (Settings shows it but new users don't know that).

### Empty / loading / error / disabled states

- **Empty flyout** ("No snippets yet") — currently text only. Should also link to "Open Settings" via a button.
- **No loading state** anywhere (DB ops are sync + sub-millisecond, fine for v1).
- **Hotkey registration failure** surfaces as a `NotificationIcon.Warning` toast — good. Add an inline banner in Settings ("Your hotkey couldn't be registered — try another combo") when failure is sticky.
- **Disabled state** of the Auto-paste checkbox today shows "(v0.2 preview)" — good UX honesty. Remove when shipping F1.

### Destructive or irreversible actions

- **Delete snippet** — see I6 (no confirm, no undo).
- **Hotkey rebind that fails** — see I1 (silently overwrites working hotkey).
- **"Quit TaskCopy"** — no confirm; if user accidentally clicks the row, they lose hotkey + tray until they relaunch. Could prompt; or move quit to a less-prominent location (tray menu) and remove from the flyout (it doesn't belong there long-term).

### Settings clarity

- **"Start with Windows"** is clear.
- **"Auto-paste"** label includes "(v0.2 preview)" — good.
- **"Global hotkey" → "Rebind…" button** works but doesn't preview the captured combo before commit; consider showing "Captured: Ctrl+Shift+F12" + "Apply / Cancel" two-step.
- **Hotkey field is not focusable for click-to-rebind** — power-user shortcut.
- **No "Reset to defaults" button** anywhere.
- **No "Backup now / Restore from backup" buttons** (until F9).
- **No "Open log folder" / "Open data folder"** (until I4 + diagnostics).

### Accessibility issues

- **No `AutomationProperties`** anywhere — see I10.
- **No keyboard nav in flyout** — see F2/F3.
- **Color-only state cues** in some control templates (hover only changes background to a slightly lighter neutral — `Surface0` vs `Mocha.Body.Subtle` foreground; check WCAG AA contrast).
- **Catppuccin Mocha Subtext0** (#A6ADC8) on Base (#1E1E2E) — ratio ~9.5:1 (passes AAA). Good.
- **Mocha.Subtext0 on Mocha.Mantle** — also passes. Good.
- **Button hover/pressed states distinguished by background only** — acceptable, but a 1-px focus outline (`Mocha.Mauve.Brush`) should appear on keyboard focus.
- **No `IsTabStop` strategy** — verify keyboard tabbing in Settings reaches all controls.

### Microcopy and trust signals

- **"TaskCopy is running"** toast on every launch is noisy — I5.
- **"No snippets yet. Open Edit snippets to add your first one."** — "Edit snippets" doesn't exist as a label; the menu item is "Edit snippets…". Use exact label or link.
- **"Auto-paste after copy" in Settings** — describe what window it pastes *into* (e.g. "into the window that was focused before you opened TaskCopy").
- **No version anywhere in-app** — I15.
- **No link to GitHub / Issues** — I15.

---

## Architecture and Maintainability

### Module / boundary improvements

- **Pure DI absence.** `App.xaml.cs` `new`s up every service; works for ~7 services, will get unwieldy at ~15. Consider `Microsoft.Extensions.DependencyInjection` (single NuGet, tiny). Defer until F1+F2+F5 land; the cost is small but premature today.
- **`ViewModels` ↔ `Views` boundary** is clean except `SettingsWindow.xaml.cs` capturing hotkey input via `PreviewKeyDown` and calling `_vm.SetHotkey` directly. Acceptable WPF idiom. If you migrate to MVVM Toolkit's messenger, route via that.
- **`HotkeyHostWindow` shadow window** is invisible *but* a real WPF Window — it counts toward `MainWindow`. Subtle: if a future feature relies on `Application.Current.MainWindow` being the Settings window, surprise. Document the choice with a comment (it's already half-documented in the constructor).
- **No interfaces.** Concrete classes everywhere. Fine for v1; introduce `IClipboardService`, `ISnippetRepository`, `IHotkeyService` when you start writing tests or want to swap implementations (e.g., for the Windhawk IPC bridge).

### Refactor candidates

- **`SnippetDatabase`** — single class doing schema + CRUD + settings KV. As features land, split: `Data/Schema.cs` (migrations), `Data/SnippetsRepository.cs`, `Data/SettingsRepository.cs`, `Data/RecentClipsRepository.cs`. Reuse a single long-lived `SqliteConnection` (currently per-call). Defer until F6 or F15.
- **`App.xaml.cs`** is ~160 LOC already and holds startup + tray wiring + flyout orchestration + settings orchestration + quit. Will grow with auto-paste, second-instance handoff, update check. Split orchestration into `Services/TrayOrchestrator.cs` once it crosses ~250 LOC.

### Test gaps

- **Zero tests** — per CLAUDE.md rule (no override). When that rule changes, the highest-value tests are: snippet templating (F5) pure function, `SnippetIO` round-trip (F9), schema migrations (F10), `Snippet.Preview` truncation (I14), hotkey persistence error paths (I1).

### Documentation gaps

- **No screenshots** in repo (CLAUDE.md acknowledges).
- **No `docs/` content** even though directory exists.
- **No `CONTRIBUTING.md`** — the project is private/MIT but if it goes public, contributors will want code-style guidance.
- **README has no install path for non-developers** — F13.
- **No keyboard shortcut reference** in README.

### Release, build, deployment

- **No CI** — F13.
- **No release artifacts** — F13.
- **No signing** — architecture-research.md §7.8 already flags this.
- **No MSIX / Microsoft Store** — ROADMAP v0.5.
- **No auto-update** — F17.
- **No winget manifest** — F13.

---

## Prioritized Roadmap

The order roughly maps to TaskCopy's existing ROADMAP.md (v0.2 → 0.5) but interleaves the bug-fix and reliability items appropriately so the existing roadmap stays the user-facing source of truth. The ROADMAP and this file should be cross-referenced when shipping each item.

### Phase A — v0.2.0 "Power-user core" (P0-only)

- [ ] **P0 — F1: Finish auto-paste**
  - Why: doubles user-perceived value; half the feature already exists.
  - Evidence: `Services/ForegroundWindowCapture.cs`, `SettingsStore.AutoPaste`, disabled checkbox.
  - Touches: new `Services/AutoPasteService.cs`; `Services/NativeMethods.cs` (add `SendInput`/`INPUT`); `App.xaml.cs` (post-copy); `Views/SettingsWindow.xaml` (enable checkbox, drop preview label); `ViewModels/SettingsViewModel.cs`.
  - Acceptance: with AutoPaste on, picking a snippet from the flyout while Notepad is the prior foreground results in the text appearing in Notepad with no further user action.
  - Verify: manual — Notepad open; hotkey; click snippet; expect text in Notepad. Edge: elevated Notepad; expect no crash, text on clipboard.

- [ ] **P0 — F2: Search + type-ahead in flyout**
  - Why: scales the flyout past ~15 snippets.
  - Evidence: `SnippetMenuWindow.xaml` has no filter; `SnippetMenuViewModel.Snippets` is the only collection.
  - Touches: `ViewModels/SnippetMenuViewModel.cs` (Filter, FilteredSnippets, SelectedIndex); `Views/SnippetMenuWindow.xaml` (TextBox row); `Views/SnippetMenuWindow.xaml.cs` (key handling + selection navigation).
  - Acceptance: typing in flyout filters live; Up/Down moves highlight; Enter copies highlighted; Esc once clears filter, twice closes.
  - Verify: seed 30 snippets; type "foo"; expect filtered list; Enter copies top match.

- [ ] **P0 — F3: Number-key quick-pick `1`..`9`**
  - Why: completes Ditto parity for muscle-memory paste.
  - Evidence: `OnKeyDown` only handles Esc.
  - Touches: `Views/SnippetMenuWindow.xaml.cs`, `Themes/Mocha.xaml` (index glyph), `ViewModels/SnippetMenuViewModel.cs` (IndexLabel).
  - Acceptance: pressing `3` in an open flyout copies the third visible row.
  - Verify: seed 10 snippets; open; press `3`; expect 3rd copied & flyout closed.

- [ ] **P0 — F4: Tray right-click → Settings/Quit/About context menu**
  - Why: discoverability of Quit and Settings.
  - Evidence: `App.xaml.cs:69-78` no ContextMenu; only flyout exposes Quit.
  - Touches: `App.xaml.cs` (`ContextMenu` or H.NotifyIcon `ContextMenuMode`); `Themes/Mocha.xaml` (MenuItem style).
  - Acceptance: right-click tray icon → menu with Open snippets / Settings… / About / Quit; flyout footer also gets Settings + Quit re-labeled.
  - Verify: right-click; click Quit → process exits cleanly; click Settings → window opens.

- [ ] **P0 — I1: Fix `SetHotkey` persist-before-register bug**
  - Why: prevents user from locking themselves out of TaskCopy.
  - Evidence: `ViewModels/SettingsViewModel.cs:162-178`.
  - Touches: `ViewModels/SettingsViewModel.cs`.
  - Acceptance: failed hotkey registration does not persist the new combo and leaves the previous combo working.
  - Verify: attempt `Win+V` (reserved); previous hotkey still active after attempt; restart app; previous hotkey still active.

### Phase B — v0.2.1 / v0.3.0 "Snippet brain" (P1)

- [ ] **P1 — F5: Placeholders / templating**
  - Why: completes the v0.2 ROADMAP item; major productivity multiplier.
  - Evidence: ROADMAP v0.2 line.
  - Touches: new `Services/SnippetTemplating.cs`; `App.xaml.cs` (capture prevClipboard); `ClipboardService.TryCopy`; optional new `Views/AskWindow.xaml`.
  - Acceptance: body `Today is {{date}}.` copies as `Today is 2026-05-24.`. `{{ask:Name}}` pops modal, value substituted before paste.
  - Verify: manual round-trip for each token.

- [ ] **P1 — F10: Schema versioning + migration framework**
  - Why: F6/F7/F8/F15 all add columns or tables. Land the framework before adding the columns.
  - Evidence: `SnippetDatabase.EnsureSchema`.
  - Touches: `Data/SnippetDatabase.cs`, new `Data/Migrations/*.cs`.
  - Acceptance: `PRAGMA user_version` set; idempotent migrations; old DBs upgrade cleanly.
  - Verify: start with v0.1 DB; new build runs migrations; `user_version` reflects target.

- [ ] **P1 — F6: Groups / folders**
  - Why: scaling past 50 snippets.
  - Touches: schema (+`groups` table, `snippets.group_id`); Settings UI panel; flyout pivot.
  - Acceptance: create groups; assign snippets; flyout shows group switcher when >0 groups exist.

- [ ] **P1 — F7: Per-snippet quick hotkey `Ctrl+Alt+1..9`**
  - Why: top-N snippets become one-keystroke pastes.
  - Touches: schema (+`quick_hotkey`); `HotkeyService` (multi-register); Settings UI per-snippet picker.
  - Acceptance: assign hotkey to snippet → pressing the hotkey anywhere copies + auto-pastes that snippet.

- [ ] **P1 — F8: Frecency / Pin / "Recent" ordering**
  - Why: surfaces high-use snippets without breaking manual order users.
  - Touches: schema (+`used_count`,`last_used_at`,`pinned`); VM sort.
  - Acceptance: ordering modes selectable in Settings; pin/unpin from row context.

- [ ] **P1 — F9: Import/export + automatic on-startup backup**
  - Why: data safety + cross-machine seeding.
  - Touches: new `Services/SnippetIO.cs`; `App.OnStartup` backup; Settings UI section.
  - Acceptance: export → JSON; import → snippets restored; daily backup rotated 3-deep.

- [ ] **P1 — F11: Second-instance handoff (named-pipe)**
  - Why: stops silent exit on double-launch; reuses the IPC primitive needed by v0.4 Windhawk mod.
  - Touches: new `Services/SingleInstanceServer.cs`; `App.OnStartup`.
  - Acceptance: second launch with `--settings` brings Settings window forward.

- [ ] **P1 — I2: Debounce snippet editor writes**
  - Why: removes per-keystroke disk I/O.
  - Touches: `ViewModels/SettingsViewModel.cs`.
  - Acceptance: rapid typing → at most 1 SQLite write per 300 ms (verify via Process Monitor).

- [ ] **P1 — I3: Tame `Deactivated → Close` so F2/F3 don't fight it**
  - Why: dependency for F2.
  - Touches: `Views/SnippetMenuWindow.xaml.cs`.

### Phase C — v0.3.0 "Polish + reliability" (P2)

- [ ] **P2 — F15: Optional clipboard auto-capture (Recent clips)**
  - Touches: `Services/ClipboardWatcher.cs`; new `recent_clips` table; flyout pivot.

- [ ] **P2 — F12: First-run welcome (seed examples + open Settings)**
  - Touches: `App.OnStartup`, seeds in code, `SettingsStore.IsFirstRun`.

- [ ] **P2 — F14: Snippet preview pane + monospace toggle**
  - Touches: hover popup in flyout; `snippets.is_monospace`; editor font swap.

- [ ] **P2 — I4: Crash log rotation + "Open log folder" button**
  - Touches: `Services/CrashLog.cs`, Settings UI.

- [ ] **P2 — I5: First-run-only launch toast**
  - Touches: `App.xaml.cs`, `SettingsStore`.

- [ ] **P2 — I6: Confirm-delete + soft-delete (Trash)**
  - Touches: `ViewModels/SettingsViewModel.cs`, schema (`deleted_at`).

- [ ] **P2 — I7: Drag-reorder in Settings**
  - Touches: `Views/SettingsWindow.xaml(.cs)`.

- [ ] **P2 — I8: "Insert {{date}} / {{clipboard}} / {{ask}}" buttons in editor**
  - Touches: `Views/SettingsWindow.xaml`.

- [ ] **P2 — I10: AutomationProperties for screen readers**
  - Touches: `Views/SnippetMenuWindow.xaml`, `Views/SettingsWindow.xaml`.

- [ ] **P2 — F13: GitHub Actions release workflow + README screenshots + (eventually signed) installer + winget manifest**
  - Why: makes TaskCopy distributable to non-developers.

### Phase D — v0.4.0 "Power-user surfaces" (existing ROADMAP plus P3 polish)

- [ ] **v0.4** — Windhawk companion mod (existing ROADMAP item; depends on F11 IPC primitive).
- [ ] **P3 — F16: Light / system-theme follow.**
- [ ] **P3 — F17: Velopack in-app auto-update.**
- [ ] **P3 — I9: Tooltip on truncated titles.**
- [ ] **P3 — I11: Atomic `Insert` (transaction + `RETURNING id`).**
- [ ] **P3 — I12: `Local\` mutex.**
- [ ] **P3 — I13: Enable Efficiency Mode.**
- [ ] **P3 — I14: `Snippet.Preview` split on `\r` or `\n`.**
- [ ] **P3 — I15: About surface w/ version + link to GitHub + LICENSE.**

---

## Quick Wins

Low-risk, hours-not-days items a coding agent can pick up in any free slot:

1. **I1** — `SetHotkey` order fix (XS). Pure correctness.
2. **I5** — First-run-only launch toast (XS). Quality of life.
3. **I12** — `Local\` mutex (XS). Forward compat.
4. **I13** — Enable Efficiency Mode (XS). One arg flip.
5. **I14** — Preview split on `\r|\n` (XS). One-line fix.
6. **I15** — About entry (XS). One link + version.
7. **F4** — Tray right-click context menu via H.NotifyIcon (S). Big discoverability win.
8. **F12** — First-run welcome (S). Big onboarding win.

---

## Larger Bets

Multi-day items needing design choice + staged rollout:

- **F1 (Auto-paste)** — requires `SendInput`, foreground races, elevation edge cases, settings UX. Modest LOC, real edge-case surface area.
- **F6 (Groups) + F8 (Frecency) + F7 (Per-snippet hotkey)** — together they reshape the snippet model and the flyout UX. Build the schema-migration framework (F10) first, then layer.
- **F9 (Import/export + backup)** — design the JSON schema with future fields (groups, quick_hotkey, pinned) reserved so v0.3 imports never break.
- **F13 (Distribution)** — single-file publish, signing budget, MSIX vs portable zip vs winget — pick one canonical install path before splitting effort.
- **v0.4 Windhawk mod** — biggest bet by ROI uncertainty. Defer until F1+F2+F11 ship and there's a stable IPC primitive to reuse.

---

## Explicit Non-Goals

- **Clipboard-history-as-default.** Stay snippet-curated. (F15 is opt-in.)
- **Cross-platform.** Win11 only (mentioned in architecture-research.md; no value in maintaining a Mac/Linux fork).
- **Cloud sync as a SaaS.** If sync ships (post-v0.4), it should be BYO bucket (S3/B2/Dropbox via user creds), already noted in ROADMAP v0.5.
- **Keyboard-hook trigger expansion** (Espanso/TextExpander-style `;sig` typing). architecture-research.md already rejects this (AV-flagged, brittle). Stay picker-based.
- **`Win+V` augmentation.** Reserved by Microsoft; already rejected.
- **Adding tests in this pass.** Per CLAUDE.md rule.
- **Touching `research/architecture-research.md`.** That doc is the v0 deliverable; this file is the v0.2-prep companion.

---

## Open Questions

Only the questions a coding agent can't answer by reading code:

1. **Auto-paste default — ON or OFF?** architecture-research.md §7.3 recommends ON. F1 plan defaults to ON. Confirm before merge — surprising "TaskCopy just typed something into my window" on first run is a worse first impression than "I have to Ctrl+V."
2. **Distribution path — portable zip vs MSIX vs both?** F13 needs a directional pick. Recommend: portable single-file zip for v0.2 (lowest friction), MSIX + winget for v0.3.
3. **Code-signing cert budget.** Required for stable tray-icon identity and for SmartScreen warm-up. Recommend: defer until v0.3 (post-validation that the app has external users).
4. **Public push timing.** README says "PUBLIC, MIT" but no remote configured. Most of the F13 release work assumes a GitHub remote exists. Pick the timing — first push could go before F1 (so F1 lands publicly) or after F1+F2+F3 (so the public v0.2.0 is the first impression).

---

*End of report. This file is intended as the durable handoff for the next coding pass; ROADMAP.md remains the user-facing source of truth and should be updated to cite this file's section IDs as items land.*
