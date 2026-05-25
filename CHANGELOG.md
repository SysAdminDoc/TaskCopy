# Changelog

All notable changes to TaskCopy will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.5] — 2026-05-25

### Added
- **Multi-field forms (F36).** Snippets can include `{{form:Name|Ticket|Priority}}` to show one modal with several fields before expansion. Values are reusable through matching `{{ask:Name}}`, `{{ask:Ticket}}`, etc., and repeated `ask` tokens reuse the same answer instead of prompting again.
- Settings editor insert toolbar now includes a `{{form:...}}` helper.

### Changed
- Live preview resolves form-backed fields with the same `<Field>` stubs used by `{{ask:Field}}`.
- CLI copy/paste and multi-clip paste use the same form prompt path as normal flyout picks.

### Architecture
- New view: `Views/FormWindow.xaml(.cs)`.
- `TemplatingContext` gained `PromptForMany` for one-shot multi-field collection.

## [0.5.4] — 2026-05-25

### Added
- **Espanso YAML import (F38).** Settings → "Install pack / import…" now accepts `.yml` / `.yaml` files containing Espanso `matches:`. Static `replace` entries become TaskCopy snippets; `label` wins for the snippet title, otherwise the first `triggers[]` value or `trigger` is used. Imports are grouped under the YAML filename and duplicate titles are skipped.

### Changed
- Unsupported Espanso behaviors (`regex`, `vars`, `form`, `form_fields`, `image_path`, `html`, `markdown`) are intentionally skipped rather than imported as broken snippets.
- The import picker now handles `.taskpack`, `.json`, `.yml`, and `.yaml` in one flow.

### Architecture
- New service: `Services/EspansoImport.cs`.
- New NuGet: `YamlDotNet` 16.1.0.

## [0.5.3] — 2026-05-25

### Fixed
- **MVVMTK0034 in `SettingsViewModel.RevertBackupEncryptedBinding`.** v0.5.2's revert path called `SetProperty(ref _backupEncrypted, …)` which directly references an `[ObservableProperty]`-decorated field — forbidden by the source generator. Refactored to set the generated `BackupEncrypted` property under the existing `_suppressEncryptionToggleEvent` flag so the change-handler doesn't loop. Behavior identical; CI green again.

## [0.5.2] — 2026-05-25

### Added
- **Opt-in backup encryption (F49).** New "Encrypt backups with password" Settings checkbox. When ON, `BackupRotator.Rotate` writes `.bak.{N}.enc` files wrapped with AES-256-GCM; the key is derived from the user's password via PBKDF2-SHA256 (600,000 iters, per OWASP 2023). The password itself is **never** persisted — only a small PBKDF2 verification token (`settings.backup.pw_token`). Per-file salt + nonce so two backups of the same DB look different. Restore prompts for the password; wrong password / corrupt ciphertext fails closed. Encrypted slots show "encrypted (password required)" in the Restore picker. New `Services/BackupCrypto.cs` carries all the crypto + a `IsEncryptedBackup` magic-header probe. Disabling encryption requires the current password (so a stranger on an unlocked machine can't silently turn it off).
- **Per-app rules (F35).** New `snippets.target_app_glob` column (schema V5). Comma-separated `*`-wildcard process-name patterns (`outlook.exe,*code*.exe`) restrict which apps a snippet shows in. `ForegroundWindowCapture.TryGetLastTargetProcessName` (added in v0.5.0 for F48) feeds the captured foreground name into the flyout VM; `SnippetMenuViewModel.ApplyFilter` drops non-matching snippets. New Settings textbox "Show in app:" lets the user set the glob per snippet. JSON export/import round-trips the field.
- **I41 (light) — "Move to group" context menu.** Right-click on a snippet row in Settings → submenu lists every defined group + "(Ungrouped)". Click sets `EditGroup`, which writes via the existing `SnippetDatabase.SetGroup` path. Lighter than duplicating the flyout chip strip into Settings.

### Changed
- `Migrations.CurrentVersion` 4 → 5 via `ApplyV5` (transactional, idempotent).
- `BackupRotator.Rotate` accepts optional `encrypt` + `password` parameters; when encrypted, the temp plaintext goes through `quick_check` before being wrapped + the temp is wiped from disk in a `finally`. `ListAvailable` enumerates both `.db` and `.enc` slots; `BackupSlot.IsEncrypted` flags them to the restore UI.
- `BackupRotator.RestoreFrom` gained an optional `password` parameter; encrypted sources decrypt to a temp, run `quick_check`, then swap in. Wrong password leaves the live DB untouched.

