# Contributing to TaskCopy

Thanks for your interest. This file collects the small set of conventions the project follows so contributions land cleanly.

## Build

```powershell
git clone https://github.com/SysAdminDoc/TaskCopy.git
cd TaskCopy
dotnet restore TaskCopy.sln
dotnet build TaskCopy.sln -c Release -warnaserror
dotnet test TaskCopy.sln -c Release --no-build
.\src\TaskCopy\bin\Release\net10.0-windows\TaskCopy.exe
```

Requirements:

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A clean Release solution build is the CI gate (`-warnaserror`), followed by the regression test suite. Run both locally before pushing.

## Commit style

- **No `Co-Authored-By:` trailers.** The project history is single-author by convention; matches existing log shape.
- **Imperative subject under ~70 chars.** "feat: add fuzzy search" / "fix: ClipboardWatcher misreads CanInclude format".
- **One logical change per commit.** Bundle related XAML+code+ROADMAP changes into one commit; split unrelated touch-ups.
- **Reference IDs.** Roadmap items use `F-`/`I-`/`B-` prefixes (see [`ROADMAP.md`](ROADMAP.md) + [`RESEARCH_FEATURE_PLAN.md`](RESEARCH_FEATURE_PLAN.md)). Cite the ID in the body so commits cross-link.

## Versioning + release docs

Every change that ships under a version bump must update three files in lockstep:

1. `src/TaskCopy/TaskCopy.csproj` — `<Version>` + `<AssemblyVersion>` + `<FileVersion>`.
2. `README.md` — the version badge link.
3. `CHANGELOG.md` — a new `## [x.y.z] — YYYY-MM-DD` entry with `### Added` / `### Changed` / `### Fixed` / `### Architecture` sections.
4. `ROADMAP.md` — check off the items being shipped under the appropriate version section.

## Code style

- Prefer named local helpers over deep ternaries.
- Comments explain *why*, not *what*. The existing files have a consistent voice — please match it.
- Keep tests focused on risky logic and regressions. The current suite covers database migrations/search, import validation, backup rotation, templating guardrails, settings bounds, and command parsing.
- WPF XAML stays minimal — no third-party MVVM frameworks beyond `CommunityToolkit.Mvvm`.

## Snippet packs

The community pack ecosystem (F44) lives at <https://github.com/SysAdminDoc/taskcopy-packs>. Packs are the same JSON format as F9 import/export with the `.taskpack` extension. Submit packs to that repo, not this one.

## Questions / bug reports

Use Settings → Diagnostics → "Copy diagnostics" (or "File issue" if you have the `gh` CLI installed) to attach version + schema + log tail to every issue. Saves a round-trip.
