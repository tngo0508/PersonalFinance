#!/usr/bin/env bash
# Pre-PR version check for the scheduled ticket agent (claude-ticket-agent.yml). Runs the same rules as
# release-check.yml against the checked-out branch, plus the two that keep parallel agent PRs from colliding:
#
#   - the branch contains origin/main (rebase first, so the bump is judged against the main it will merge into)
#   - AppVersion.Current agrees with Major.Minor.Patch and is above main and every other open agent/issue-* PR
#   - CHANGELOG.md's first version section is that version, dated, and not empty
#
# Prints what it found and exits 1 with every problem listed when the PR would fail the release check.
# Needs gh authenticated for the repository (GH_TOKEN) and GH_REPO or a GitHub origin remote.
#
#   agent-version-check.sh [--built-on-main] [BRANCH]
#     BRANCH: this PR's branch, left out of the open-PR comparison (default: HEAD's)
#     --built-on-main: judge against the main the branch is built on (its merge-base), not the latest main. For the
#       workflow's check after the PR is open: if main has moved since, the rebase job deals with that.
set -uo pipefail

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE="$SCRIPTS/release-version.sh"
APP_VERSION_FILE="PersonalFinance/src/PersonalFinance.Shared/Constants/AppVersion.cs"
BUILT_ON_MAIN=false
if [ "${1:-}" = "--built-on-main" ]; then
  BUILT_ON_MAIN=true
  shift
fi
BRANCH="${1:-$(git rev-parse --abbrev-ref HEAD)}"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

problems=()

git fetch -q origin main
main_ref=origin/main
if [ "$BUILT_ON_MAIN" = "true" ]; then
  main_ref="$(git merge-base origin/main HEAD)"
elif ! git merge-base --is-ancestor origin/main HEAD; then
  problems+=("The branch is not on the latest main: run 'git fetch origin main && git rebase origin/main' and resolve AppVersion.cs and CHANGELOG.md.")
fi

if ! version="$(bash "$RELEASE" current 2>&1)"; then
  echo "${version}"
  exit 1
fi
echo "This branch: v$version"

git show "$main_ref:$APP_VERSION_FILE" > "$TMP/main.cs"
main_version="$(bash "$RELEASE" current "$TMP/main.cs")"
echo "main:        v$main_version"
if ! bash "$RELEASE" is-newer "$version" "$main_version"; then
  problems+=("v$version is not above main's v$main_version: bump AppVersion (Current, Major, Minor, Patch together).")
fi

# Open agent PRs other than this one. Their versions are taken: the next to merge would conflict with this one.
while read -r number head; do
  [ -n "$number" ] || continue
  [ "$head" = "$BRANCH" ] && continue
  if ! gh api -H "Accept: application/vnd.github.raw" \
      "repos/{owner}/{repo}/contents/$APP_VERSION_FILE?ref=$head" > "$TMP/pr.cs" 2>/dev/null; then
    echo "PR #$number ($head): could not read its AppVersion, skipped"
    continue
  fi
  pr_version="$(bash "$RELEASE" current "$TMP/pr.cs" 2>/dev/null)" || continue
  echo "PR #$number:    v$pr_version ($head)"
  if ! bash "$RELEASE" is-newer "$version" "$pr_version"; then
    problems+=("v$version is not above open PR #$number's v$pr_version: bump to the next free version.")
  fi
done < <(gh pr list --state open --limit 100 --json number,headRefName \
  --jq '.[] | select(.headRefName | startswith("agent/issue-")) | "\(.number) \(.headRefName)"')

if ! bash "$RELEASE" notes "$version" > /dev/null 2> "$TMP/notes.err"; then
  problems+=("$(sed 's/^::error:://' "$TMP/notes.err"): add '## [$version] - $(date -u +%F)' at the top of CHANGELOG.md with this PR's entries.")
else
  first="$(grep -m 1 -E '^## \[' CHANGELOG.md)"
  case "$first" in
    "## [$version] - "*) echo "CHANGELOG:   $first" ;;
    *) problems+=("CHANGELOG.md's first section is '$first': move the v$version section to the top.") ;;
  esac
fi

if [ "${#problems[@]}" -gt 0 ]; then
  for p in "${problems[@]}"; do echo "::error::$p"; done
  exit 1
fi
echo "OK: v$version is free and its CHANGELOG section is in place."