### Architecture
- New services: `Services/BackupCrypto.cs`, `Services/AppGlob.cs`.
- New settings KV: `backup.encrypted`, `backup.pw_token`.
- `App._inMemoryBackupPassword` field caches the password for this session only — never persisted, lost on app exit. Re-enter via Settings to enable post-restart backup rotation.

## [0.5.1] — 2026-05-25

### Added
- **Multi-clip paste (F32).** Ctrl+click (or Ctrl+Space when keyboard-focused) toggles a snippet's membership in a multi-paste set. When ≥1 row is selected, the flyout footer shows "Paste N selected (Enter)" and Enter ships the concatenated bundle with the configurable separator from `settings.multipaste.separator` (defaults to `\n\n`). Each contributing snippet's `used_count` is bumped; lifetime stats include the multi-paste body length. Esc has a three-stage clear (multi-selection → filter → close).

### Changed
- **`SnippetMenuViewModel.Snippets` and `RecentClips` are now `BulkObservableCollection<T>` (I37).** `ApplyFilter` writes into a temporary `List<T>` then calls `ReplaceAll` so WPF sees one `CollectionChanged(Reset)` instead of N add/remove events. Measurable improvement at >500-snippet libraries during typing-while-filter.

### Deferred
- **I41 (drag-to-group)** — needs duplicating the flyout's chip-strip into Settings, which is real UX design work better suited to a dedicated session. Tracked in ROADMAP v0.5+.

### Architecture
- New: `ViewModels/BulkObservableCollection.cs` — `ObservableCollection<T>` subclass with `ReplaceAll(IEnumerable<T>)` that suspends per-item events during the rebuild.
- New settings KV: `settings.multipaste.separator` (default `\n\n`).
- New ViewModel surface: `SnippetMenuViewModel.MultiSelection`, `HasMultiSelection`, `MultiSelectionLabel`, `ToggleMultiSelectionAtIndex`, `ClearMultiSelection`, `IsMultiSelected`, `TryCopyMultiSelection`, `MultiSnippetCopyRequested` event.
- New App handler: `HandleMultiSnippetCopyAsync` expands each body separately (so placeholders keep their per-snippet context), concatenates with the configured separator, and ships through the existing auto-paste + stats path.

## [0.5.0] — 2026-05-25

### Added
- **Snippet body edit history (F46).** Every debounced save records one row in the new `snippet_body_history` table (capped at 10 most recent per snippet, FK CASCADE drops them on hard-delete). The snippet editor toolbar grows a "History…" button opening a modal of past versions with timestamps; per-row "Restore" pushes the body back through the normal EditBody path (which itself records a fresh history row, so a restore is itself revertible). Per-row "Delete this version" for one-off pruning.
- **Per-snippet last-paste target tracking (F48).** New `snippets.last_target_process_name` + `last_target_at` columns. After a successful auto-paste, `ForegroundWindowCapture.TryGetLastTargetProcessName` resolves the captured HWND's PID → process name and writes it back. Foundation for future F35 per-app rules; no UI surface yet beyond the DB.
- **Lifetime usage statistics (F37).** New `settings.stats.total_pastes` + `stats.total_chars` counters incremented on every successful auto-paste. About window now leads with "You've pasted N snippets — about M minutes of typing TaskCopy did for you." (5 chars/sec estimate). Falls back to a friendly empty state on a fresh install.
- **CLI result file (I39).** `--copy` / `--paste` / `--list` now write a one-line outcome (`ok: …` / `not-found: …` / `error: …`) to `%LOCALAPPDATA%\TaskCopy\.cli-result`. Lets PowerShell / cmd scripts know whether a snippet lookup actually hit. Pipe IPC stays one-way; the file is the workaround for `WinExe` not having attached stdout.

### Changed
- **Schema bumped V3 → V4** via `Migrations.ApplyV4` (idempotent, transactional). Migration covers F46 + F48.
- **About window now requires a `SettingsStore` constructor argument** to surface the usage-stats line. Default constructor still works for backward compatibility (just hides the stats line).

