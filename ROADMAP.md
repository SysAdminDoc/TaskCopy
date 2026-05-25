# TaskCopy — Roadmap

Single-click clipboard snippet manager for Windows. Right-click the tray icon (or hit a hotkey) to open a flyout near the cursor with your stored snippets; click one to copy + auto-paste into the previously focused window.

**Stack:** C# / .NET 10 WPF · H.NotifyIcon.Wpf · NHotkey.Wpf · SQLite · CommunityToolkit.Mvvm · Catppuccin Mocha / Latte
**Reality check:** literally extending the Windows 11 taskbar right-click menu requires Windhawk DLL injection. The tray + cursor-flyout pattern delivers the same UX with zero injection. A Windhawk companion mod can add the literal taskbar trigger as a v0.5 power-user add-on.

This file is the single source of truth for what's done and what's next. The detailed evidence, rationale, and acceptance criteria for each F-/I-/B- item live in [`RESEARCH_FEATURE_PLAN.md`](RESEARCH_FEATURE_PLAN.md); the architecture rationale lives in [`research/architecture-research.md`](research/architecture-research.md). What shipped in each release lives in [`CHANGELOG.md`](CHANGELOG.md).

---

## v0.1.0 — Minimal viable snippet menu ✅ SHIPPED (2026-05-23)

