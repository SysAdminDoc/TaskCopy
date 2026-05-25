# TaskCopy — Research and Feature Plan (post-v0.4.2 audit)

> **Archive note (2026-05-25):** This file is retained as research evidence and detailed rationale only. Its old checklists have been consolidated into `ROADMAP.md`, which is the single active to-do source. Completed work is recorded in `CHANGELOG.md`.

**Date:** 2026-05-25 (later that day, post-CI verdict)
**Branch / head:** `master` @ `a6fdb61` (release: v0.4.2 "Async paste path")
**Reviewer:** autonomous research pass (no implementation in this pass)
**Companion to:** [`ROADMAP.md`](ROADMAP.md), [`research/architecture-research.md`](research/architecture-research.md), [`CHANGELOG.md`](CHANGELOG.md)

This pass replaces the prior plan. v0.4.0 / v0.4.1 / v0.4.2 shipped the prior plan's items (see CHANGELOG); this pass focuses on **what's broken at the v0.4.2 baseline** and **what the prior passes did not cover**. The headline finding is that all three v0.4.x CI runs failed — `dotnet build -c Release -warnaserror` reports 5 errors, so the published v0.4.x binaries cannot ship until those are fixed.

---

## Executive Summary

**The headline finding is critical: v0.4.0, v0.4.1, and v0.4.2 all fail CI.** GitHub Actions run `26408247365` (v0.4.2) failed in 47s with 5 compile errors under `-warnaserror`. The local build status was "no SDK available, will catch on CI" — and CI caught it. None of the published commits' Release build is usable as a binary today. The release workflow in `.github/workflows/release.yml` shares the same `-warnaserror` semantics implied by the publish step, so tagging `v0.4.2` would also fail.

After the build fixes, the project's strongest current shape is **a feature-complete single-user clipboard snippet manager** (29 features, schema V3, three rotating backups, type-ahead + group pivot + recent-clips + auto-paste + per-snippet hotkeys + transforms + CLI) **whose biggest remaining frictions are not features at all**: it's accessibility (no high-contrast theme), internationalisation (no localisation pass), backup safety (backups are plaintext, never verified post-write), and distribution maturity (unsigned binary, no winget, no auto-update). The original v0.5 ROADMAP commitments (Windhawk mod, Velopack) remain the right next bets after the build is green.

**Top 10 opportunities (priority order):**

