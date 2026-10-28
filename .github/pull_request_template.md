Closes #

## Summary

<!-- What changed and why, in a few sentences. Name the Feature(s) touched. -->

## Database changes

<!-- New or changed Entities and the migration name, or "None". Every generated migration is inspected before commit. -->

## Done checklist

<!-- From AGENTS.md. Tick each item or say why it does not apply. -->

- [ ] Tests added or updated; `dotnet test PersonalFinance.sln` and `npm run lint` pass.
- [ ] `AppVersion` bumped in `PersonalFinance/src/PersonalFinance.Shared/Constants/AppVersion.cs` (`Current`, `Major`, `Minor`, `Patch` together; patch = fix, minor = feature, major = breaking or schema migration).
- [ ] `CHANGELOG.md` has a `## [x.y.z] - YYYY-MM-DD` section at the top for that version, entries in the right category (or the PR is docs-only or labelled `no-release`).
- [ ] The change is focused: no unrelated refactoring, leftover TODOs or debug code.
- [ ] New or changed user-facing strings are keys in `Resources/i18n/en.json` only. The `Translate (i18n)` check drafts the Vietnamese and pushes it as `i18n: translate N keys (vi)` (or `npm run i18n:translate` ran locally). No hand-typed `vi.json` or `i18n.lock.json` entries. Reviewing the Vietnamese is optional.

## Evidence

<!-- The exact commands you ran and their results (test counts, lint output, E2E run). Say which checks were blocked and why. -->

## Outstanding issues

<!-- Anything left undone or follow-up tickets, or "None". -->
