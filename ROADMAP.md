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

## v0.5.0 — Future (not committed)

- [ ] **Windhawk companion mod** (`windhawk/taskcopy-taskbar-menu.wh.cpp`) — depends on F18+F29 stable IPC primitive.
  - Hook `TaskbarResources::OnTaskListButtonContextRequested` (Win11) + `CTaskListWnd::_HandleContextMenu` (Win10)
  - Inject "TaskCopy ▶" submenu populated from named-pipe IPC + `--copy <id>` callback
  - README install steps + submit to `ramensoftware/windhawk-mods` (self-host fallback)
- [ ] **F26 — Velopack in-app auto-update** — depends on F18 release pipeline being stable.
- [ ] **F30 — Encrypted snippet store** (BYO password; SQLCipher dep swap). Non-trivial dep change.
- [ ] **F31 — BYO cloud sync** (S3 / B2 / Dropbox via user creds).
- [ ] **F32 — Multi-clip paste** (assemble several entries then paste once).
- [ ] **F33 — Image clipboard support** (CF_DIB + thumbnails).
- [ ] **F34 — Open body in external editor** ($EDITOR / `code` / `notepad++`).
- [ ] **F35 — Per-app rules** (`target_app_glob` column).
- [ ] **F36 — Multi-field forms** (`{{form:Field1|Field2}}`).
- [ ] **F37 — Usage statistics** ("you saved 4 minutes today").
- [ ] **F38 — YAML import** (Espanso compat).
- [ ] **F39 — `{{shell:cmd}}` evaluation** (opt-in per-snippet, warning dialog).
- [ ] **F40 — Syntax-highlighted body editor** (AvalonEdit when `IsMonospace`).
- [ ] **I16 (Option B)** — DynamicResource refactor for fully-live theme swap.
- [ ] **I22 — Move `Thread.Sleep` off the dispatcher in `AutoPasteService`**.
- [ ] **B12 — Snippet-list bulk-update perf at >500 items** (`AddRange`/deferred refresh).

### Distribution polish (post-F18-Phase-1)

- [ ] **F18 Phase 2 — winget manifest** (submit to `microsoft/winget-pkgs`).
- [ ] **F18 Phase 3 — Authenticode signing cert** for SmartScreen reputation + stable tray-icon GUID.
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
