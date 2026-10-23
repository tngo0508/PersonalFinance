# 0006 – One GitHub Release per Merged PR, Tagged from AppVersion

**Date:** 2026-10-22  
**Status:** Accepted

## Context

Every PR already bumps `AppVersion` and adds a `CHANGELOG.md` entry, but GitHub Releases were made by hand. By 4.1.1 the
newest Release was still v2.0.1, and about 680 lines had piled up under `## [Unreleased]` for versions that were deployed
but never released.

## Decision

1. **Every merged PR is a release.** After the merge to `main` is deployed and the smoke tests pass, the `create-release`
   job in `deploy-azure.yml` creates the tag `v{AppVersion.Current}` on the merged commit and a GitHub Release. A Release
   therefore means "this version is live and smoke-tested", the same point at which `latest` images move.
2. **The version stays a manual, reviewed bump.** CI reads `AppVersion.Current`; it never computes or commits a version.
3. **`CHANGELOG.md` has no `[Unreleased]` section.** Each PR adds `## [x.y.z] - YYYY-MM-DD` for its own version, and that
   section becomes the Release notes, followed by GitHub's generated list of merged PRs. This deliberately departs from
   Keep a Changelog, which collects unreleased entries and cuts them at release time.
4. **The PR is the gate, not the deployment.** `release-check.yml` fails a PR unless the version is higher than `main`'s,
   matches its components, and has its CHANGELOG section. If the tag already exists at release time (no bump, or a
   re-run), `create-release` skips with a notice instead of failing a deployment that succeeded. Docs-only PRs and PRs
   labelled `no-release` are exempt and create no Release.

## Considered Options

- **Semantic-release or Conventional Commits deciding the version in CI:** rejected. The version is a judgment call
  (a schema migration is a major bump), CI would have to push commits to `main`, and it replaces a rule contributors
  already follow.
- **Keep `[Unreleased]` and have CI cut it at release time:** rejected. CI would commit the renamed heading back to
  `main`, a bot push on a protected branch, and the section would mix several PRs if two merged before a deploy finished.
- **`--generate-notes` only:** rejected. PR titles alone lose the user-facing description the CHANGELOG already has.
- **Release on merge, before deployment:** rejected. A failed deploy would leave a Release for a version that never ran.

## Consequences

- PR authors pick the version number before merge. Two open PRs that pick the same number conflict in
  `AppVersion.cs`, and the second must bump again after rebasing.
- A CHANGELOG date is the day the PR was written, which can be a day or two before the Release.
- Versions 2.0.2 to 4.1.0 have no Release or CHANGELOG section of their own; their entries are under `[4.1.1]`.
