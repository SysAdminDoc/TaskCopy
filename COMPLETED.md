# TaskCopy Completed Work

This file summarizes shipped work. Release-level details remain in
[`CHANGELOG.md`](CHANGELOG.md); the active backlog remains in
[`ROADMAP.md`](ROADMAP.md).

## Current Release

TaskCopy is at **v0.5.12** on `master`.

- The core Windows clipboard snippet workflow is shipped: tray icon, global
  hotkey, flyout search, keyboard navigation, direct snippet copy, and optional
  auto-paste back into the previously focused app.
- The snippet library is backed by SQLite with schema migrations, WAL mode,
  integrity checks, JSON export/import, `.taskpack` import, Espanso YAML import,
  trash, backup rotation, restore UI, and encrypted backup support.
- Power-user editing is shipped: groups, pinned snippets, usage-based sorting,
  recent clipboard capture, per-snippet hotkeys, paste modes, placeholders,
  transforms, forms, shell placeholders, image snippets, code editing, body
  history, and external-editor integration.
- Reliability and diagnostics are shipped: single-instance IPC, per-user pipe
  naming, CLI copy/paste/list commands, diagnostics bundle, crash log rotation,
  GitHub issue filing via `gh`, regression tests, and CI build/test coverage.
- Accessibility and UX polish are shipped: Mocha, Latte, and high-contrast
  styling; keyboard focus states; screen-reader labels; first-run seed snippets;
  refined Settings, flyout, dialogs, empty states, and accent handling.

## Shipped Milestones

- **v0.1.0:** minimal tray and global-hotkey snippet picker.
- **v0.2.0:** power-user core, persistence, and first Settings workflows.
- **v0.3.0:** snippet intelligence and placeholder expansion.
- **v0.4.0 - v0.4.6:** distribution foundation, backup restore, trash, CLI,
  diagnostics, sticky positioning, repo contribution hygiene, and release
  workflow polish.
- **v0.5.0 - v0.5.12:** edit history, usage stats, encrypted backups, per-app
  rules, multi-clip paste, image snippets, Espanso import, shell placeholders,
  syntax-highlighted editing, FTS5 search, regression tests, backup hardening,
  and premium UX polish.

## Verification History

Recent release entries in [`CHANGELOG.md`](CHANGELOG.md) record successful
Release build and test commands for the code-bearing milestones. This
consolidation is documentation-only.
