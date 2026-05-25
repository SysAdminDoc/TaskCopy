# TaskCopy — Roadmap

Single-click clipboard snippet manager for Windows. Right-click the tray icon (or hit a hotkey) to open a flyout near the cursor with your stored snippets; click one to copy + auto-paste into the previously focused window.

**Stack:** C# / .NET 10 WPF · H.NotifyIcon.Wpf · NHotkey.Wpf · SQLite · CommunityToolkit.Mvvm · Catppuccin Mocha
**Reality check:** literally extending the Windows 11 taskbar right-click menu requires Windhawk DLL injection. The tray + cursor-flyout pattern delivers the same UX with zero injection. A Windhawk companion mod can add the literal taskbar trigger as a v0.4 power-user add-on.

This file is the single source of truth for what's done and what's next. The detailed evidence, rationale, and acceptance criteria for each F-/I- item live in [`RESEARCH_FEATURE_PLAN.md`](RESEARCH_FEATURE_PLAN.md); the architecture rationale lives in [`research/architecture-research.md`](research/architecture-research.md).

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

## v0.2.0 — Power-user core (P0 batch from RESEARCH_FEATURE_PLAN.md Phase A)

Goal: turn TaskCopy from "fancy clipboard" into "muscle-memory text expander."

### P0 features

- [x] **F1 — Finish auto-paste** (`Services/ForegroundWindowCapture` + new `Services/AutoPasteService` using `SendInput`; enable settings checkbox)
- [x] **F2 — Search + type-ahead filter in the flyout** (`SnippetMenuViewModel.Filter`, `SnippetMenuWindow` TextBox row + keyboard nav)
- [x] **F3 — Number-key quick-pick `1`..`9`** (flyout `OnKeyDown` + row index glyph)
- [x] **F4 — Tray right-click → Settings / Quit / About / Open snippets context menu** (move flyout to left-click only, add native context menu; also add Settings + Quit + About rows to flyout footer)

### P0 bug fixes

- [x] **I1 — Fix `SetHotkey` persist-before-register bug** (`ViewModels/SettingsViewModel.cs:162-178`)

### Quick wins (XS — bundled with above)

- [x] **I5 — First-run-only launch toast** (`SettingsStore.IsFirstRun` flag)
- [x] **I12 — Use `Local\` mutex instead of `Global\`**
- [x] **I13 — Enable Efficiency Mode on tray icon**
- [x] **I14 — `Snippet.Preview` split on `\r` or `\n` (not just `\n`)**
- [x] **I15 — About surface w/ version + link to GitHub + LICENSE**
- [x] **I9 — Tooltip on truncated titles**
- [x] **I11 — Atomic `Insert` (transaction + `RETURNING id`)**

### Foundation for later phases

- [x] **F10 — Schema versioning + migration framework** (`PRAGMA user_version` + `Data/Migrations.cs`)
- [x] **F12 — First-run welcome (seed 3-5 example snippets + open Settings)**

### Reliability

- [x] **I2 — Debounce snippet editor writes (300 ms idle)**
- [x] **I3 — Tame `Deactivated → Close` race** (so F2 typing doesn't dismiss the flyout)
- [x] **F11 — Second-instance handoff via named pipe `\\.\pipe\TaskCopy`** (signals first instance to open Settings; reuses IPC for v0.4 Windhawk mod)
- [x] **I4 — Crash log rotation + "Open log folder" button in Settings**

---

## v0.3.0 — Snippet brain

Goal: scale snippets past 50, add power-user editing affordances.

- [x] **F5 — Placeholders (`{{date}}`, `{{time}}`, `{{clipboard}}`, `{{cursor}}`, `{{ask:Field}}`)**
- [x] **F6 — Snippet groups / folders** (`groups` table + nullable `group_id` on `snippets`; group dropdown in Settings + Manage groups modal; flyout pivot deferred — flyout still shows all snippets with search to narrow)
- [x] **F7 — Per-snippet quick hotkey** (`quick_hotkey TEXT NULL` column + multi-register in `HotkeyService`)
- [x] **F8 — Frecency / Pin / "Recent" ordering** (`used_count`/`last_used_at`/`pinned` columns)
- [x] **F9 — JSON import/export + automatic on-startup backup** (rotated 3-deep via `VACUUM INTO`)
- [x] **F14 — Snippet preview popup on hover + monospace body toggle** (per-snippet `is_monospace`) — preview tooltips already shipped in v0.2 (I9); monospace toggle shipped here, hover popup deferred (current tooltip covers it).
- [x] **F15 — Optional clipboard auto-capture (Recent clips section)** (`AddClipboardFormatListener` + `recent_clips` table; respects `ExcludeClipboardContentFromMonitors`) — capture + Settings toggle + "Clear" shipped; flyout "Recent" section deferred (clips are stored and clearable, but not yet shown in the picker).
- [x] **I6 — Confirm-delete (with "don't ask again") + soft-delete trash** (`deleted_at INTEGER NULL`) — confirm + soft-delete + 30-day auto-purge shipped; "don't ask again" deferred (delete is a rare action — keep prompt by default).
- [x] **I7 — Drag-reorder in Settings list**
- [x] **I8 — Insert-token buttons in editor toolbar (`{{date}}`, `{{clipboard}}`, `{{ask}}`)**
- [x] **I10 — `AutomationProperties` on flyout + Settings for screen readers**

---

## v0.4.0 — Power-user surfaces

- [x] **F16 — Light / system-theme follow** (`Themes/Latte.xaml` + startup swap; restart required to apply — live `WM_SETTINGCHANGE` swap deferred because the styles bind brushes via StaticResource).
- [ ] **Windhawk companion mod** (`windhawk/taskcopy-taskbar-menu.wh.cpp`) — depends on F11 IPC primitive
  - Hook `TaskbarResources::OnTaskListButtonContextRequested` (Win11) + `CTaskListWnd::_HandleContextMenu` (Win10)
  - Inject "TaskCopy ▶" submenu populated from named-pipe IPC
  - README install steps + submit to `ramensoftware/windhawk-mods` (self-host fallback)

---

## v0.5.0+ — Future ideas (not committed)

- [ ] **F13 — Signed single-file publish + GitHub Actions release workflow + README screenshots + winget manifest** (needs SDK + GitHub remote + signing cert budget)
- [ ] **F17 — Velopack in-app auto-update** (depends on F13 release pipeline)
- [ ] MSIX packaging + Microsoft Store submission
- [ ] Cloud sync (encrypted, BYO bucket — S3/B2/Dropbox via user-supplied creds)
- [ ] Rich-text + image snippets (HTML, PNG via `CF_DIB`)
- [ ] Snippet templates w/ form-fill prompts beyond `{{ask:…}}`
- [ ] Code-signing cert + signed releases

---

## Out-of-scope (decided NO, not deferred)

- **Deskband / `IDeskBand` shell extension** — removed in Win11, dead end
- **`IContextMenu` / `IShellExtInit` shell extension** — wrong surface (targets files/folders, not taskbar)
- **Overriding `Win+V`** — owned by Microsoft, low-level keyboard hook is AV-flagged
- **Fork of ExplorerPatcher** — legal issues (taskbar reimpl is GPL-2.0 + author has restricted derivative distribution)
- **Keyboard-trigger expansion (Espanso-style `;sig` autocomplete)** — same `SetWindowsHookEx` AV issue; stay picker-based
- **Clipboard-history-as-default** — F15 is opt-in only; keep snippet-curated identity
- **Tests** — per CLAUDE.md, no tests unless explicitly requested