See [`CHANGELOG.md`](CHANGELOG.md#010--2026-05-23).

## v0.2.0 — Power-user core ✅ SHIPPED (2026-05-24)

See [`CHANGELOG.md`](CHANGELOG.md#020--2026-05-24).

## v0.3.0 — Snippet brain ✅ SHIPPED (2026-05-24)

See [`CHANGELOG.md`](CHANGELOG.md#030--2026-05-24).

---

## v0.4.0 — Polish & distribution ✅ SHIPPED (2026-05-25)

Goal: close the v0.3 deferred gaps, harden correctness, and ship a binary anybody can install.

### P0 — Correctness & v0.3 closure

- [x] **B9 — Fix `CanIncludeInClipboardHistory` misread** (`Services/ClipboardWatcher.cs`) — read the 4-byte DWORD payload; skip only when value == 0. Apps that explicitly opt IN are now captured.
- [x] **B10 — First-run race ordering** (`App.xaml.cs`) — open Settings first, *then* show the welcome toast.
- [x] **F19 — Recent-clips flyout pivot** — opt-in `recent_clips` capture now surfaces a "Recent" section above the snippet list with click-to-copy + Promote-to-snippet via right-click.
- [x] **F20 — Group pivot chips** — flyout shows `[All] [Group A] [Group B] [Ungrouped]` chips above search when ≥1 group is defined; selection persists via `flyout.last_group_id`.
- [x] **F18 Phase 1 — Distribution unlock**: `<PublishSingleFile>true</PublishSingleFile>`, `app.manifest` with PerMonitorV2 DPI, GitHub Actions `release.yml` (portable + self-contained zips on `v*` tag) + `ci.yml` (Release build on every push), README install section rewrite.

### P1 — Closure features

- [x] **F21 — Restore-from-backup UI + startup `PRAGMA quick_check`** — corrupted DB prompts restore on launch; Settings has explicit "Restore backup…" button; pre-restore snapshot makes it reversible.
- [x] **F22 — Free-form quick-hotkey assignment** — replaced the fixed Ctrl+Alt+1..9 ComboBox with a Capture… button + display label. Any combo (e.g. Ctrl+Alt+S) is now valid. Refuses combos that clash with the primary hotkey or with the reserved Ctrl-only set (C/V/X/Z/Y/A/S/N/O/P/F/W/T).
- [x] **F23 — Trash bin UI** — new `TrashWindow` lists soft-deleted snippets with deleted-at + purge-in countdown; per-row Restore / Delete Permanently; Empty Trash. Settings → "Trash…" button.
- [x] **I16 (Option A) — Live theme swap via relaunch prompt** — picking a theme prompts to apply now; the relaunch reopens Settings via `--settings`.
- [x] **I17 — Hotkey registration status indicator** — green/red dot beside the primary hotkey display, fires off `HotkeyService.PrimaryRegistrationChanged`.
- [x] **I18 — Auto-paste fail toast** — one-time-per-session tray notification when `SetForegroundWindow` refuses (typically an elevated target).
- [x] **I20 — `app.manifest` with PerMonitorV2 DPI + gdiScaling** — included with the distribution unlock.
- [x] **I21 — Daily backup throttle** — `BackupRotator.Rotate` no longer runs on every launch; gated by `settings.backup.last_at` once per 24 h.
- [x] **I24 — JSON export carries `schemaVersion`** — payload tagged with `Migrations.CurrentVersion` so future importers can branch on the source schema.
- [x] **I26 — Generic seeded snippets** — replaced `"Best,\nMatt"` with `"Best,\n{{ask:Name}}"` so first-launch doesn't leak any identity.

### P2 — Power-user surfaces

- [x] **F24 — Send-as-keystrokes paste mode** — per-snippet `paste_mode` column (V3 migration); Settings dropdown "Auto (Ctrl+V)" / "Type characters"; uses `INPUT_KEYBOARD` + `KEYEVENTF_UNICODE` for apps that swallow Ctrl+V (terminals, RDP, password fields).
- [x] **F25 — Live placeholder preview pane** — editor shows what `Today is {{date}}.` resolves to as you type.
- [x] **F27 — Fuzzy / prefix-weighted search** — title-prefix > title-contains > body-contains scoring; fielded operators (`title:foo` / `body:foo`).
- [x] **F28 — Clipboard transforms** — pipe-chained: `{{clipboard|upper}}`, `{{clipboard|trim|lower}}`, `{{clipboard|jsonpretty}}`, `{{clipboard|urlencode}}`, `{{clipboard|urldecode}}`, `{{clipboard|base64}}`, `{{clipboard|base64decode}}`, `{{clipboard|sha256}}`, `{{clipboard|md5}}`, `{{clipboard|reverse}}`, `{{clipboard|length}}`. Unknown transforms degrade to identity.
- [x] **F29 — CLI scripting** — `--copy <id|title>` / `--paste <id|title>` / `--list` (writes `id\ttitle` to `%LOCALAPPDATA%\TaskCopy\snippets.list`) via the existing named-pipe IPC.
- [x] **I28 — "Copy diagnostics" button** — bundles version + schema + snippet count + hotkey state + last-backup + crash.log tail to clipboard as a Markdown block.
- [x] **I30 — README documents `--settings`/`--flyout`/`--copy`/`--paste`/`--list`**.
- [x] **I31 — README keyboard cheatsheet**.
- [x] **I32 — Settings list keyboard accelerators** — Del/Ctrl+N/F2.
- [x] **I33 — Keyboard focus outline** — `IsKeyboardFocused` triggers added to Mocha + Latte button templates.
- [x] **I34 — Status-bar snippet count** — "N snippets · M groups" in Settings status bar.

---

## v0.5.5 — "Multi-field forms" ✅ SHIPPED (2026-05-25)

- [x] **F36 — Multi-field forms** (`{{form:Field1|Field2}}`) — one modal gathers several values before expansion; matching `{{ask:Field}}` tokens reuse those values and repeated single-field asks are cached per expansion.

## v0.5.4 — "Espanso import" ✅ SHIPPED (2026-05-25)

- [x] **F38 — YAML import (Espanso compat)** — Settings import picker accepts `.yml` / `.yaml`; static Espanso `matches:` entries import as snippets grouped under the YAML filename. Dynamic Espanso behaviors (`regex`, `vars`, `form`, `image_path`, `html`, `markdown`) are skipped rather than converted incorrectly.

## v0.5.3 — Build hotfix ✅ SHIPPED (2026-05-25)

- [x] **MVVMTK0034 in `RevertBackupEncryptedBinding`** (v0.5.2 broke CI) — refactored to set the generated property under the suppress flag instead of `SetProperty(ref _field, ...)`.

## v0.5.2 — "Encrypted backups + per-app rules" ✅ SHIPPED (2026-05-25)

Schema bumped V4 → V5.

- [x] **F49** — Opt-in backup encryption. AES-256-GCM + PBKDF2-SHA256 (600k iters). New `Services/BackupCrypto.cs`. Settings checkbox "Encrypt backups with password" — disabling requires the current password. Lost password = unrecoverable backups; UX explicitly warns. Restore picker shows "encrypted (password required)" for `.enc` slots.
- [x] **F35** — Per-app rules. New `snippets.target_app_glob` column (V5). Comma-separated `*`-wildcard process-name patterns. Flyout VM honors it via `AppGlob.Matches(glob, capturedForegroundProcess)`. JSON export round-trips.
- [x] **I41 (light)** — Right-click context menu on Settings snippet list → "Move to group" submenu. Lighter than duplicating the flyout chip strip into Settings.

## v0.5.1 — "Multi-clip paste + flyout perf" ✅ SHIPPED (2026-05-25)

- [x] **F32** — Multi-clip paste. Ctrl+click / Ctrl+Space toggles selection; Enter pastes the concatenated bundle with `settings.multipaste.separator` (default `\n\n`). Esc has three-stage clear.
- [x] **I37** — `BulkObservableCollection<T>` for `Snippets` + `RecentClips`. `ApplyFilter` does one `CollectionChanged(Reset)` instead of N add/remove events.

## v0.5.0 — "Snippet history + stats + CLI reliability" ✅ SHIPPED (2026-05-25)

Schema bumped V3 → V4. Adds the most-requested data-safety net (per-snippet edit history) and unlocks future per-app rules.

- [x] **F46** — Body edit history. New `snippet_body_history` table (10 newest per snippet, FK CASCADE). History modal in Settings with Restore + per-row delete.
- [x] **F48** — Per-snippet last-paste target. Captures the target process name post auto-paste. Foundation for F35 per-app rules.
- [x] **F37** — Lifetime usage statistics in About: "You've pasted N snippets — about M minutes of typing TaskCopy did for you."
- [x] **I39** — `--copy`/`--paste`/`--list` write outcome to `%LOCALAPPDATA%\TaskCopy\.cli-result` so scripts can branch on the actual lookup result.
- [x] **B22** — Culture-sensitive string sort audit verified clean (every `Contains`/`StartsWith`/`Equals` already uses `StringComparison.OrdinalIgnoreCase`).

## v0.4.6 — "Sticky position + repo hygiene" ✅ SHIPPED (2026-05-25)

- [x] **F50 — "Last position (sticky)" flyout position** (new `FlyoutPosition.LastPosition`, persisted via `SettingsStore.FlyoutLastPosition`, restored on next open with monitor-work-area clamping).
- [x] **`CONTRIBUTING.md`** with build / commit / versioning conventions.
- [x] **`.github/ISSUE_TEMPLATE/`** (bug + feature) + **`.github/PULL_REQUEST_TEMPLATE.md`**.

## v0.4.5 — "Power-user integrations" ✅ SHIPPED (2026-05-25)

- [x] **F45 — `gh issue create` integration** for the "File issue" button. Falls back to clipboard when `gh` isn't on PATH.
- [x] **F44 (code-only) — `.taskpack` extension support** in the import dialog filter + button rename. Curation/index repo is separately-tracked work.
- [x] **I40 — Open in external editor** for the snippet body. Resolves `Settings.ExternalEditorCommand` → `$EDITOR` → `code --wait` → `notepad`.

## v0.4.4 — "Quality of life + Accessibility" ✅ SHIPPED (2026-05-25)

Phase B + selected Phase C items from `RESEARCH_FEATURE_PLAN.md`.

- [x] **F42 — High-contrast theme variant** (new `Themes/HighContrast.xaml`, auto-selects when `SystemParameters.HighContrast` is on).
- [x] **F47 — Don't-ask-again for delete confirm** (new `ConfirmDeleteWindow`, `settings.delete.skip_confirm`).
- [x] **F52 — Reset to defaults button** (clears settings KV only; preserves snippets/groups/trash).
- [x] **I35 — `Microsoft.Extensions.DependencyInjection` 9.0.0** (container built; existing wiring unchanged; v0.5 services prefer constructor injection).
- [x] **I38 — Test hotkey verification button** (catches the case where I17's status dot says "Active" but a third-party hotkey manager intercepts).
- [x] **B17 follow-up** — `OnUserPreferenceChanged` also reacts to HC-on/off transitions regardless of preference.

## v0.4.3 — "Make it build" ✅ SHIPPED (2026-05-25)

Phase A from `RESEARCH_FEATURE_PLAN.md` — restores green CI and closes the v0.4.x backup-safety + theme-runtime gaps.

- [x] **B16 — fix the 5 `-warnaserror` CI errors** (App.xaml.cs CS8604 guard, SettingsWindow.xaml.cs CS0103 using, SettingsViewModel.cs CS9273+CS9258 `field` rename, AboutWindow.xaml.cs IL3000 → `AppContext.BaseDirectory`).
- [x] **B17 — `Theme.Auto` follows runtime OS theme** via `SystemEvents.UserPreferenceChanged` + `ThemeService.SystemThemeChanged` event + I16-A relaunch prompt reuse.
- [x] **F41 / B20 — Backup snapshot verify** via `PRAGMA quick_check` against the freshly-written `.bak.0.db`; broken file is deleted and logged.
- [x] **B18 — README `--list` doc correction** (writes `%LOCALAPPDATA%\TaskCopy\snippets.list`, not stdout).
- [x] **B21 — Win+\* combos rejected in `IsReservedCombo`** with TaskCopy's tailored message.

## v0.4.2 — Async paste path ✅ SHIPPED (2026-05-25)

- [x] **I22** — `AutoPasteService.TryAutoPasteDetailedAsync` replaces the `Thread.Sleep`-on-dispatcher path with `await Task.Delay`. Sync overloads kept for callers not yet async-aware.

## v0.4.1 — Polish & reliability ✅ SHIPPED (2026-05-25)

- [x] **I19** — Active monitor center flyout position (Settings dropdown; persisted).
- [x] **I23** — Decay-weighted frecency in MostUsed sort.
- [x] **I25** — Flyout tooltip honors `IsMonospace`.
- [x] **I27** — Tray toast for dispatcher exceptions instead of modal MessageBox.
- [x] **I29** — Migration transaction boundary tightened (`ApplyV2`/`ApplyV3` pass tx to every DDL).
- [x] **B11** — Quick-hotkey clashes with primary surface a clear message (closed by F22 capture path).
- [x] **B13** — fsync backup file after VACUUM INTO via `FileStream.Flush(true)`.
- [x] **B14** — `{{cursor}}` cap warning toast when offset > 5000.
- [x] **B15** — Registry-canonical Start-with-Windows reconcile on load.

## Active backlog — future releases

This is the only active to-do checklist. `RESEARCH_FEATURE_PLAN.md` is archived evidence and acceptance detail; completed work belongs in `CHANGELOG.md`.

### App features

- [ ] **F33 — Image clipboard support** — capture/paste CF_DIB with thumbnails and explicit privacy limits.
- [ ] **F39 — `{{shell:cmd}}` evaluation** — opt-in per snippet, disabled on import, with a warning dialog before first execution.
- [ ] **F40 — Syntax-highlighted body editor** — AvalonEdit when `IsMonospace`, with plain TextBox fallback.
- [ ] **F51 — FTS5 search at scale** — body search index once libraries cross the large-snippet threshold.
- [ ] **F30 — Encrypted snippet store** — BYO password; likely SQLCipher dependency swap. Separate from already-shipped encrypted backups.
- [ ] **F31 — BYO cloud sync** — S3 / B2 / Dropbox via user credentials.

### Platform and integration

- [ ] **Windhawk companion mod** (`windhawk/taskcopy-taskbar-menu.wh.cpp`) — literal taskbar context-menu trigger using the stable IPC / CLI path.
  - Hook `TaskbarResources::OnTaskListButtonContextRequested` (Win11) + `CTaskListWnd::_HandleContextMenu` (Win10).
  - Inject "TaskCopy >" submenu populated from named-pipe IPC + `--copy <id>` callback.
  - README install steps + submit to `ramensoftware/windhawk-mods` (self-host fallback).
- [ ] **F26 — Velopack in-app auto-update** — depends on release artifacts being stable.
- [ ] **F43 — Localisation hook + en-US baseline** — resource framework, translator docs, one proof culture.
- [ ] **I16 (Option B)** — DynamicResource refactor for fully-live theme swap.
- [ ] **I36 — SettingsViewModel split** — extract editor, diagnostics, hotkey/startup, backup/encryption responsibilities.

### Distribution polish

- [ ] **F18 Phase 2 — winget manifest** — submit to `microsoft/winget-pkgs`.
- [ ] **F18 Phase 3 — Authenticode signing cert** — SmartScreen reputation + stable tray-icon GUID.
- [ ] **MSIX packaging + Microsoft Store submission**.
- [ ] **Screenshots captured at 125% DPI** to `assets/screenshots/` (light + dark).

---

## Out-of-scope (decided NO, not deferred)

- **Deskband / `IDeskBand` shell extension** — removed in Win11, dead end
- **`IContextMenu` / `IShellExtInit` shell extension** — wrong surface (targets files/folders, not taskbar)
- **Overriding `Win+V`** — owned by Microsoft, low-level keyboard hook is AV-flagged
- **Fork of ExplorerPatcher** — legal issues (taskbar reimpl is GPL-2.0 + author has restricted derivative distribution)
- **Keyboard-trigger expansion (Espanso-style `;sig` autocomplete)** — same `SetWindowsHookEx` AV issue; stay picker-based
- **Clipboard-history-as-default** — F15 + F19 are opt-in only; keep snippet-curated identity
- **Generic scripting / arbitrary keyboard-macro automation** — `SendInput` stays internal to the auto-paste + send-as-typing paths
- **Tests** — per CLAUDE.md, no tests unless explicitly requested