### Verified
- **B22 culture-sensitive string sort audit — clean.** Every `Contains`/`StartsWith`/`Equals`/`HashSet`/`Any(g.Name)` callsite in the codebase already passes `StringComparison.OrdinalIgnoreCase` (or compares on a numeric/char). No latent culture-sensitive surprise.

### Architecture
- New model: `Models/BodyHistoryEntry.cs`.
- New view: `Views/BodyHistoryWindow.xaml(.cs)`.
- New viewmodel: `ViewModels/BodyHistoryViewModel.cs`.
- New repository surfaces: `SnippetDatabase.RecordBodyHistory`, `GetBodyHistory`, `DeleteBodyHistoryEntry`, `SetLastTarget`.
- New service surface: `ForegroundWindowCapture.TryGetLastTargetProcessName`.
- `SettingsViewModel.FlushPendingSave` now records a history row after each disk write.
- `App.HandleSnippetCopyAsync` records last-target + bumps stats post successful auto-paste.

## [0.4.6] — 2026-05-25

### Added
- **"Last position (sticky)" flyout position (F50).** New `Theme.LastPosition` enum value + Settings dropdown entry. On flyout close, `SnippetMenuWindow.OnClosingPersistPosition` writes the current `Left`/`Top` (WPF DIPs) to `settings.flyout.last_x` / `last_y`. Next open restores them. NaN/missing settings fall back to cursor placement on first use. Existing clamp-into-work-area logic still applies (so a moved monitor won't strand the flyout off-screen).
- **`CONTRIBUTING.md`** with build steps, commit-style conventions (no Co-Authored-By, imperative subject, cite F-/I-/B- IDs), versioning checklist, and a pointer at the snippet-pack ecosystem.
- **`.github/ISSUE_TEMPLATE/{bug_report,feature_request}.md`** + **`.github/PULL_REQUEST_TEMPLATE.md`** so first-time contributors see the expected shape upfront. Bug template requests the diagnostics bundle directly.

### Architecture
- `SettingsStore.FlyoutLastPosition` tuple property, persisted as invariant-culture doubles.
- `SnippetMenuWindow.ShowAtCursor` gained an optional `SettingsStore` parameter so the window can self-persist position on close without an extra wiring step from App.

## [0.4.5] — 2026-05-25

### Added
- **`File issue` button — `gh` CLI integration (F45).** Settings → Diagnostics → "File issue" detects the GitHub CLI on PATH and, when available + authenticated, opens a new issue against `SysAdminDoc/TaskCopy` with the existing diagnostics bundle as the body. Falls back to clipboard + a status hint when `gh` is missing or auth fails. New `Services/GhCli.cs` keeps the integration small + best-effort (2 s availability probe, 30 s issue-create timeout).
- **`.taskpack` file extension support (F44 code-only).** Import dialog filter now offers "TaskCopy pack or snippets (\*.taskpack;\*.json)" as the default. Format is the existing F9 JSON — `.taskpack` is the convention community packs use so a file association can resolve to TaskCopy. Settings button renamed "Install pack / import…". Index repo (curation work) is out-of-scope for this code change; tracked separately at `SysAdminDoc/taskcopy-packs`.
- **"Open in editor…" in the snippet editor (I40).** Spawns the user's preferred external editor on the current body. Resolution order: `Settings.ExternalEditorCommand` → `$EDITOR` → `code --wait` (VS Code on PATH) → `notepad.exe`. The body is round-tripped through a temp file; the editor process exit signals "done" so `code --wait` / `notepad++ -nosession` / `notepad` all work. New `Services/ExternalEditor.cs`. New `SettingsStore.ExternalEditorCommand` KV.

### Architecture
- New services: `Services/GhCli.cs`, `Services/ExternalEditor.cs`.
- New repository surface: `SettingsStore.ExternalEditorCommand`.

## [0.4.4] — 2026-05-25

### Added
- **High-contrast theme variant (F42).** New `Themes/HighContrast.xaml` palette delegates every brush to `SystemColors.*` so Windows HC themes drive the look. `ThemeService.Resolve` auto-selects HighContrast when `SystemParameters.HighContrast` is on regardless of the user's preference (accessibility users get the right palette even if they didn't explicitly pick it). New `Theme.HighContrast` enum value + Settings dropdown entry "High contrast (system colors)". `ThemeService.OnUserPreferenceChanged` now also reacts to `UserPreferenceCategory.Accessibility` so HC-on/off transitions trigger the same B17 relaunch prompt.
- **"Don't ask again" for delete confirm (F47).** New `Views/ConfirmDeleteWindow.xaml` (a custom WPF dialog — `MessageBox` doesn't ship with a checkbox). Suppression persists via `settings.delete.skip_confirm`. Trash + 30-day purge still guards against the accidental Del; reset the suppression via the new F52 "Reset to defaults" button.
- **"Test hotkey" verification button (I38).** New `HotkeyService.TestHookOneShot` swallows the next primary-hotkey trigger and reports it to the Settings UI. Surfaces a clear "didn't fire in 5 s — another app may be grabbing it" message when the registered hotkey doesn't actually reach TaskCopy (catches the case where I17's green dot says "Active" but a third-party hotkey manager is intercepting).
- **"Reset to defaults" button (F52).** Settings → Diagnostics → "Reset to defaults…" wipes the `settings` KV table only (snippets, groups, trash stay intact) and relaunches into Settings via the existing `--settings` CLI path. Confirm dialog explicitly enumerates what gets cleared.
- **`Microsoft.Extensions.DependencyInjection` 9.0.0 (I35).** Container built in `App.OnStartup` from the manually-constructed singletons. Existing wiring is unchanged; new v0.5 services (Velopack update check, Windhawk IPC bridge) can resolve from `App._services` instead of adding hand-hooks.

