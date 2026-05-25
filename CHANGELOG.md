# Changelog

All notable changes to TaskCopy will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] — 2026-05-24

### Added
- **Auto-paste (F1)** — after copying a snippet, TaskCopy restores the previously
  focused window and synthesises Ctrl+V via `SendInput`. Settings checkbox now
  enabled; defaults ON for new installs (existing explicit "0" still respected).
- **Flyout search + type-ahead (F2)** — `TextBox` row at the top of the flyout
  filters the snippet list live on Title OR Body (case-insensitive contiguous
  substring). Header status reflects match count; dedicated empty states for
  "no snippets yet" vs "no matches".
- **Keyboard navigation in flyout** — Up/Down/PageUp/PageDown move the highlighted
  row (auto scroll-into-view); Enter copies; Esc clears the filter then closes
  on a second press. Search box auto-focused on open.
- **Alt+1..9 quick-pick (F3)** — first nine visible rows show their `1`..`9`
  index next to the title; `Alt+<digit>` (both top-row and numpad) copies the
  matching row. Plain digit keys stay routed to the search box.
- **Tray right-click context menu (F4)** — native Catppuccin Mocha menu with
  Open snippets / Settings… / About / Quit. Left-click still opens the flyout;
  double-click still opens Settings. Flyout footer also gains About alongside
  Settings… and Quit.
- **About surface (I15)** — new `AboutWindow` shows version (from assembly),
  links to repo + LICENSE.
- **Second-instance handoff (F11)** — running TaskCopy.exe a second time signals
  the first instance (via named pipe `\\.\pipe\TaskCopy`) to bring Settings
  forward, instead of silently exiting. CLI: `--settings` (default) or `--flyout`.
- **Schema migrations (F10)** — new `Data/Migrations.cs` tracks schema via
  `PRAGMA user_version`. v0.1 schema preserved as ApplyV1 (idempotent for
  upgrading users). Connections now enable `PRAGMA foreign_keys = ON`; the
  database opens in `journal_mode = WAL` for better durability + read
  concurrency.
- **First-run welcome (F12)** — fresh installs get 5 generic example snippets
  seeded and the Settings window opens automatically. Toggled via
  `SettingsStore.IsFirstRunComplete`.
- **Snippet truncation tooltips (I9)** — Title + Preview rows in flyout and
  Settings list now show the full Title/Body in a hover tooltip.
- **Diagnostics buttons** — "Open log folder" and "Open data folder" buttons in
  Settings open `%LOCALAPPDATA%\TaskCopy\logs` and `%LOCALAPPDATA%\TaskCopy`
  respectively.

### Changed
- Snippet editor in Settings no longer issues a SQLite UPDATE per keystroke;
  writes debounce to 300 ms idle (I2). Pending edits flushed on selection
  change and Settings window close.
- Launch toast is now first-run-only (I5); subsequent launches are silent.
  Hotkey-registration-failure toast still fires when applicable.
- `Snippet.Preview` now splits on `\r` OR `\n` (I14), fixing CR-only bodies.
- Hotkey rebind no longer persists or applies the new combo until
  `TryRegister` succeeds (I1); the previous combo is restored and re-registered
  on failure, eliminating the lock-yourself-out path.
- `SnippetMenuWindow.Deactivated` skips closing when focus moves to another
  TaskCopy window (I3), so future in-process dialogs / popups don't dismiss
  the flyout.
- `SnippetDatabase.Insert` now wraps `MAX(sort_order) + INSERT` in a single
  transaction using `RETURNING id`, eliminating the two-statement race (I11).
- Single-instance mutex moved from `Global\` to `Local\` namespace (I12) —
  per-user-session scope matches `LocalApplicationData` storage and avoids the
  `SeCreateGlobalPrivilege` requirement on locked-down RDS / kiosk sessions.
- Tray icon now opts INTO Win11 Efficiency Mode (I13) — throttles background
  CPU/memory priority while TaskCopy idles.
- `CrashLog.Write` rotates `crash.log` → `crash.log.1` (overwriting any prior)
  once the live file exceeds 1 MB (I4).

### Architecture
- New service: `Services/AutoPasteService` composes `ForegroundWindowCapture`
  + `SettingsStore.AutoPaste`; SendInput / INPUT / KEYBDINPUT P/Invoke added
  to `NativeMethods`.
- New service: `Services/SingleInstanceServer` (named-pipe IPC). Same primitive
  is the planned hook-up point for the v0.4 Windhawk taskbar mod.
- `ForegroundWindowCapture` now ignores HWNDs owned by the TaskCopy process so
  the tray icon / flyout / settings window can't be captured as the "previous
  foreground."
- Flyout snippet list switched from `ItemsControl` to `Focusable=False`
  `ListBox` with `SelectedIndex` binding — keyboard focus stays on the search
  box while selection moves.
- New view model wrapper `ViewModels/SnippetRow` carries 1-based `DisplayIndex`
  for the flyout's `1..9` index glyphs without mutating `Models/Snippet`.

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
