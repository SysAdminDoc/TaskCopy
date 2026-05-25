# TaskCopy — Research and Feature Plan (post-v0.4 pass)

**Date:** 2026-05-25
**Branch / head:** `master` @ v0.4.0 ship
**Reviewer:** autonomous development pass (this pass implemented Phase A+B of the prior plan).
**Companion to:** [`ROADMAP.md`](ROADMAP.md), [`research/architecture-research.md`](research/architecture-research.md), [`CHANGELOG.md`](CHANGELOG.md)

---

## Status — what changed in this pass

v0.4.0 shipped Phase A (v0.3 closure + correctness) and Phase B (power-user surfaces + distribution) of the prior plan. The complete change list is in [`CHANGELOG.md`](CHANGELOG.md#040--2026-05-25); the high-level checkboxes are in [`ROADMAP.md`](ROADMAP.md#v040--polish--distribution--shipped-2026-05-25). What follows is a deduplicated companion focused on **what's still open after v0.4**.

### Closed by v0.4

| ID | Title | Note |
|---|---|---|
| B9 | `CanIncludeInClipboardHistory` misread | Reads the 4-byte payload; 0=exclude, 1=include. |
| B10 | First-run race ordering | Settings opens before toast. |
| F18 (Phase 1) | Single-file publish + GH Actions | Portable + self-contained zips on `v*` tag. |
| F19 | Recent-clips flyout pivot | "Recent" section with promote-to-snippet. |
| F20 | Flyout group pivot chips | Persists `flyout.last_group_id`. |
| F21 | Restore-from-backup + `PRAGMA quick_check` | Startup integrity check + Settings button. |
| F22 | Free-form quick-hotkey | Capture button; rejects reserved combos. |
| F23 | Trash bin UI | Restore / Delete Permanently / Empty Trash. |
| F24 | Send-as-keystrokes paste mode | `paste_mode` column (V3) + Unicode SendInput. |
| F25 | Live placeholder preview | Editor pane shows `Expand` result. |
| F27 | Fuzzy / prefix-weighted search | Title-prefix > title-contains > body-contains. |
| F28 | Clipboard transforms | upper/lower/trim/jsonpretty/url/base64/sha256/md5. |
| F29 | CLI scripting flags | `--copy` / `--paste` / `--list`. |
| I16 (Option A) | Live theme swap via relaunch | Prompt-to-restart; reopens Settings. |
| I17 | Hotkey registration status indicator | Green/red dot in Settings. |
| I18 | Auto-paste fail toast | One-time-per-session via `AutoPasteService.Result`. |
| I20 | `app.manifest` PerMonitorV2 DPI | + gdiScaling, asInvoker. |
| I21 | Daily backup throttle | Gated by `settings.backup.last_at`. |
| I24 | JSON export carries `schemaVersion` | Migrations.CurrentVersion at export time. |
| I26 | Generic seeded snippets | `{{ask:Name}}` instead of `Matt`. |
| I28 | Copy diagnostics button | Markdown bundle to clipboard. |
| I30 | README documents CLI flags | And winget plan. |
| I31 | README keyboard cheatsheet | Plus tooltips on Settings controls. |
| I32 | Settings list accelerators | Del / Ctrl+N / F2. |
| I33 | Keyboard focus outline | Mocha + Latte button templates. |
| I34 | Settings status-bar snippet count | Live via collection-changed. |

---

## Open work (v0.5+)

The current ROADMAP v0.5 section enumerates these; this file just adds the file-level evidence + acceptance criteria each one needs.

### Larger features (deferred until user signal)

- **F26 — Velopack in-app auto-update**
  - Why deferred: depends on F18 Phase 1 binaries gaining real-world usage; until there's a v0.4.x release cadence in the wild, the auto-update plumbing has nothing to test against.
  - Touches: new `Services/UpdateService.cs`; `App.OnStartup` async check; Settings KV `update.mode` (default = "notify"); release-workflow extension to emit Velopack deltas alongside the plain zip.
  - Verification: stage a `v0.4.1` release; install `v0.4.0`; wait < 10 min; toast + Settings shows "Update available".

- **F30 — Encrypted snippet store (BYO password)**
  - Why deferred: dep swap from `Microsoft.Data.Sqlite` → SQLitePCL.raw + bundle_e_sqlcipher (or similar). Non-trivial; the user can disable `RecentClipsEnabled` to keep clipboard captures out of the file today.
  - Touches: `Data/SnippetDatabase.cs` opener; new password-on-first-launch + change-password flows; `PRAGMA key` integration.

- **F31 — BYO cloud sync (S3/B2/Dropbox)** — already in v0.5 ROADMAP; no design needed beyond credentials UX and conflict resolution.

- **F32 — Multi-clip paste** — checkbox per row in the flyout; assemble with a separator; one Ctrl+V. Cheap; ship when there's demand.

- **F33 — Image clipboard support** — adds `body_blob BLOB NULL`, `mime_type TEXT NULL` columns + `CF_DIB` handling. Significantly enlarges surface; defer.

- **F34 — Open body in external editor** — spawn `$EDITOR` / `code` / `notepad++` on the body content, re-read on close. P2 if a code-heavy user asks.

- **F35 — Per-app rules** — `target_app_glob TEXT NULL` column; foreground-window process matching at paste time. Power-user only.

- **F36 — Multi-field forms** (`{{form:Field1|Field2}}`) — extends `AskWindow.Prompt` into a modal with N TextBoxes. Pairs well with F39.

- **F37 — Usage statistics** — track snippet copies → estimate time saved. About-window gimmick.

- **F38 — YAML import (Espanso compat)** — parser for Espanso's `matches:` blocks. Community snippet-pack enabler.

- **F39 — `{{shell:cmd}}` evaluation** — code-execution vector; opt-in per snippet; warning dialog. Ship carefully.

- **F40 — Syntax-highlighted body editor** — AvalonEdit; one NuGet. P3 nice-to-have.

### Polish (small but defer-worthy)

- **I16 Option B — DynamicResource refactor** — convert Mocha/Latte brush references from `StaticResource` to `DynamicResource` so theme swap becomes truly live. Touches every `Style.Setter` that references a `*.Brush` resource — large blast radius for a small payoff (Option A's relaunch UX is acceptable).

- **I19 — "Active monitor center" flyout positioning** — for ultrawide users. Already on the ROADMAP.

- **I22 — Move `Thread.Sleep` off the dispatcher** — refactor `AutoPasteService.TryAutoPasteDetailed` to `async`. Defensive; the 30ms+15ms is below human-perceptible.

- **I23 — Decay-weighted frecency** — `count × exp(-Δt/τ)` in `SnippetMenuViewModel.SortForFlyout`. One pure-VM change.

- **I25 — Flyout tooltip honors `IsMonospace`** — `FontFamily` binding on the tooltip Style based on the row's `IsMonospace`.

- **I27 — Tray toast for dispatcher exceptions** — replace `MessageBox.Show` with `_trayIcon.ShowNotification` (and keep the modal only when `Application.Current` is null).

- **I29 — Tighten `ApplyV2`/`ApplyV3` transaction boundary** — DDL inside the explicit tx + a single `SetUserVersion` only after every ALTER succeeds. Defensive; no observed bug.

### Bug carries (no observed user impact today)

- **B11** — Quick-hotkey collision with primary surfaces clearer message.
- **B12** — Snippet-list bulk-update perf at >500 items.
- **B13** — fsync backup file after `VACUUM INTO`.
- **B14** — `{{cursor}}` left-arrow cap warning toast.
- **B15** — Reconcile `StartupService.IsEnabled` (registry) vs `SettingsStore.StartWithWindows` (DB).

### Distribution polish

- **F18 Phase 2 — winget manifest** (submit to `microsoft/winget-pkgs`). Needs a stable, signed binary URL.
- **F18 Phase 3 — Authenticode signing cert** — fixes SmartScreen reputation + tray-icon GUID persistence across exe path changes.
- **MSIX packaging + Microsoft Store submission**.
- **Screenshots at 125% DPI** to `assets/screenshots/` (light + dark) → link from README.

### v0.5 headline — Windhawk companion mod

The last v0 ROADMAP commitment: hook `TaskbarResources::OnTaskListButtonContextRequested` (Win11) + `CTaskListWnd::_HandleContextMenu` (Win10) and inject a "TaskCopy ▶" submenu populated from named-pipe IPC + the new `--copy <id>` callback (F29 makes this trivial). Mod ID: `taskcopy-taskbar-menu`. Self-host fallback if the `ramensoftware/windhawk-mods` PR is slow.

---

## Architecture notes

### Codebase shape after v0.4

- 33 source files (up from 29 at v0.3.0).
- New services: `SnippetTransforms.cs`, `SnippetMatch.cs`.
- New views/VMs: `TrashWindow.xaml(.cs)`, `TrashViewModel.cs`.
- New CI/release plumbing: `.github/workflows/{release,ci}.yml`, `src/TaskCopy/app.manifest`.
- Schema at V3 (`paste_mode` added).
- `App.xaml.cs` is now ~580 LOC. The split candidates called out previously (`TrayOrchestrator`, `SnippetCopyOrchestrator`) become more attractive as the file approaches 800 LOC; defer until F26 (auto-update) adds the next service.

### Test gaps (still per CLAUDE.md, no tests)

When CLAUDE.md's "no tests" rule is overridden, the highest-ROI tests in order:
1. `SnippetTemplating.Expand` w/ pipe-chained transforms (F28 surface).
2. `SnippetMatch.Score` for fielded + unfielded query shapes.
3. `Migrations.Apply` v1→v2→v3 against an `:memory:` SQLite DB.
4. `SnippetIO.Export` → `Import` round-trip equivalence including the new `pasteMode` + `schemaVersion` fields.
5. `BackupRotator.ListAvailable` + `RestoreFrom` happy + corrupt-source paths.
6. `HotkeyService.TryParseHotkey` — every valid + every documented invalid syntax.
7. `ClipboardWatcher.TryReadDwordFormat` for B9 regression.

### Documentation gaps

- **Screenshots still pending.** README references them implicitly; the actual `assets/screenshots/` directory does not exist. Capture at 125% DPI both Mocha + Latte.
- **No `CONTRIBUTING.md`.** Repo is public/MIT; a one-pager covering build steps + commit conventions (no Co-Authored-By, no AI-attribution) would help drive-by PRs.
- **`docs/` is empty.** Could host a component map or design notes; not load-bearing.

---

## Explicit non-goals (carried forward)

- Clipboard-history-as-default (F19 + F15 stay opt-in).
- Trigger-based text expansion (`SetWindowsHookEx`).
- Overriding `Win+V`.
- Cross-platform.
- Cloud sync as a SaaS.
- Generic scripting / arbitrary keyboard-macro automation.
- Tests in pre-CLAUDE.md-override passes.

---

*End of report. ROADMAP.md remains the user-facing source of truth. Next pass should pick up from v0.5 (Windhawk companion mod + F26 auto-update + F18 Phase 2 winget).*