### Changed
- `ThemeService.OnUserPreferenceChanged` now compares `Resolve(_currentPreference)` against the last resolved theme — fires for both Theme.Auto light/dark flips AND HC-on/HC-off transitions regardless of which preference the user picked.
- `SettingsViewModel.DeleteSnippet` consults `_settings.DeleteSkipConfirm` first and only shows the modal when needed.
- `SettingsViewModel.IsReservedCombo` extended to flag any combo containing `ModifierKeys.Windows` (rolled in from v0.4.3).
- `SettingsViewModel.ThemeOptions` now includes "High contrast (system colors)" as a fourth entry.
- `SnippetDatabase.ClearAllSettings()` added — wipes settings KV without touching snippets/groups/trash. Used by F52.

### Architecture
- New views: `Views/ConfirmDeleteWindow.xaml(.cs)`.
- New theme: `Themes/HighContrast.xaml` (~280 lines, all `{x:Static SystemColors.…}` references).
- New service surfaces: `HotkeyService.TestHookOneShot` Action, `SettingsViewModel.DeleteConfirmer` callback (avoids ViewModels → Views compile-time dependency).
- New NuGet: `Microsoft.Extensions.DependencyInjection` 9.0.0.
- `ThemeService` gains explicit-preference + Accessibility-category handling in the runtime watcher.

## [0.4.3] — 2026-05-25

