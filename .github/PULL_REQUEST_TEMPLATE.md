## Summary

<!-- 1-3 bullet points describing the change. Reference the F-/I-/B- ID from
     ROADMAP.md / RESEARCH_FEATURE_PLAN.md if this closes a planned item. -->

## Changes

<!-- File-level summary of what was modified or added. -->

## Verification

- [ ] `dotnet build src\TaskCopy\TaskCopy.csproj -c Release -warnaserror` is clean
- [ ] Manual smoke test of the affected UI path
- [ ] CHANGELOG.md entry added under the next version section
- [ ] ROADMAP.md checkbox flipped if this closes a planned item
- [ ] Version bumped in csproj + README badge if shipping under a new release

## Notes

<!-- Anything reviewers should pay attention to: tricky edge case, schema
     migration, new dependency, breaking change to a public format. -->
