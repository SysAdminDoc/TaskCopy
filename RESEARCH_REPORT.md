# TaskCopy Research Report

This file is the current research synthesis. Historical audit details are
archived at
[`docs/archive/research/RESEARCH_FEATURE_PLAN_2026-05-25.md`](docs/archive/research/RESEARCH_FEATURE_PLAN_2026-05-25.md);
the original architecture research remains in
[`research/architecture-research.md`](research/architecture-research.md).

## Current Product Thesis

TaskCopy should remain a standalone tray + hotkey clipboard snippet manager for
Windows. That architecture gives users nearly the same workflow as the original
"right-click the taskbar" idea without injecting into Explorer, requiring
Windhawk, or depending on undocumented Windows taskbar internals.

The primary app is now mature enough that remaining work is mostly distribution,
trust, localization, sync, and deeper integration rather than core snippet
management.

## Architecture Decision

- **Primary path:** WPF tray icon plus global hotkey opens the TaskCopy flyout at
  the cursor. This is shippable, signable, testable, and compatible with normal
  Windows app distribution.
- **Companion path:** a Windhawk mod can be built later for users who
  specifically want a literal taskbar context-menu trigger. It should remain an
  optional power-user add-on that calls into the stable TaskCopy IPC / CLI path.
- **Rejected paths:** deskbands, generic shell context-menu extensions,
  overriding `Win+V`, and broad low-level keyboard/menu hooks are not viable
  primary product surfaces.

## Active Research Risks

- **Data trust:** encrypted backups are shipped, but an encrypted primary
  snippet store remains open. This likely needs a deliberate storage decision
  instead of bolting encryption onto the current SQLite access pattern.
- **Sync:** BYO cloud sync needs conflict rules, account-free configuration,
  backup compatibility, and clear failure states before implementation.
- **Distribution:** winget, Authenticode signing, Velopack updates, MSIX, and
  Microsoft Store packaging are still separate trust and maintenance projects.
- **Localization:** the UI needs a resource baseline and translator workflow
  before non-English releases.
- **Literal taskbar integration:** the Windhawk companion mod should depend on
  the stable app IPC and should not become a required install path.

## Next Research Inputs

- Compare SQLCipher or app-layer encryption tradeoffs for `F30`.
- Define sync conflict semantics for snippets, groups, images, trash, and body
  history before `F31`.
- Confirm the packaging path order: winget, signing, Velopack, MSIX, then Store.
- Refresh Windhawk hook details before starting the companion mod because Windows
  taskbar internals can drift quickly.