### Fixed
- **CI was red since v0.4.0 (B16, 5 errors).** All five `-warnaserror` violations resolved:
  - `App.xaml.cs:251` CS8604 — guard `_settings` in `ShowSnippetMenu`'s early-out.
  - `Views/SettingsWindow.xaml.cs:140` CS0103 — added `using System.Windows.Automation;` for `AutomationProperties.GetName` lookup.
  - `ViewModels/SettingsViewModel.cs:115` CS9273+CS9258 — renamed the `PromptFor` lambda parameter from `field` (a C# 14 contextual keyword in property accessors) to `f`.
  - `Views/AboutWindow.xaml.cs:24` IL3000 — replaced `Assembly.GetEntryAssembly()?.Location` (always empty in single-file publish) with `AppContext.BaseDirectory` for the side-by-side `LICENSE` lookup.

### Added
- **`Theme.Auto` follows OS theme changes at runtime (B17).** Subscribes to `SystemEvents.UserPreferenceChanged` and, when the resolved palette would actually change, surfaces the same I16-A relaunch prompt the Settings dropdown uses. Listener only fires while the user has Auto selected — `ThemeService.UpdatePreference` keeps the state machine in sync when the user manually switches.
- **Backup snapshot verification (F41 / B20).** After `BackupRotator.Rotate` writes the new `.bak.0.db` via `VACUUM INTO` + fsync, it runs `PRAGMA quick_check` against the file in a fresh read-only connection. On non-"ok" the broken backup is deleted (the prior slot 0 — now at .bak.1 — remains intact) and a CrashLog entry records the corruption. Catches the rare case where VACUUM INTO claimed success but the file on disk is unusable.
- **Win+\* reserved-combo coverage (B21).** `SettingsViewModel.IsReservedCombo` now rejects any combo containing `ModifierKeys.Windows` so the user gets TaskCopy's "reserved by Windows" message instead of NHotkey's generic registration error.

### Changed
- **README CLI section corrected (B18)** — `--list` writes `id\tTitle` lines to `%LOCALAPPDATA%\TaskCopy\snippets.list` (the implementation never used stdout, since TaskCopy is a `WinExe` without an attached console).

### Architecture
- `Services/ThemeService` gains `SystemThemeChanged` event + `StartSystemThemeWatcher` / `StopSystemThemeWatcher` / `UpdatePreference` API.
- `Data/SnippetDatabase.IntegrityCheck(string path)` static overload runs quick_check against an explicit file path in read-only mode — used by `BackupRotator` for the new verification step.

## [0.4.2] — 2026-05-25

### Changed
- **`AutoPasteService` is async (I22)** — the dispatcher no longer blocks on the 30 ms + 15 ms paste-settle `Thread.Sleep`s. New `TryAutoPasteDetailedAsync` is the canonical entry point; both `App.HandleSnippetCopyAsync` and `App.HandleRecentClipCopyAsync` now `await` it directly. The legacy sync `TryAutoPasteDetailed` / `TryAutoPaste` overloads still exist (Task.Run + GetAwaiter().GetResult()) for any external caller that isn't async-aware.

## [0.4.1] — 2026-05-25

### Added
- **Active monitor center flyout position (I19)** — Settings → Flyout position lets users open the flyout horizontally centered on the active monitor (default still "At cursor"). Useful on ultrawide displays where the above-and-left anchor can pin against an edge.
- **Decay-weighted frecency (I23)** — Most-used sort now uses `count × exp(-Δt / 7 days)` so a snippet used 100× last year doesn't outrank one used 5× today.
- **Flyout tooltip honors `IsMonospace` (I25)** — hover preview keeps aligned columns for code snippets via a `Cascadia Mono` font binding on the tooltip.
- **`{{cursor}}` cap warning toast (B14)** — when the caret offset exceeds 5,000 left-arrows the cap kicks in and surfaces a one-time tray notification.

### Changed
- **Dispatcher exceptions show a tray toast (I27)** — replaces the foreground-stealing `MessageBox` with a non-blocking `NotificationIcon.Warning` notification. Modal fallback retained for very early startup catastrophes where the tray icon isn't wired yet.
- **Backups now fsync to disk (B13)** — after `VACUUM INTO` writes the fresh snapshot, `FileStream.Flush(flushToDisk: true)` forces the OS write-back cache out so a power loss between launch and the next sync can't produce a torn backup.
- **Migrations run fully inside their transaction (I29)** — `ApplyV2` and `ApplyV3` now pass the `SqliteTransaction` to every `ALTER TABLE` and `PRAGMA table_info`, so a partial migration failure rolls back cleanly and leaves `user_version` at the prior value for the next launch to retry.
- **Start-with-Windows is registry-canonical (B15)** — the `HKCU\…\Run` registry value is now the source of truth on every startup. The `SettingsStore.StartWithWindows` mirror is reconciled at load time so a user who deleted the Run value externally sees the truth in the Settings checkbox.

### Fixed
- **Quick-hotkey collisions with the primary hotkey already produce a clear message (B11 closed-by-F22)** — when capturing a per-snippet hotkey that matches the primary `Ctrl+Alt+V` combo, the status bar now says exactly which collision happened instead of just "registration failed."

## [0.4.0] — 2026-05-25

### Fixed
- **`ClipboardWatcher` correctly reads `CanIncludeInClipboardHistory` (B9)** — the 4-byte DWORD payload (0=exclude, 1=include) is now honored. Older code treated *any* presence of the format as "exclude" and silently dropped clips from apps that explicitly opted IN. Updated comment cites Microsoft's clipboard-formats reference.
- **First-run race ordering (B10)** — Settings opens *before* the welcome toast so window activation no longer competes for foreground focus.

### Added — closure of v0.3 deferred work
- **Recent-clips flyout pivot (F19, closes F15)** — when `RecentClipsEnabled` is on, the flyout now shows a "Recent" section above the snippet list with the last 10 captured clips. Click to copy + auto-paste; right-click for a context menu offering "Copy" or "Promote to snippet…" (the latter inserts a new snippet derived from the clip body and opens Settings on it).
- **Flyout group pivot chips (F20, closes F6)** — chip strip above the search box: `[All · N] [Group A · M] [Group B · K] [Ungrouped · J]`. Selection persists across opens via `settings.flyout.last_group_id`. Chips hide entirely when no groups are defined.
- **Restore-from-backup UI + startup integrity check (F21)** — `PRAGMA quick_check` runs on every startup; non-"ok" prompts the user to restore from the freshest `.bak.0.db` (with a pre-restore snapshot saved at `.bak.preRestore.db` so the restore is reversible). Settings → Diagnostics → "Restore backup…" exposes the same path on demand.
- **Free-form per-snippet quick-hotkey (F22)** — replaced the fixed `Ctrl+Alt+1..9` ComboBox with a Capture… button + display label. Any combo (e.g. `Ctrl+Alt+S`) is now valid. Refuses combos that clash with the primary hotkey or with the reserved Ctrl-only set (`C V X Z Y A S N O P F W T`). Clear button removes a binding.
- **Trash bin UI (F23)** — new `TrashWindow` modal listing soft-deleted snippets with their deletion timestamp and a "purges in N days" countdown. Per-row Restore / Delete Permanently, plus an Empty Trash footer button. Settings → "Trash…" button.

### Added — power-user surfaces
- **Send-as-keystrokes paste mode (F24)** — per-snippet "Paste" dropdown: Auto (Ctrl+V, default) or Type characters. Type-mode uses `INPUT_KEYBOARD` with `KEYEVENTF_UNICODE` so the snippet appears in terminals, RDP sessions, and other apps that swallow synthetic Ctrl+V. Capped at 5,000 characters per snippet. Schema bumped to V3 (`paste_mode INTEGER NOT NULL DEFAULT 0`). JSON export round-trips the field.
- **Live placeholder preview pane (F25)** — the snippet editor now shows what `Hi {{ask:Name}} on {{date}}.` resolves to, live, below the body field. Uses stub values (`<Name>`, `<clipboard>`, today's date) so the preview stays pure and never touches the real clipboard.
- **Fuzzy / prefix-weighted search (F27)** — replaced boolean substring matching with scored ranking: title prefix (100) > title contains (60) > body contains (20). Fielded operators: `title:foo` / `body:foo` match only that column. Pure-managed string ops; no FTS5 dep.
- **Clipboard transforms (F28)** — pipe-chained placeholders: `{{clipboard|upper}}`, `{{clipboard|lower}}`, `{{clipboard|trim}}` / `trimstart` / `trimend`, `{{clipboard|reverse}}`, `{{clipboard|length}}`, `{{clipboard|jsonpretty}}`, `{{clipboard|urlencode}}`, `{{clipboard|urldecode}}`, `{{clipboard|base64}}`, `{{clipboard|base64decode}}`, `{{clipboard|sha256}}`, `{{clipboard|md5}}`. Multi-chain works: `{{clipboard|trim|upper}}`. Unknown transforms are no-ops. Works on any string-producing placeholder, not just clipboard.
- **CLI scripting flags (F29)** — `TaskCopy.exe --copy <id|title>` puts a snippet on the clipboard, `--paste <id|title>` copies + auto-pastes into the foreground window, `--list` writes `id\ttitle` lines to `%LOCALAPPDATA%\TaskCopy\snippets.list`. All routed through the existing `\\.\pipe\TaskCopy` named pipe. Lookups resolve by id first, then case-insensitive title match.

### Added — UX, accessibility, diagnostics
- **Live theme swap via relaunch (I16 Option A)** — picking a new theme now prompts: "Apply now? TaskCopy will restart and Settings will reopen." Confirmed restarts via `--settings` so Settings comes back focused. Brushes still bind StaticResource (Option B = `DynamicResource` refactor deferred to v0.5+).
- **Hotkey registration status indicator (I17)** — `HotkeyService.IsPrimaryRegistered` + `PrimaryRegistrationChanged` event surface a green/red dot in Settings beside the hotkey display.
- **Auto-paste fail toast (I18)** — `AutoPasteService.TryAutoPasteDetailed` now returns a `Result` enum (`Skipped`/`Pasted`/`ForegroundRestoreFailed`/`SendInputFailed`). When the foreground refuses (typically an elevated target), a one-time-per-session tray notification explains why and tells the user to Ctrl+V manually.
- **Per-monitor DPI v2 manifest (I20)** — new `app.manifest` declares `PerMonitorV2, PerMonitor` DPI awareness + `gdiScaling=true` for crispness on mixed-DPI setups. Also pins `asInvoker` execution level (no elevation prompt).
- **Daily backup throttle (I21)** — `BackupRotator.Rotate` no longer fires on every launch; gated by `settings.backup.last_at` once per 24 h.
- **Generic seeded snippets (I26)** — first-run seed no longer includes `"Best,\nMatt"`; uses `"Best,\n{{ask:Name}}"` which doubles as a placeholder demo.
- **JSON export carries `schemaVersion` (I24)** — payload tagged with `Migrations.CurrentVersion` at export time so future importers can branch on it.
- **Copy diagnostics button (I28)** — Settings → Diagnostics → "Copy diagnostics" bundles `{version, schema, OS, snippet/group counts, hotkey state, lastBackup, theme, autoPaste, recentClips, last 200 lines of crash.log}` into a Markdown block on the clipboard.
- **Keyboard focus outline (I33)** — Mocha and Latte button templates now show a `Mocha.Mauve` 1 px border on `IsKeyboardFocused`. Same for `Mocha.SnippetRow`.
- **Settings list keyboard accelerators (I32)** — Del = delete, Ctrl+N = add, F2 = focus title with text selected.
- **Status-bar snippet count (I34)** — Settings status bar shows "N snippets · M groups", live-updated via `Snippets.CollectionChanged`/`Groups.CollectionChanged`.

### Added — distribution
- **Single-file publish (F18 Phase 1)** — `<PublishSingleFile>true</PublishSingleFile>` by default; `IncludeNativeLibrariesForSelfExtract=true` + `EnableCompressionInSingleFile=true`. `dotnet publish -c Release -r win-x64 [--self-contained false|true]` produces either a runtime-dependent ~3-8 MB exe or a fully self-contained ~80 MB exe.
- **GitHub Actions release workflow** (`.github/workflows/release.yml`) — on `v*` tag (or manual dispatch), builds both portable and self-contained `win-x64` flavors, zips them as `TaskCopy-<version>-win-x64-<flavor>.zip`, and uploads to the GitHub Release.
- **CI workflow** (`.github/workflows/ci.yml`) — verifies `dotnet build -c Release -warnaserror` on every push + PR.
- **README install section + keyboard cheatsheet + CLI doc** (I30, I31) — covers the prebuilt-binary path, the SmartScreen workaround, the planned winget invocation, and the build-from-source path. Lists every keyboard shortcut + every CLI flag.

### Changed
- `Migrations.CurrentVersion` bumped from 2 to 3 (`ApplyV3` adds `snippets.paste_mode`).
- `csproj` `<Version>` bumped to 0.4.0; `<PublishSingleFile>` flipped to `true`; `<Deterministic>` + `<ContinuousIntegrationBuild>` (CI-only) added.
- `SnippetIO.ExportPayload` gains a nullable `schemaVersion`; `ExportSnippet` gains `pasteMode`.
- `SnippetTemplating.Expand` now splits the captured token at the first `|` and applies pipe-chained transforms via new `Services/SnippetTransforms.cs`.
- `SingleInstanceServer.ParseCliMessage` now recognises `--list` / `--copy <arg>` / `--paste <arg>` (plus the existing `--flyout` / `--settings`).
- `HotkeyService.TryRegister` now updates an `IsPrimaryRegistered` property and fires `PrimaryRegistrationChanged` on transition.

### Architecture
- New services: `Services/SnippetTransforms.cs` (F28), `Services/SnippetMatch.cs` (F27).
- New views: `Views/TrashWindow.xaml(.cs)`.
- New viewmodels: `ViewModels/TrashViewModel.cs`.
- `Data/Migrations.cs` adds `ApplyV3` (forward-only, idempotent via `AddColumnIfMissing`).
- `BackupRotator.ListAvailable` + `BackupRotator.RestoreFrom` close the F21 loop with a transactional `VACUUM INTO` pre-restore snapshot.
- New `src/TaskCopy/app.manifest` (PerMonitorV2 DPI, gdiScaling, asInvoker).
- `.github/workflows/{release,ci}.yml` are now the canonical build entrypoints.

## [0.3.0] — 2026-05-24

### Added
- **Snippet placeholders (F5)** — `{{date}}`, `{{date:format}}`, `{{time}}`, `{{time:format}}`, `{{clipboard}}` (captures the clipboard text from before TaskCopy opened), `{{cursor}}` (caret lands here after auto-paste — sends N synthesised Left arrows), `{{ask:Field}}` (modal prompts at paste time; Cancel aborts the whole copy). Unknown tokens are preserved verbatim; single-pass expansion. Insert-token buttons in the Settings editor toolbar (I8).
- **Snippet groups (F6)** — `groups` table + nullable `snippets.group_id` (FK `ON DELETE SET NULL`). New "Manage groups…" modal: add / rename / delete / reorder. Per-snippet Group ComboBox in the editor.
- **Per-snippet quick hotkey (F7)** — bind any snippet to `Ctrl+Alt+1..9` for direct copy + auto-paste from anywhere. HotkeyService refactored to support multiple ID-keyed registrations. Static `TryParseHotkey` accepts "Ctrl+Alt+1" style strings.
- **Frecency / Pin / sort modes (F8)** — `used_count`, `last_used_at`, `pinned` columns track usage. New flyout sort modes: Manual / Most used / Recently used. Pinned snippets always promote to top in non-Manual modes; 📌 glyph in the flyout row.
- **JSON import/export + auto-backup (F9)** — version-tagged JSON payload; SkipDuplicates merge. New `BackupRotator` does `VACUUM INTO` snapshots on each startup (3-deep rotation). New "Export snippets…" / "Import snippets…" buttons in Settings.
- **Soft-delete + 30-day auto-purge (I6)** — Delete now confirms via dialog then sets `deleted_at` instead of dropping the row. Background task on startup purges trash older than 30 days.
- **Drag-reorder in Settings (I7)** — drag snippets within the ListBox; the existing ↑/↓ buttons still work.
- **Optional clipboard auto-capture (F15)** — opt-in via Settings checkbox. `AddClipboardFormatListener` watches the clipboard and stores plain-text items < 10 KB into `recent_clips` (dedup + trim-to-50). Honors `ExcludeClipboardContentFromMonitors` and `CanIncludeInClipboardHistory` flags so password-manager content is never captured. "Clear" button purges the table.
- **Monospace body toggle (F14, partial)** — per-snippet `is_monospace` column. When set, the body editor switches to Cascadia Mono / Consolas / Courier New.
- **AutomationProperties (I10)** — Name/HelpText sprinkled on flyout search, flyout list rows, Settings snippet list, and the title + body editors so screen readers describe them properly.
- **Light theme (F16)** — Catppuccin Latte palette ships alongside Mocha. New Settings dropdown "Theme: Mocha / Latte / Follow system" (the latter reads HKCU `AppsUseLightTheme`). Applied at startup; restart required to change.

### Changed
- `SnippetDatabase.Insert` accepts an optional `groupId` parameter. `GetAll` now filters out soft-deleted rows by default; `GetTrashed` exposes them. New repository methods: `SoftDelete`, `Restore`, `PurgeDeletedOlderThan`, `SetQuickHotkey` (enforces single-owner per slot), `SetPinned`, `SetMonospace`, `SetGroup`, `RecordUse`, `GetGroups`, `InsertGroup`, `RenameGroup`, `DeleteGroup`, `ReorderGroups`, `InsertRecentClip` (dedup + trim), `GetRecentClips`, `ClearRecentClips`, `BackupTo` (`VACUUM INTO`).
- Snippet copy flow refactored: `SnippetMenuViewModel` now raises a `SnippetCopyRequested` event; `App.HandleSnippetCopyAsync` owns expand → clipboard → `RecordUse` → close-flyout → auto-paste. Same path serves per-snippet quick hotkeys.
- `Snippet` model gains observable properties for the seven new schema columns.
- `Models/SnippetGroup` + `Models/RecentClip` added.

### Architecture
- New services: `SnippetTemplating`, `BackupRotator`, `SnippetIO`, `ClipboardWatcher`, `ThemeService`.
- `Data/Migrations.cs` `ApplyV2` bundles all v0.3 schema changes — idempotent via `AddColumnIfMissing` + `PRAGMA table_info` introspection.
- New WPF windows: `ManageGroupsWindow`, `AskWindow`.
- `Themes/Latte.xaml` is a drop-in palette replacement keeping Mocha.* keys so every existing XAML reference resolves without churn.

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