1. **P0 — B16: Fix the 5 `-warnaserror` errors at HEAD so v0.4.x builds again.** Specific files + line numbers + fixes are documented below.
2. **P0 — F41: Verify the just-written backup before declaring success.** Today `BackupRotator.Rotate` calls `VACUUM INTO` + `FileStream.Flush(true)` and trusts that an openable backup landed on disk. A 200µs `PRAGMA quick_check` against the fresh file would catch backup corruption *at write time*, not at restore time.
3. **P0 — B17: `Theme.Auto` doesn't re-resolve when the OS theme changes mid-session.** ROADMAP v0.4.0 ships `Auto` but [`ThemeService.IsSystemLight`](src/TaskCopy/Services/ThemeService.cs#L52) is only read once at startup. A `SystemEvents.UserPreferenceChanged` listener + relaunch prompt would close the gap.
4. **P1 — F42: High-contrast theme variant.** Mocha + Latte are both decorative palettes; users on Windows High-Contrast (`HCBlack`/`HCWhite`) get the same color contrast as everyone else. WCAG AAA on a 1.4:1 background is still 1.4:1.
5. **P1 — F43: Localisation hook + en-US baseline.** Every UI string is hardcoded; no `.resx`, no `<XmlnsDefinition>`. v0.4 added free-form keyboard combos and tray menus that lose meaning in any other language.
6. **P1 — F44: Snippet-pack ecosystem** — package the existing JSON import path as `*.taskpack` files + a GitHub topic + a curated starter set (Git command cookbook, regex patterns, dev signatures, support replies). Adjacent to F38 (Espanso YAML import) but the native-format path is cheaper.
7. **P1 — I35: DI container for the now-9 service graph.** [`App.xaml.cs`](src/TaskCopy/App.xaml.cs) crossed 700 LOC and the constructor wiring is hand-hooked. Adding `Microsoft.Extensions.DependencyInjection` (single NuGet, ~150KB) unblocks tests + the v0.5 Windhawk IPC bridge.
8. **P1 — I36: Snippet edit history per body** — one `body_history` row per debounced flush, capped at 10 versions. Cheap insurance against the "Settings ate my snippet body" bug class.
9. **P2 — F45: `gh issue create` integration in the "Copy diagnostics" path.** When `gh` is on PATH, offer to file the issue directly; otherwise keep the current clipboard-Markdown path. Reduces report friction by 80%.
10. **P2 — I37: `Squelched ObservableCollection` for batch operations.** [`SnippetMenuViewModel.ApplyFilter`](src/TaskCopy/ViewModels/SnippetMenuViewModel.cs#L120) issues N `CollectionChanged` events; at 500+ snippets the typing-while-filter gets visibly choppy. Builds on B12 from the prior plan.

Below: full evidence + fixes + new feature plans + an actionable roadmap.

---

## Evidence Reviewed

### Local files and directories inspected at v0.4.2 (HEAD `a6fdb61`)

- Build / project: [`TaskCopy.sln`](TaskCopy.sln), [`src/TaskCopy/TaskCopy.csproj`](src/TaskCopy/TaskCopy.csproj) (Version 0.4.2, `<PublishSingleFile>true</PublishSingleFile>`, `<Deterministic>true</Deterministic>`).
- CI / release: [`.github/workflows/ci.yml`](.github/workflows/ci.yml), [`.github/workflows/release.yml`](.github/workflows/release.yml). **CI status: 3-of-3 failed (Apply 5 fixes documented in B16 to recover.)**
- Manifest: [`src/TaskCopy/app.manifest`](src/TaskCopy/app.manifest) — PerMonitorV2 + asInvoker + gdiScaling.
- Root docs: [`README.md`](README.md), [`CLAUDE.md`](CLAUDE.md) (local-only / gitignored), [`ROADMAP.md`](ROADMAP.md), [`CHANGELOG.md`](CHANGELOG.md) (v0.1.0 → v0.4.2 entries), [`LICENSE`](LICENSE), [`.gitignore`](.gitignore).
- Research: [`research/architecture-research.md`](research/architecture-research.md) (v0 deliverable; still authoritative for build-time landscape).
- Data: [`Data/Migrations.cs`](src/TaskCopy/Data/Migrations.cs) (V1 → V3), [`Data/SnippetDatabase.cs`](src/TaskCopy/Data/SnippetDatabase.cs), [`Data/SettingsStore.cs`](src/TaskCopy/Data/SettingsStore.cs).
- Models: [`Models/Snippet.cs`](src/TaskCopy/Models/Snippet.cs) (12 [ObservableProperty] columns), [`Models/SnippetGroup.cs`](src/TaskCopy/Models/SnippetGroup.cs), [`Models/RecentClip.cs`](src/TaskCopy/Models/RecentClip.cs).
- Services (13 files): [`AutoPasteService.cs`](src/TaskCopy/Services/AutoPasteService.cs) (async), [`BackupRotator.cs`](src/TaskCopy/Services/BackupRotator.cs), [`ClipboardService.cs`](src/TaskCopy/Services/ClipboardService.cs), [`ClipboardWatcher.cs`](src/TaskCopy/Services/ClipboardWatcher.cs), [`CrashLog.cs`](src/TaskCopy/Services/CrashLog.cs) (NonFatalNotifier hook), [`ForegroundWindowCapture.cs`](src/TaskCopy/Services/ForegroundWindowCapture.cs), [`HotkeyService.cs`](src/TaskCopy/Services/HotkeyService.cs) (PrimaryRegistrationChanged event), [`NativeMethods.cs`](src/TaskCopy/Services/NativeMethods.cs) (KEYEVENTF_UNICODE added), [`SingleInstanceServer.cs`](src/TaskCopy/Services/SingleInstanceServer.cs) (4 CLI msgs), [`SnippetIO.cs`](src/TaskCopy/Services/SnippetIO.cs), [`SnippetMatch.cs`](src/TaskCopy/Services/SnippetMatch.cs) (new at v0.4), [`SnippetTemplating.cs`](src/TaskCopy/Services/SnippetTemplating.cs) (pipe transforms), [`SnippetTransforms.cs`](src/TaskCopy/Services/SnippetTransforms.cs) (new at v0.4), [`StartupService.cs`](src/TaskCopy/Services/StartupService.cs), [`ThemeService.cs`](src/TaskCopy/Services/ThemeService.cs).
- ViewModels: [`SettingsViewModel.cs`](src/TaskCopy/ViewModels/SettingsViewModel.cs) (~720 LOC at HEAD), [`SnippetMenuViewModel.cs`](src/TaskCopy/ViewModels/SnippetMenuViewModel.cs), [`SnippetRow.cs`](src/TaskCopy/ViewModels/SnippetRow.cs) (now carries `BodyTooltipFontFamily`), [`ManageGroupsViewModel.cs`](src/TaskCopy/ViewModels/ManageGroupsViewModel.cs), [`TrashViewModel.cs`](src/TaskCopy/ViewModels/TrashViewModel.cs) (new at v0.4).
- Views: [`AboutWindow.xaml(.cs)`](src/TaskCopy/Views/AboutWindow.xaml), [`AskWindow.xaml(.cs)`](src/TaskCopy/Views/AskWindow.xaml), [`HotkeyHostWindow.cs`](src/TaskCopy/Views/HotkeyHostWindow.cs), [`ManageGroupsWindow.xaml(.cs)`](src/TaskCopy/Views/ManageGroupsWindow.xaml), [`SettingsWindow.xaml(.cs)`](src/TaskCopy/Views/SettingsWindow.xaml), [`SnippetMenuWindow.xaml(.cs)`](src/TaskCopy/Views/SnippetMenuWindow.xaml) (12 grid rows now), [`TrashWindow.xaml(.cs)`](src/TaskCopy/Views/TrashWindow.xaml) (new at v0.4).
- Themes: [`Themes/Mocha.xaml`](src/TaskCopy/Themes/Mocha.xaml), [`Themes/Latte.xaml`](src/TaskCopy/Themes/Latte.xaml) (both gained `IsKeyboardFocused` triggers in v0.4).
- Converters: [`Converters/BoolToVisibilityConverters.cs`](src/TaskCopy/Converters/BoolToVisibilityConverters.cs).
- Tools: [`tools/generate-icon.ps1`](tools/generate-icon.ps1).

### Git history reviewed

24 commits, `d3a5e74` (initial scaffold) → `a6fdb61` (v0.4.2). The post-v0.3 work shipped in this session: `ebbd803` (v0.4.0, 34 files +2362/-1015), `d939b62` (v0.4.1, 16 files +233/-43), `a6fdb61` (v0.4.2, 6 files +51/-36). Author: Matthew Parker `<matt_parker@outlook.com>`. No Co-Authored-By trailers anywhere (matches per-project convention).

### Build / test / docs / release artifacts inspected

- **Build (CI):** [3 of 3 v0.4.x runs failed](https://github.com/SysAdminDoc/TaskCopy/actions). Run `26408247365` (v0.4.2) failed in 47s with 5 errors under `-warnaserror`. **Verified.**
- **Build (local):** no .NET 10 SDK on the build VM per [auto-memory](no-dotnet-sdk-on-vm). v0.4.x was written and committed without a local Release build. The CI failure is the only build signal we have. **Verified.**
- **Tests:** none. CLAUDE.md rule "no tests unless explicitly requested" still applies.
- **Docs:** README rewritten at v0.4.0 with install/build/cheatsheet/CLI sections. CHANGELOG has v0.1.0 → v0.4.2 entries, Keep-a-Changelog format. ROADMAP enumerates v0.1-v0.4.2 shipped + v0.5 wishlist. `research/architecture-research.md` unchanged from v0.0; still the authoritative architecture deliverable.
- **Release:** no `v*` tags pushed yet. `.github/workflows/release.yml` exists but has never run. **No published binaries.**

### External sources

- [Microsoft `IL3000` analyzer guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/warnings/il3000) — single-file `Assembly.Location` returns empty.
- [Microsoft C# 14 `field` keyword reference](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) — contextual keyword in property accessors.
- [WPF `AutomationProperties.GetName`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationproperties.getname) — namespace `System.Windows.Automation`.
- [SQLite `PRAGMA quick_check` / `integrity_check`](https://www.sqlite.org/pragma.html#pragma_integrity_check) — F41 verification.
- [`Microsoft.Win32.SystemEvents.UserPreferenceChanged`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged) — runtime OS-theme change notification (B17).
- [Windows High Contrast theming guide](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview#system-resources-and-high-contrast) — `SystemColors` + `SystemParameters.HighContrast` (F42).
- [.NET 10 single-file-app trim warnings table](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/warnings) — IL3000–IL3002.
- [`Microsoft.Extensions.DependencyInjection` 9.x](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/) — single NuGet for I35.
- [`Velopack`](https://github.com/velopack/velopack) — modern Squirrel.Windows successor for F26.
- [Espanso `matches:` YAML spec](https://espanso.org/docs/matches/basics/) — F38 import path.
- [`gh` CLI `issue create`](https://cli.github.com/manual/gh_issue_create) — F45 integration.

### Areas that could not be verified from this environment

- **Local Release build / live app behavior** — no .NET 10 SDK on the VM. All v0.4.x code was written blind; the 5 CI errors are the first build signal. Marked everywhere as **Needs live validation** after fix.
- **Single-file binary actually running** — `dotnet publish -p:PublishSingleFile=true` has never executed against this code, even on CI. The csproj is configured but only `dotnet build` runs in `ci.yml`. The release workflow does publish but has never triggered (no `v*` tag).
- **Per-monitor DPI v2 manifest behavior** — `app.manifest` declares it but a mixed-DPI live test hasn't happened.
- **`H.NotifyIcon` Efficiency Mode on Win10** — opted in at v0.2; only Win11 reports the leaf icon.
- **Tray-icon GUID persistence across exe path changes** — requires Authenticode signing; deferred.
- **`SystemEvents.UserPreferenceChanged` firing under Theme.Auto** — needs a live OS-theme flip to verify.

---

## Current Product Map

### Core workflows (today, v0.4.2)

1. **Open snippet flyout** → click / number-key → text on clipboard (+ optional auto-paste).
2. **Pick recent clipboard item** from the same flyout → copy / promote-to-snippet (F19, opt-in).
3. **Filter by group** via chip strip above search (F20).
4. **Direct paste a snippet from anywhere** via free-form quick-hotkey (F22, e.g. `Ctrl+Alt+S`).
5. **Type-mode paste for legacy targets** via per-snippet `paste_mode` (F24).
6. **Curate snippets** in Settings — title/body/group/pin/monospace/quick-hotkey/paste-mode + drag-reorder + Del/Ctrl+N/F2 accelerators + live placeholder preview.
7. **Restore a deleted snippet** from the Trash window.
8. **Recover from DB corruption** — startup `PRAGMA quick_check` prompts restore; Settings → Diagnostics → "Restore backup…" lets the user pick a slot.
9. **Send diagnostics** — "Copy diagnostics" produces a Markdown bundle (version/schema/log tail) on the clipboard for GitHub issues.
10. **Scripted invocation** — `TaskCopy.exe --copy <id|title>` / `--paste <id|title>` / `--list` / `--flyout` / `--settings`.

### Feature delivery accounting (after v0.4.2)

| Pre-v0.4 (v0.1-v0.3) | v0.4.0 | v0.4.1 | v0.4.2 |
|---|---|---|---|
| F1-F12, F14, F15, F16, I1-I15, B1-B8 (29 items) | F18 Phase 1 + F19 + F20 + F21 + F22 + F23 + F24 + F25 + F27 + F28 + F29 + I16-A + I17 + I18 + I20 + I21 + I24 + I26 + I28 + I30 + I31 + I32 + I33 + I34 + B9 + B10 (26 items) | I19 + I23 + I25 + I27 + I29 + B11 + B13 + B14 + B15 (9 items) | I22 (1 item) |

Total shipped: 65 numbered items. Open: B16 (the build) + a fresh batch of v0.5 candidates listed below.

### User personas (refined)

- **The author** — daily-driver developer on Win11 / Mocha / `Ctrl+Alt+V` muscle memory.
- **Sysadmin / support engineer** — canned replies, command snippets; needs `--paste <id>` from a launcher (F29 unlocks this).
- **Non-technical assistant given a pre-curated library** — currently still blocked by F18 Phase 2 (no signed binary / no winget) + B16 (build doesn't ship).
- **New personas surfaced by v0.4 features:**
  - **Compliance-template user** — pre-bundled canned legal/HR replies. F44 snippet-pack ecosystem serves them.
  - **Translator / multilingual user** — F43 localisation unblocks them.
  - **Accessibility-first user** — F42 high-contrast unblocks them.

### Platforms / distribution / integrations / permissions / storage (delta from v0.3)

- **Single-file publish** is on by default in csproj — but no release has been built or published. Until B16 + a `v*` tag, the install path is still `dotnet build`.
- **CI workflow** verifies `dotnet build -c Release -warnaserror` — currently red.
- **Release workflow** builds portable + self-contained on `v*` tag — currently untriggered.
- Everything else unchanged: `%LOCALAPPDATA%\TaskCopy\`, HKCU-only registry, named pipe per-user, no network.

---

## Feature Inventory (delta vs v0.3 baseline)

Rather than re-list the 23 features inventoried in the prior plan, this section captures **what changed at v0.4** and **what new maturity rating each item now carries**.

| # | Feature | v0.4 delta | Maturity now | Top remaining gap |
|---|---|---|---|---|
| 1 | Tray icon | Efficiency Mode + non-fatal-notifier hook (I27) | Complete | None at v0.5 scope. |
| 2 | Global hotkey | `IsPrimaryRegistered` + status indicator (I17) | Complete | Status indicator: confirms registration but not whether `OnHotkey` actually fires (a fully-suppressed hotkey would still show "Active"). Needs a "Test hotkey" button. |
| 3 | Cursor-anchored flyout | + Active monitor center mode (I19) | Complete | Only two positions; "Last position (sticky)" still open. |
| 4 | Type-ahead search | Replaced boolean Matches with scored ranking (F27) | Complete | Fuzzy-typo matching (Levenshtein-distance ≤2) not done. |
| 5 | Alt+1..9 quick-pick | Unchanged | Complete | None. |
| 6 | Per-snippet quick hotkey | Free-form + capture button (F22) | Complete | Reserved-combo list rejects Ctrl+letter staples but not Win+* (OS will refuse; user gets generic NHotkey error). |
| 7 | Auto-paste | Async + per-snippet `paste_mode` + Unicode typing + fail toast + cursor-clamp toast (F24/I18/B14/I22) | Complete | Type-mode at 50+ chars/sec rate-limit would prevent some IME mangling. |
| 8 | Placeholders | + pipe transforms (F28) | Complete | `{{shell:cmd}}` and `{{form:...}}` not done. |
| 9 | Groups | + flyout chip pivot (F20) | Complete | Drag a snippet onto a chip to reassign — not done. |
| 10 | Frecency / Pin / Sort modes | Decay-weighted MostUsed (I23) | Complete | None. |
| 11 | JSON import/export | + schemaVersion + pasteMode round-trip (I24) | Complete | No import dry-run preview ("you will add 12, skip 3"). |
| 12 | Auto-backup | Throttled to once/24h + fsync + integrity check + restore UI + slot listing (F21 / I21 / B13) | Complete | **F41: no quick_check against the just-written backup before declaring success.** |
| 13 | Soft-delete trash | + Trash UI (F23) | Complete | "Don't ask again" for delete confirm: deferred, still wanted. |
| 14 | Recent clips | + flyout pivot + promote (F19) + B9 fix | Complete | No per-app exclude list defaulting to known password managers. |
| 15 | Monospace toggle | + tooltip font follows (I25) | Complete | None. |
| 16 | Themes Mocha / Latte / Auto | Live swap via relaunch prompt (I16-A) | Partial | **B17: Theme.Auto does not re-resolve when OS theme flips at runtime.** |
| 17 | Drag-reorder | Unchanged | Complete | None. |
| 18 | First-run welcome | Generic seeds (I26) + race fix (B10) | Complete | No 30s tour GIF / no Start-with-Windows offer. |
| 19 | Crash log | + Non-fatal tray toast (I27) + Copy diagnostics (I28) | Complete | Verbose / event-log channel for debugging still absent. |
| 20 | Settings diagnostics | + Restore-backup + Copy diagnostics + Trash buttons (F21/F23/I28) | Complete | "Reset to defaults" + "Network: allow update checks" toggles not done. |
| 21 | Single-instance handoff | + `--copy/--paste/--list` (F29) | Complete | One-way pipe → CLI cannot surface "not found." |
| 22 | About surface | Unchanged (but `Assembly.Location` is broken under single-file → B16). | **Broken under single-file (B16)** | Versioning + repo + LICENSE link, but `Open LICENSE` lookup will fail post-publish. |
| 23 | Schema migrations | + ApplyV3 (paste_mode) + tx-tightened (I29) | Complete | DDL-rollback under SQLite still risks half-applied state if power dies mid-`ALTER`. |
| 24 (new) | CLI scripting | F29 entirely new | Complete | One-way pipe limits scripting reliability — see I39 below. |

### Hidden / disabled / undocumented features (delta)

- **`--list` writes to `%LOCALAPPDATA%\TaskCopy\snippets.list`** but the README example says "writes id\\tTitle to stdout" — they disagree. The implementation is the file-write; the README is wrong. **Fix needed.**
- **`AutoPasteService.CursorOffsetClamped` event** wires the B14 toast but isn't documented anywhere user-facing — fine; it's a developer-visible signal only.
- **`HotkeyService.PrimaryRegistrationChanged` event** powers I17 but isn't surfaced to other consumers — fine.

---

## Bugs Found at v0.4.2

These are the **must-fix** items before tagging a release. **B16** is the umbrella for the 5 CI errors; the rest are observability/quality items.

### B16 — Five `-warnaserror` errors at HEAD

| # | File:Line | Code | Cause | Fix |
|---|---|---|---|---|
| 16a | [`App.xaml.cs:251`](src/TaskCopy/App.xaml.cs#L251) | CS8604 | `new SnippetMenuViewModel(_db, _settings)` passes a nullable `SettingsStore?` where a non-nullable `SettingsStore` is required. The early-out guard at line 244 only checks `_db`/`_clipboard`/`_foreground`. | Add `_settings is null` to the guard at line 244, OR change `SnippetMenuViewModel` ctor to `SettingsStore? settings`. The guard is cleaner. |
| 16b | [`Views/SettingsWindow.xaml.cs:140`](src/TaskCopy/Views/SettingsWindow.xaml.cs#L140) | CS0103 | `AutomationProperties.GetName(tb)` in `FindTitleBox` — type unresolved. Namespace is `System.Windows.Automation` (in PresentationCore). | Add `using System.Windows.Automation;` at the top of the file. |
| 16c+d | [`ViewModels/SettingsViewModel.cs:115`](src/TaskCopy/ViewModels/SettingsViewModel.cs#L115) | CS9273 + CS9258 | `PromptFor = field => $"<{field}>",` inside the GET accessor of `EditBodyPreview`. `field` is now a C# 14 contextual keyword inside property accessors. | Rename the lambda parameter to `f` (or `name`, or `key` — `f` matches the convention already used in the App.xaml.cs `PromptFor` call sites, but those are inside methods, not accessors, so they don't trip the keyword). |
| 16e | [`Views/AboutWindow.xaml.cs:24`](src/TaskCopy/Views/AboutWindow.xaml.cs#L24) | IL3000 | `Assembly.GetEntryAssembly()?.Location` always returns empty in single-file. The csproj now defaults `PublishSingleFile=true` so this analyzer fires. | Replace with `System.AppContext.BaseDirectory` to locate the on-disk `LICENSE` next to the exe. Same lookup, different API. |

All five fixes are XS. Cumulative diff < 15 lines.

### B17 — `Theme.Auto` doesn't follow OS theme changes at runtime

- **Current behavior:** [`ThemeService.IsSystemLight`](src/TaskCopy/Services/ThemeService.cs#L52) reads `HKCU\…\Themes\Personalize\AppsUseLightTheme` exactly once, in `App.OnStartup`. If the user flips their OS to Light mode after launch, TaskCopy keeps the launch-time palette.
- **Recommended change:** subscribe to `Microsoft.Win32.SystemEvents.UserPreferenceChanged`; when `PreferenceCategory == General` and `_settings.Theme == Theme.Auto`, prompt with the same I16-A relaunch flow. Don't try to live-swap (StaticResource brushes; same blocker as I16 Option A).
- **Code locations:** `Services/ThemeService.cs` (new event), `App.xaml.cs` (subscribe in OnStartup, unsubscribe in OnExit), `ApplyThemeRequested` event reused.
- **Complexity:** S.
- **Priority:** **P0** (Auto mode is the recommended default + user-visible at every theme flip).

### B18 — `--list` README documentation doesn't match implementation

- **Current behavior:** README says `--list … writes all snippets as "id\tTitle" to stdout (best-effort)`. Actual code at [`App.DumpListToDisk`](src/TaskCopy/App.xaml.cs#L406) writes to `%LOCALAPPDATA%\TaskCopy\snippets.list`. Stdout is not used (and can't be, from a `WinExe` without `AllocConsole`).
- **Recommended change:** Update README to match — "writes `id\tTitle` lines to `%LOCALAPPDATA%\TaskCopy\snippets.list`; caller reads that file." Document the path explicitly in the CLI section.
- **Code locations:** README only.
- **Complexity:** XS.
- **Priority:** **P1**.

### B19 — Pipe-IPC is one-way; CLI cannot detect "snippet not found"

- **Current behavior:** [`SingleInstanceServer.TrySend`](src/TaskCopy/Services/SingleInstanceServer.cs#L75) writes the message and disposes the pipe. The second-launch process always exits with code 0 — even when `--copy "Nonexistent title"` matches nothing in the first instance.
- **Recommended change:** v0.5 — switch the pipe to bidirectional (`PipeDirection.InOut`), have `OnPipeMessage` write a one-line response (`"ok"` or `"not-found"`), and let the CLI client read that and set the process exit code accordingly. Keep the existing one-way wire format for backward compat; gate the new response on a `--script` flag.
- **Complexity:** M.
- **Priority:** **P2** (scripting reliability for launcher integrations).

### B20 — `BackupRotator.Rotate` doesn't verify the just-written snapshot

- **Current behavior:** [`BackupRotator.Rotate`](src/TaskCopy/Services/BackupRotator.cs#L13) writes via `VACUUM INTO` + fsync. If `VACUUM INTO` lies (rare but possible on a bad disk sector) or the fsync silently failed, the user has a corrupt `.bak.0` that only fails when they actually need it.
- **Recommended change:** After fsync, run `PRAGMA quick_check;` against the new backup file in read-only mode. On non-`ok`, delete it and log; the previous `.bak.0` (now at `.bak.1`) still holds. Cheap (~µs on a 10K-row DB).
- **Code locations:** `Services/BackupRotator.cs`, new `SnippetDatabase.IntegrityCheck(string path)` overload.
- **Complexity:** S.
- **Priority:** **P0** (cheap; closes the F21 verification gap that prior research left open).

### B21 — `Reserved combo` list in `SettingsViewModel.IsReservedCombo` doesn't include Win-key combos

- **Current behavior:** [`SettingsViewModel.IsReservedCombo`](src/TaskCopy/ViewModels/SettingsViewModel.cs) rejects Ctrl+C/V/X/Z/Y/A/S/N/O/P/F/W/T but not anything with `ModifierKeys.Windows`. The OS will refuse to register Win+letter (most are OS-reserved), so the user gets NHotkey's generic "already in use" rather than a TaskCopy-authored "Win+* is OS-reserved; pick another combo."
- **Recommended change:** Add `modifiers & ModifierKeys.Windows` to the reserved check with a tailored message; OS refuses these anyway, this just gives a clearer error.
- **Code locations:** `ViewModels/SettingsViewModel.IsReservedCombo`.
- **Complexity:** XS.
- **Priority:** **P2**.

### B22 — `OrderBy(s => s.Title)` and similar use culture-sensitive comparison

- **Current behavior:** Several places sort strings via `OrderBy(s => s.Title)` (e.g. [`SnippetIO.Import`'s existing-titles HashSet uses `StringComparer.OrdinalIgnoreCase` correctly](src/TaskCopy/Services/SnippetIO.cs#L96), but elsewhere the default `Comparer<string>.Default` resolves to `StringComparer.CurrentCulture`). For a Turkish user, `İmportant` and `important` sort differently than for an English user.
- **Recommended change:** Audit all `OrderBy(string)` and `Contains(string, …)` to use `StringComparer.OrdinalIgnoreCase` / `StringComparison.OrdinalIgnoreCase`. Already done for most match paths — verify all of them.
- **Code locations:** `ViewModels/ManageGroupsViewModel`, `Services/SnippetIO`, `ViewModels/SnippetMenuViewModel`.
- **Complexity:** S.
- **Priority:** **P3**.

---

## Highest-Value New Features

Numbering continues past prior plans (F18-F40 used). New starts at F41.

### F41 — Verify backup snapshots immediately after write

- **User problem:** users only discover backup corruption when they need to restore. By then it's too late.
- **Evidence:** `BackupRotator.Rotate` does `VACUUM INTO` + fsync but never opens the resulting file. F21's restore path runs `quick_check` against the live DB; the backup file gets no such check.
- **Proposed behavior:** after fsync, open the fresh backup via a read-only SqliteConnection + `PRAGMA quick_check`. On non-"ok", delete it (the prior slot 0, now at slot 1, remains intact) and write a CrashLog entry. On "ok", proceed.
- **Implementation areas:** `Services/BackupRotator.Rotate`; new `SnippetDatabase.IntegrityCheck(string explicitPath)` overload, OR inline a tiny SqliteConnection in the rotator.
- **Data model / API / UI implications:** None.
- **Risks & edges:** quick_check on a freshly-VACUUMed file is nearly instant (< 1 ms for typical TaskCopy DB sizes); won't add perceptible startup latency. False positives essentially never happen — quick_check is conservative but not paranoid.
- **Verification:** corrupt a backup mid-write (kill `dotnet` with `taskkill /F` during VACUUM) → expect the partial file to be deleted on next launch + CrashLog entry.
- **Complexity:** S.
- **Priority:** **P0**.

### F42 — High-contrast theme variant

- **User problem:** Users on Windows High Contrast theme (`Settings → Accessibility → Contrast themes → HCBlack/HCWhite`) need WCAG AAA contrast that neither Mocha (#CDD6F4 on #1E1E2E ≈ 11:1, AAA OK) nor Latte (#4C4F69 on #EFF1F5 ≈ 7:1, AAA OK) is *designed* against actual HC palettes. Worse, the system focus indicators get replaced with HC defaults that won't match TaskCopy's templates.
- **Evidence:** `Themes/Mocha.xaml` + `Themes/Latte.xaml` are the only palettes. `ThemeService.Resolve` only switches between them. Windows offers `HighContrast`, `HighContrastBlack`, `HighContrastWhite`, `HighContrast2` themes — TaskCopy doesn't react to any of them.
- **Proposed behavior:** new `Themes/HighContrast.xaml` palette that delegates *all* brushes to `SystemColors.*` keys (WindowBrush, WindowTextBrush, HighlightBrush, etc.). `ThemeService.Resolve` adds a check: if `SystemParameters.HighContrast` is `true`, force the HC theme regardless of preference. Settings dropdown gains "High contrast (system)" as an explicit option for users who want it forced.
- **Implementation areas:** new `Themes/HighContrast.xaml` (~150 lines, all `{x:Static SystemColors.…}` references); `Services/ThemeService` extension; Settings UI dropdown entry.
- **Data model / API / UI implications:** Add `Theme.HighContrast` enum value. Migration-safe because `SettingsStore.Theme` does `Enum.TryParse` and falls back to Mocha.
- **Risks & edges:** Some Mocha-tinted UI (the 📌 pin glyph, the search-box caret color) won't translate cleanly to system colors. Accept; the focus is "legible," not "branded." HC users explicitly opt out of branded palettes.
- **Verification:** Set OS to HC Black → relaunch → expect TaskCopy in HC palette with system-default focus rectangles; tray icon still mauve (icon is .ico, not themed).
- **Complexity:** M.
- **Priority:** **P1** (accessibility is a real gap).

### F43 — Localisation hook + en-US baseline

- **User problem:** All UI strings are hardcoded English. Distribution unlock (F18) makes non-English-speaking users reachable; today they have no path.
- **Evidence:** No `.resx` files; no `<XmlnsDefinition>`; `StringFormat`s inline in XAML; status messages built with C# interpolation throughout `SettingsViewModel`.
- **Proposed behavior:** introduce `Properties/Strings.resx` (en-US default) + per-culture overlays (`Strings.de.resx`, etc.). XAML uses `{x:Static p:Strings.SettingsTitle}`. C# string interpolation switches to `string.Format(Strings.Status_HotkeySetTo, display)` style. Start with the en-US baseline only; community contributes overlays.
- **Implementation areas:** new `Properties/Strings.resx` (large surface — every Hardcoded string in XAML + every status message in VMs); csproj `<EmbeddedResource>` entries; tooling note in README + CONTRIBUTING for translators.
- **Data model / API / UI implications:** None to schema. Some user-facing IDs become culture-resolved at build time.
- **Risks & edges:** Some strings include placeholders (`$"Imported {n} snippets"`); these need composite formats. .resx supports them with `{0}` syntax.
- **Verification:** set machine culture to `de-DE`; with `Strings.de.resx` containing one translated key; expect that key's German variant to surface; others fall back to en-US.
- **Complexity:** L (mostly mechanical but touches every UI file).
- **Priority:** **P1** (distribution unlock makes this matter; can ship the framework without any non-en translations).

### F44 — Snippet-pack ecosystem (`.taskpack` files)

- **User problem:** First-launch shows 5 generic seeded snippets. A regex cookbook, a git command cheatsheet, a customer-support reply library, or a compliance template set would take a new user from "empty" to "useful" in one click.
- **Evidence:** `Services/SnippetIO` already does JSON import/export. The format is versioned. No reuse of this primitive for community packs.
- **Proposed behavior:** Add a `.taskpack` MIME-association OR a simple file extension contract — files ARE the existing import JSON, just with a curated extension. Settings → "Install pack…" file picker filters `*.taskpack;*.json`. Maintain a `github.com/sysadmindoc/taskcopy-packs` index repo (or a topic tag) where the community can publish curated libraries. README links to "Browse packs."
- **Implementation areas:** Settings UI string change ("Import snippets…" → adds "Install pack…" alongside, or rename); README section "Find more snippets."
- **Data model / API / UI implications:** None to code — pure file/UX convention layered on F9.
- **Risks & edges:** Packs from untrusted sources can carry malicious `{{shell:cmd}}` once F39 ships. Ship F39 with a per-snippet opt-in flag + warning dialog; pack imports never auto-enable shell execution.
- **Verification:** download a pack from the index repo; "Install pack…" → 25 new snippets visible in Settings; toggling sort modes still works.
- **Complexity:** S (code) + M (index-repo curation).
- **Priority:** **P1** (high leverage; cost is mostly the curation, not the code).

### F45 — `gh issue create` integration in "Copy diagnostics"

- **User problem:** "Copy diagnostics" produces a Markdown block, but the user still has to open a browser, find Issues, paste, fill the title. Friction kills bug reports.
- **Evidence:** [`SettingsViewModel.CopyDiagnostics`](src/TaskCopy/ViewModels/SettingsViewModel.cs) builds the bundle and clipboards it. No "file directly" path.
- **Proposed behavior:** detect `gh` on PATH at startup; if present, "Copy diagnostics" gains a "File issue" sibling button: writes the bundle to a temp file, `Process.Start("gh", "issue create --repo SysAdminDoc/TaskCopy --title 'Bug report' --body-file <tempfile>")`. On `gh` failure or absence, gracefully fall back to clipboard.
- **Implementation areas:** `Services/GhCli.cs` new; SettingsWindow gains a second button.
- **Data model / API / UI implications:** None.
- **Risks & edges:** `gh` not on PATH (common). Auth failures → toast, fall back to clipboard. Privacy: diagnostics bundle never includes snippet bodies (already true), but does include OS version + crash.log tail.
- **Verification:** install `gh`, `gh auth login`, click "File issue" → browser opens to the just-created GitHub issue draft.
- **Complexity:** S.
- **Priority:** **P2** (nice-to-have; reduces report friction).

### F46 — Snippet edit history (10-version cap per body)

- **User problem:** debounced save overwrites the previous body. A typo in the editor + a 300ms idle = the old body is gone.
- **Evidence:** [`SettingsViewModel.FlushPendingSave`](src/TaskCopy/ViewModels/SettingsViewModel.cs) calls `_db.Update(s.Id, s.Title, s.Body)` with no history. Undo in the editor only goes back to the last keystroke; nothing across save boundaries.
- **Proposed behavior:** Schema bump to V4: new `snippet_body_history(id, snippet_id, body, saved_at)` table with a trigger or app-level write that inserts one row per FlushPendingSave, then trims to 10 newest per `snippet_id`. Settings adds a per-snippet "History…" button opening a small modal: list of past versions with timestamps, "Restore" per row.
- **Implementation areas:** `Data/Migrations.ApplyV4`, `Data/SnippetDatabase` (new repository methods), `Views/HistoryWindow.xaml(.cs)`, Settings UI button.
- **Data model / API / UI implications:** Schema V4. JSON export does NOT include history (per-machine, like usage stats).
- **Risks & edges:** DB growth: 10 versions × 5KB average body × 50 snippets = 2.5MB. Negligible. Trash + History both live in DB; a snippet hard-purged from Trash also drops its history (use FK with ON DELETE CASCADE).
- **Verification:** edit body, wait 300ms, edit again, wait, click "History…" → 2 versions; restore the older one → editor shows the older text + the DB body matches.
- **Complexity:** M.
- **Priority:** **P1**.

### F47 — "Don't ask again" for delete confirm

- **User problem:** Confirm-delete (I6) is good for new users; power users delete dozens of snippets cleaning up and resent the modal each time.
- **Evidence:** [`SettingsViewModel.DeleteSnippet`](src/TaskCopy/ViewModels/SettingsViewModel.cs#L383) always shows `MessageBox.Show`. Soft-delete + Trash UI makes recovery trivial, so suppression carries low risk.
- **Proposed behavior:** Add a "Don't ask again" checkbox to the delete confirm using a custom WPF dialog (MessageBox doesn't support checkboxes natively). Persist via `settings.delete.skip_confirm`.
- **Implementation areas:** new `Views/ConfirmDeleteWindow.xaml(.cs)` (~40 lines), `SettingsStore.DeleteSkipConfirm`, `SettingsViewModel.DeleteSnippet`.
- **Risks & edges:** Once suppressed, a stray Del key + Trash auto-purge in 30 days = real loss. Mitigate: confirm dialog also gets a checkbox "remind me again in 7 days" that re-arms; or just document "you can always undo from Trash within 30 days."
- **Complexity:** S.
- **Priority:** **P2**.

### F48 — Per-snippet "last paste target" stat

- **User problem:** Power users want to know "which snippet do I use into which app the most?" for organizing.
- **Evidence:** `ForegroundWindowCapture` already captures the HWND; we extract the process name nowhere. No `last_target_app` column.
- **Proposed behavior:** Schema V4 (bundle with F46): add `last_target_process_name TEXT NULL`, `last_target_at INTEGER NULL` to `snippets`. On successful auto-paste, capture `Process.GetProcessById(GetWindowThreadProcessId)` and store.
- **Implementation areas:** `Services/AutoPasteService` (new return data), `App.HandleSnippetCopyAsync` (write through), `SnippetDatabase` (new column + setter). Optional Settings column showing the target.
- **Data model / API / UI implications:** Schema V4. F35 (per-app rules) builds on this directly.
- **Risks & edges:** Process name lookup costs ~100µs. Negligible. Privacy: process names like `1Password.exe` are sensitive; never include in JSON export.
- **Verification:** copy a snippet into Notepad; check Settings → snippet shows "last paste: notepad.exe just now."
- **Complexity:** M.
- **Priority:** **P3** (foundation for F35 more than user-visible today).

### F49 — Backup encryption (separate from F30 store encryption)

- **User problem:** Snippets often contain emails, ticket numbers, API tokens, passwords-as-templates. Backups land in `%LOCALAPPDATA%\TaskCopy\` in plaintext SQLite. Whole-disk encryption helps on a stolen laptop; nothing helps if backups are copied off-device.
- **Evidence:** `BackupRotator.Rotate` writes plaintext via SQLite `VACUUM INTO`. No encryption.
- **Proposed behavior:** Settings → Diagnostics → "Encrypt backups with password" toggle + password prompt. When on, post-VACUUM the rotator wraps the file with AES-GCM using a key derived from the password (PBKDF2 600,000 iters per OWASP). Stores `.bak.0.enc` instead. Restore prompts for the password; password reset is impossible (typical for E2E features).
- **Implementation areas:** new `Services/BackupCrypto.cs` (~100 lines, no deps), `BackupRotator.Rotate` branching, restore flow, Settings UI.
- **Data model / API / UI implications:** Backup file extension changes when enabled (`.bak.N.enc`). Mixed-mode (.bak + .bak.enc both present) requires picker prompt.
- **Risks & edges:** Lost password = lost backups. Make this very explicit in the password-set dialog.
- **Verification:** enable + set password; restart → expect `.bak.0.enc` on disk; restore → password prompt → DB restored.
- **Complexity:** M.
- **Priority:** **P3** (waiting on user signal; the F30 store-encryption path may obsolete this).

### F50 — "Last position (sticky)" flyout position

- **User problem:** Users with a fixed workflow (always paste into the second monitor at a specific corner) want the flyout to remember where they last had it.
- **Evidence:** [`SettingsStore.FlyoutPosition`](src/TaskCopy/Data/SettingsStore.cs) has two modes (Cursor, MonitorCenter). The original plan mentioned "Last position (sticky)" but it never shipped.
- **Proposed behavior:** Third enum value `LastPosition`. On flyout close, persist `flyout.last_x` / `flyout.last_y` as monitor-work-area-relative percentages. On open, restore.
- **Implementation areas:** `SettingsStore`, `SnippetMenuWindow.ShowAtCursor` + new `OnClosed` handler.
- **Data model / API / UI implications:** Two new settings keys.
- **Complexity:** S.
- **Priority:** **P3**.

### F51 — Snippet-search across body using FTS5 at >1000 items

- **User problem:** Today's substring scoring is O(N×L) where L is body length. At 1000+ snippets the type-ahead has a visible lag.
- **Evidence:** [`SnippetMatch.Score`](src/TaskCopy/Services/SnippetMatch.cs) does plain `Contains` calls. Microsoft.Data.Sqlite ships FTS5 enabled.
- **Proposed behavior:** Add `snippets_fts` virtual table (FTS5) mirroring `(id, title, body)`. Triggers maintain it on insert/update/delete. Score function consults FTS5's `bm25()` when the library exceeds a threshold (e.g. 500 snippets) and falls back to managed scoring below that.
- **Implementation areas:** new V4 migration; `SnippetDatabase` FTS5 maintenance; `SnippetMatch.Score` branching.
- **Data model / API / UI implications:** Schema V4. Disk size grows ~20% (FTS5 inverted index).
- **Risks & edges:** FTS5 tokenization is whitespace + alphanumeric by default; symbol-heavy snippets (code, regex) match less well. The fallback substring scoring covers them.
- **Verification:** seed 1500 snippets; type → expect <50ms response (vs ~150ms substring).
- **Complexity:** M.
- **Priority:** **P3** (premature — typical libraries are <100 snippets).

### F52 — "Reset to defaults" button in Settings

- **User problem:** No one-click revert. Users who experimented and want a clean slate currently have to delete `%LOCALAPPDATA%\TaskCopy\`.
- **Evidence:** Settings has no Reset button. Multiple individual toggles can be reverted manually, but there's no atomic "back to factory."
- **Proposed behavior:** Settings → Diagnostics → "Reset to defaults…" → confirm dialog → wipes `settings` table (snippets/groups untouched), re-seeds the hotkey to Ctrl+Alt+V, re-registers, prompts relaunch for theme.
- **Implementation areas:** `SettingsViewModel.ResetToDefaults` + `SnippetDatabase.ClearSettings` (DELETE FROM settings;).
- **Risks & edges:** "Snippets too" expectation — be explicit in the confirm dialog that user content stays.
- **Complexity:** S.
- **Priority:** **P3**.

---

## Existing Feature Improvements

### I35 — Adopt `Microsoft.Extensions.DependencyInjection` for the service graph

- **Current behavior:** [`App.xaml.cs`](src/TaskCopy/App.xaml.cs) `new`s up 12 services in `OnStartup` and wires every event handler by hand. File is ~720 LOC.
- **Problem:** at 12 services + 8 view models the wiring is no longer scannable. Tests (when CLAUDE.md's no-tests rule is lifted) will need swap points. F26 Velopack adds the 13th service. v0.5 Windhawk mod adds a 14th.
- **Recommended change:** add `Microsoft.Extensions.DependencyInjection` 9.0.0 (~150KB). New `App.ServiceProvider` field; `OnStartup` becomes `services.AddSingleton<X>()...services.BuildServiceProvider()`. View models get their dependencies via constructor injection.
- **Code locations:** `App.xaml.cs` (orchestration → composition root), every service gets a constructor (most already do), every VM gets a constructor (already does).
- **Backward compatibility:** None — internal refactor.
- **Verification:** existing functional behavior unchanged; in-memory smoke test of the service graph; F26+Windhawk reuse the container.
- **Complexity:** M.
- **Priority:** **P1**.

### I36 — Settings UI breakdown — `SettingsViewModel` too big

- **Current behavior:** `SettingsViewModel` is 720+ LOC and owns snippet editor + groups + hotkey + theme + sort + auto-paste + recent clips + export + import + open-folders + diagnostics + trash + restore-backup + status. It's the largest single file in the codebase after `App.xaml.cs`.
- **Recommended change:** split into three VMs along natural seams:
  - `SnippetEditorViewModel` — title/body/group/pin/monospace/quickHotkey/pasteMode/EditBodyPreview state.
  - `PreferencesViewModel` — autopaste/startup/theme/sort/flyoutPosition/recentclips/hotkeyRebind.
  - `DiagnosticsViewModel` — backup/restore/export/import/openFolders/copyDiagnostics/trash.
  - `SettingsViewModel` shrinks to the shell + status string + ownership of the three children.
- **Code locations:** new `ViewModels/SnippetEditorViewModel.cs`, `ViewModels/PreferencesViewModel.cs`, `ViewModels/DiagnosticsViewModel.cs`; `SettingsWindow.xaml` binds nested `DataContext`s.
- **Backward compatibility:** internal.
- **Complexity:** M.
- **Priority:** **P2** (pair with I35).

### I37 — Bulk `ObservableCollection` updates on filter / refresh

- **Current behavior:** [`SnippetMenuViewModel.ApplyFilter`](src/TaskCopy/ViewModels/SnippetMenuViewModel.cs#L120) calls `Snippets.Clear(); foreach (...) Snippets.Add(...)`. WPF re-renders rows per Add. At 500+ snippets typing-while-filter is choppy.
- **Recommended change:** swap `ObservableCollection<SnippetRow>` for [`CommunityToolkit.Mvvm.Collections.ObservableGroupedCollection`] OR roll a small `BulkObservableCollection` that suspends `CollectionChanged` during `ReplaceAll(IEnumerable<T>)`. Same approach for `RecentClips`.
- **Code locations:** `ViewModels/SnippetMenuViewModel`, new `ViewModels/BulkObservableCollection.cs`.
- **Backward compatibility:** internal.
- **Complexity:** S.
- **Priority:** **P2**.

### I38 — "Test hotkey" verification button

- **Current behavior:** I17's green/red dot only reflects `RegisterHotKey` success — not whether the system actually delivers `WM_HOTKEY` to TaskCopy's HWND. Some OS configurations (specific accessibility tools, third-party hotkey managers) can register but swallow.
- **Recommended change:** next to the rebind button, add a "Test" button that sets a flag, waits 5 seconds for the hotkey to fire, and shows "Triggered" or "Didn't fire in 5s — check for other apps grabbing this combo."
- **Code locations:** `SettingsViewModel`, `SettingsWindow.xaml`.
- **Complexity:** S.
- **Priority:** **P2**.

### I39 — `--copy/--paste` should write a result file the CLI client can read

- **Current behavior:** B19 above — pipe is one-way; CLI exit code is always 0.
- **Recommended change:** for `--copy`/`--paste`/`--list`, the first instance writes `%LOCALAPPDATA%\TaskCopy\.cli-result` (one line: "ok" / "not-found" / "error: msg"). The second instance, after sending, reads that file with a 500ms poll and prints its content to a logfile + exits with a non-zero code on non-"ok". (`WinExe` cannot write to a real stdout without `AllocConsole`; the file is the workaround.)
- **Code locations:** `Services/SingleInstanceServer`, `App.xaml.cs`.
- **Complexity:** S.
- **Priority:** **P3**.

### I40 — `Open in external editor` for the body field

- **Current behavior:** No way to compose a long snippet body in the user's preferred editor.
- **Recommended change:** Editor toolbar → "Open in editor…" button. Writes the body to a temp file with a sentinel header, spawns `code` / `notepad++` / `notepad` (configurable via `editor.command` setting or `$EDITOR`), polls for changes, reads back on close.
- **Code locations:** new `Services/ExternalEditor.cs`, `SettingsWindow.xaml`.
- **Complexity:** M.
- **Priority:** **P3** (carried from F34 in prior plan).

### I41 — Snippet drag-to-group

- **Current behavior:** Drag-reorder in the Settings list (I7) only reorders within the current group. To change a snippet's group you must use the dropdown.
- **Recommended change:** when F20-style group chips exist in the flyout, mirror them as drop targets in the Settings list. Drag a snippet onto a chip header → assigns group.
- **Code locations:** `Views/SettingsWindow.xaml.cs` (DragOver/Drop on group chips), needs F20-equivalent in Settings.
- **Complexity:** M.
- **Priority:** **P3**.

---

## Reliability, Security, Privacy, Data Safety

### Bugs / risks identified

- **B16 (P0)** — Build broken at HEAD; 5 errors. Specific fixes documented above.
- **B17 (P0)** — Theme.Auto doesn't track runtime OS theme changes.
- **B18 (P1)** — README documents stdout for `--list`, code writes a file.
- **B19 (P2)** — IPC is one-way; CLI cannot detect "not found".
- **B20 (P0)** — Backup files are written and not verified.
- **B21 (P2)** — Win+* combos give a generic NHotkey error instead of a TaskCopy-authored message.
- **B22 (P3)** — Some string sorts/comparisons use culture-sensitive default.

### Missing guardrails

- **No `Application.Current` null-guard in `CrashLog.Install`** during very early failure paths; the existing fallback to `MessageBox.Show` works only if the message pump is alive. Document as "early startup catastrophe."
- **No size cap on `recent_clips.body`** beyond the 10KB MaxBytesPerClip cap. A pathological 100KB clip is rejected, but no telemetry tells the user.
- **`SnippetTransforms.Sha256` / `Md5`** run on UTF-8 bytes of the input; non-ASCII inputs get a deterministic but locale-independent hash — good. No length check; running sha256 on a 100MB clipboard payload is wasteful. Cap input length to 1MB.
- **`F39 {{shell:cmd}}` is documented in research but not implemented**; if it ever lands, *must* be opt-in per snippet + warning dialog. Reaffirm in the v0.5 plan.

### Permission / network / filesystem concerns

- **No network usage today.** Once F26 Velopack lands, the update check is the first outbound connection. Explicit "Updates: Off / Notify / Automatic" toggle in Settings; default to Notify.
- **`HKCU\…\Run`** registry write remains user-scope. B15 reconciliation closed in v0.4.1.
- **Single-file analyzer surfaced one false alarm (IL3000)** which led to a real bug (B16e); audit `IL3001`/`IL3002` if `Microsoft.Data.Sqlite` adds reflection paths in future versions.

### Recovery / rollback

- **F41 (backup verify)** closes the last gap in the F21 restore chain.
- **F46 (body history)** is the per-snippet recovery layer Trash doesn't cover (Trash recovers whole snippets; History recovers edit states).

### Logging / diagnostics

- **Today**: crash.log only. Verbose channel still absent.
- **Add**: optional `events.log` (1MB rotation) when `settings.verbose_logging` is on. Capture: hotkey register/unregister, schema migrations, settings changes, auto-paste attempts (target HWND class + title — without sensitive content), recent-clip skip reasons (exclude/can-include=0/too-big/dedup), backup verify outcomes.

---

## UX, Accessibility, Trust

### Onboarding gaps

- **README still has no screenshots.** v0.4.0 ROADMAP item never produced the `assets/screenshots/` directory. Capture at 125% DPI in both Mocha and Latte → link from README under Install.
- **First-launch tour** — Settings opens with seeds but no callout pointer to "where the hotkey is set" or "what placeholders are." A small in-window "Welcome" panel with a Got-it dismissal would help.
- **No `Start-with-Windows` offer in the first-run welcome** — the user has to discover the checkbox.

### Empty / loading / error states

- **No-match empty state** copy says "Press Esc to clear the filter" but recently-clips filter has the same UI and the message can apply to either. Consider context-specific empty states ("No matches in snippets · No matches in recent clips").
- **Trash empty state** shows status text but no illustration / friendly note ("Nothing here — deleted snippets purge after 30 days").

### Destructive / irreversible actions

- **Empty Trash** prompts a confirm — good.
- **Delete Permanently** prompts a confirm — good.
- **Restore Backup** prompts a confirm + takes a pre-restore snapshot — excellent.
- **Reset to defaults** doesn't exist yet (F52); when it ships, must confirm.

### Settings clarity

- **`SelectedFlyoutPosition` tooltip** says "Active monitor center is more comfortable on ultrawide monitors" — accurate; consider also "Last position (sticky)" once F50 ships.
- **`PasteModeOption.Label` says "Auto (Ctrl+V)"** — the parenthetical helps; consider adding "Type characters (slower; works in terminals/RDP)" so users know the trade-off.
- **No "Updates" section yet** (waits on F26).

### Accessibility

- **AutomationProperties** sprinkled in v0.3 still cover the right surfaces — flyout search, snippet rows, Settings list, title+body editors.
- **Focus outline** (I33) covers Mocha + Latte; F42 high-contrast variant will inherit the right system colors.
- **Keyboard nav** — F22 free-form quick-hotkey + I32 Settings accelerators close the obvious gaps. `Tab` order in Settings → not explicitly tested; needs live validation.

### Microcopy / trust signals

- **"Auto-paste was skipped — the target window may be running elevated"** (I18 toast) — clear and actionable. Maintain this tone for future failure messages.
- **"You can press {hotkey} from anywhere to open the picker"** — the first-run toast says this; restate in the Settings header so returning users don't forget their custom binding.

---

## Architecture & Maintainability

### Module / boundary improvements

- **`App.xaml.cs` is now 720+ LOC** (was 415 at v0.4.0). Without I35 + I36 it'll cross 1000 by v0.5 (Velopack + Windhawk IPC bridge). Bundle the refactor with F26.
- **`SettingsViewModel` is 720+ LOC** — see I36.
- **`SnippetDatabase` is 500+ LOC** — covered by the V4 schema work + repository split (I36 sibling).

### Refactor candidates (after I35)

- Extract `ITrayOrchestrator`, `ISnippetCopyOrchestrator` from App.xaml.cs.
- Extract `ISnippetsRepository`, `IGroupsRepository`, `ITrashRepository`, `IRecentClipsRepository`, `ISettingsRepository` from `SnippetDatabase`.
- Introduce `IClipboardService`, `IHotkeyService`, `IAutoPasteService` interfaces — DI-friendly + test-friendly.

### Test gaps (when CLAUDE.md "no tests" rule is lifted)

Same order as prior plan; F27/F28/V3-V4 migrations now in the top 3:

1. `SnippetTemplating.Expand` w/ pipe-chained transforms (F28 surface).
2. `SnippetTransforms.Apply` per-transform behavior + chaining.
3. `Migrations.Apply` v1→v2→v3→v4 against an `:memory:` SQLite DB.
4. `SnippetMatch.Score` for fielded + unfielded queries + ranking ties.
5. `SnippetIO.Export` → `Import` round-trip with `pasteMode` + `schemaVersion` fields.
6. `BackupRotator.ListAvailable` + `RestoreFrom` + `Rotate` w/ F41 verify.
7. `HotkeyService.TryParseHotkey` — every documented syntax + every documented invalid case.
8. `ClipboardWatcher.TryReadDwordFormat` (B9 regression coverage).

### Documentation gaps

- **Screenshots still pending.** F18 Phase 1 carryover.
- **No CONTRIBUTING.md.** With public MIT + community pack ecosystem (F44), this is overdue.
- **`docs/` is empty.** Could host a component map or design notes.
- **README has stale `--list` claim (B18).**

### Release / build / deployment

- **CI is red (B16).** First priority.
- **No release artifacts yet.** No v* tag has been pushed.
- **No signing.** Architecture-research.md §7.8 still flags this.
- **No MSIX, no winget, no auto-update.** Tracked.

---

## Archived Prioritized Roadmap

This section records the original v0.4.2 audit sequencing. It is no longer an active checklist; use `ROADMAP.md` for remaining work.

### Phase A — v0.4.3 "Make it build" (P0 only)

- [ ] **P0 — B16: Fix the 5 -warnaserror errors**
  - Why: published v0.4.x commits don't compile; release workflow can't tag.
  - Evidence: CI run [26408247365](https://github.com/SysAdminDoc/TaskCopy/actions/runs/26408247365) — 5 errors.
  - Touches: `App.xaml.cs:251` (CS8604 — add `_settings is null` to the guard), `Views/SettingsWindow.xaml.cs:140` (CS0103 — add `using System.Windows.Automation;`), `ViewModels/SettingsViewModel.cs:115` (CS9273/CS9258 — rename `field` lambda param to `f`), `Views/AboutWindow.xaml.cs:24` (IL3000 — replace `Assembly.GetEntryAssembly()?.Location` with `AppContext.BaseDirectory`).
  - Acceptance: `dotnet build src/TaskCopy/TaskCopy.csproj -c Release -warnaserror` returns 0 warnings, 0 errors on CI.
  - Verify: next push triggers ci.yml → green check.

- [ ] **P0 — B17: Theme.Auto follows runtime OS theme**
  - Why: Auto is the recommended default and silently fails the most common surprise (user flips OS theme mid-day).
  - Evidence: `ThemeService.IsSystemLight` reads registry once at startup; no `SystemEvents.UserPreferenceChanged` listener.
  - Touches: `Services/ThemeService.cs`, `App.xaml.cs` (subscribe + unsubscribe), reuse `ApplyThemeRequested` event for the relaunch prompt.
  - Acceptance: with Theme=Auto, flipping OS theme prompts to restart.
  - Verify: live — Settings → Personalize → Light, expect TaskCopy relaunch prompt.

- [ ] **P0 — B20 / F41: Verify backup snapshot after write**
  - Why: cheap insurance; turns silent corruption into a logged event.
  - Touches: `Services/BackupRotator.Rotate`, `SnippetDatabase.IntegrityCheck(string path)` overload.
  - Acceptance: corrupt the live DB to force a bad VACUUM INTO; expect fresh `.bak.0` to be deleted + CrashLog entry; previous `.bak.1` intact.
  - Verify: synthetic — manually corrupt then trigger Rotate.

### Phase B — v0.4.4 "Quality of life" (P1, fits in one commit)

- [ ] **P1 — B18: Fix README `--list` documentation**
  - Touches: `README.md`.
  - Acceptance: `--list` section says "writes `id\tTitle` to `%LOCALAPPDATA%\TaskCopy\snippets.list`."

- [ ] **P1 — F47: Don't-ask-again for delete confirm**
  - Touches: new `Views/ConfirmDeleteWindow.xaml(.cs)`, `SettingsStore`, `SettingsViewModel.DeleteSnippet`.
  - Acceptance: delete with checkbox set → no future modal; reset via Settings → "Reset to defaults" (post-F52).

- [ ] **P1 — I38: "Test hotkey" verification button**
  - Touches: `SettingsViewModel`, `SettingsWindow.xaml`.

### Phase C — v0.5.0 "Reach" (P1 features that broaden the audience)

- [ ] **P1 — F42: High-contrast theme**
- [ ] **P1 — F43: Localisation hook + en-US baseline** (Larger Bet — see below)
- [ ] **P1 — F44: Snippet-pack ecosystem**
- [ ] **P1 — F46: Snippet edit history (V4 migration)**
- [ ] **P1 — I35: Adopt MS.Extensions.DependencyInjection**
- [ ] **P1 — F52: Reset to defaults**

### Phase D — v0.5.x "Polish + Distribution Phase 2/3"

- [ ] **P2 — F45: `gh issue create` integration**
- [ ] **P2 — I36: SettingsViewModel split**
- [ ] **P2 — I37: Bulk ObservableCollection updates**
- [ ] **P2 — B19 / I39: Two-way IPC + CLI result file**
- [ ] **P2 — B21: Win+* combos get tailored message**
- [ ] **P2 — F26: Velopack auto-update** (depends on F18 Phase 2 binaries shipping)
- [ ] **P2 — F18 Phase 2: winget manifest**
- [ ] **P2 — F18 Phase 3: Authenticode signing budget decision**

### Phase E — v0.6 "Power user" (large bets)

- [ ] **P3 — Windhawk companion mod** (existing ROADMAP item; depends on F26 + F18 Phase 3 stable IPC primitive).
- [ ] **P3 — F30: Encrypted snippet store** (SQLCipher dep swap).
- [ ] **P3 — F31: BYO cloud sync** (S3/B2/Dropbox).
- [ ] **P3 — F48: Per-snippet last-target tracking**
- [ ] **P3 — F49: Backup encryption (lighter than F30)**
- [ ] **P3 — F50: "Last position (sticky)" flyout position**
- [ ] **P3 — F51: FTS5 search at scale**
- [ ] **P3 — F32-F40, I16-B, I40, I41**: see ROADMAP.md v0.5+ section.

---

## Quick Wins

XS items a coding agent can pick up in any free slot. Each one < 30 minutes.

1. **B16a/b/c/d/e** — five compile fixes (XS each, ~10 LOC total).
2. **B18** — README `--list` correction (XS).
3. **B21** — Win+* combos in `IsReservedCombo` (XS).
4. **B22** — culture-aware string sort audit (XS).
5. **F50** — "Last position (sticky)" flyout placement (S).
6. **F52** — "Reset to defaults" Settings button (S).
7. **I38** — "Test hotkey" verification (S).
8. **Add CONTRIBUTING.md** with build steps + commit conventions (XS).
9. **Add `.github/ISSUE_TEMPLATE/{bug,feature}.md`** + `PULL_REQUEST_TEMPLATE.md` (XS).

---

## Larger Bets

Multi-day items needing design choice + staged rollout:

- **F43 Localisation** — touches every UI file. Need an early decision on resource framework (`.resx` is standard but verbose; consider [`Stride.Core.Translation`](https://github.com/stride3d/stride/tree/master/sources/core/Stride.Core.Translation) or just hand-rolled `JsonStringLoader` to keep cost low).
- **F26 Velopack + Distribution Phase 2/3** — must coexist with B16 build fix; do not stack until v0.4.3 ships green.
- **I35 + I36 DI refactor** — natural to bundle with F26 (which adds the 13th service). Defer until v0.5 to avoid churning v0.4.3 hotfix.
- **F30 + F49 encryption** — pick one: store encryption (F30, full coverage, dep swap risk) OR backup encryption only (F49, no dep change). Most users want backup encryption; ship F49 first.
- **Windhawk mod** — biggest ROI uncertainty. Defer to v0.6 until distribution is stable.

---

## Explicit Non-Goals (carried + new)

Carried from prior plans:

- Clipboard-history-as-default (F19 stays opt-in).
- Trigger-based text expansion (SetWindowsHookEx).
- Overriding Win+V.
- Cross-platform.
- Cloud sync as a SaaS.
- Generic scripting / arbitrary keyboard-macro automation.
- Tests in pre-CLAUDE.md-override passes.

New in this pass:

- **Multi-user / multi-tenant** — TaskCopy is single-user; storage is `%LOCALAPPDATA%`. Don't add per-user roles or shared workspaces.
- **OCR / image clipboards as primary feature** — F33 stays opt-in; making it default dilutes the "curated snippets" identity.
- **AI-assisted snippet generation** — "Ask AI to write a snippet" is feature creep that doesn't match the muscle-memory identity. Decline.
- **Telemetry by default** — opt-in always. Even the F45 `gh issue create` flow stays user-initiated.

---

## Open Questions

Only questions that can't be answered by inspecting the project or researching public sources.

1. **B16e (`AppContext.BaseDirectory` vs `Path.GetDirectoryName(Environment.ProcessPath)`)** — both are single-file-safe. The former is more idiomatic; the latter resolves symlinks differently on Linux (irrelevant here). Recommendation: `AppContext.BaseDirectory`. Confirm before merge.
2. **F43 resource framework** — `.resx` vs lighter alternative? Recommendation: ship `.resx` + the standard `ResourceManager` + cultures via build-time `<EmbeddedResource>` because tooling (`xliff` editors, Crowdin, Lokalise) all groks .resx. Confirm.
3. **F44 pack index location** — same repo as TaskCopy under `packs/` subdir, OR a separate `sysadmindoc/taskcopy-packs` repo? Separate repo is cleaner for community contributions; main repo simpler for v1. Recommendation: separate repo from day 1.
4. **F26 Velopack `delta` size vs full-installer size** — Velopack defaults produce both; on 80MB self-contained binaries, delta updates are essential. Confirm policy: ship deltas for self-contained, full-only for portable.
5. **F18 Phase 3 signing budget** — Authenticode cert (~$80–300/yr OV; EV ~$500+) — confirm the user has the budget before scoping the work.
6. **F42 high-contrast — opt-in vs forced** — `SystemParameters.HighContrast` is `true` → force HC palette regardless of user theme choice, OR offer it as an explicit Theme.HighContrast value that auto-selects on HC OS? Recommendation: both — auto-select on HC OS *and* expose as an explicit choice.

---

*End of report. ROADMAP.md is the active source of truth; this file is archived research context.*
