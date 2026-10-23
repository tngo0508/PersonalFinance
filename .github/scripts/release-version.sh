#!/usr/bin/env bash
# Release helpers shared by release-check.yml (PRs) and the create-release job in deploy-azure.yml.
# See docs/adr/0006-one-release-per-merged-pr.md.
#
#   release-version.sh current [AppVersion.cs]   Print AppVersion.Current; fail if it disagrees with Major.Minor.Patch[-Suffix]
#   release-version.sh is-newer NEW OLD          Exit 0 when SemVer NEW is higher than OLD
#   release-version.sh notes VERSION [CHANGELOG] Print the body of CHANGELOG's "## [VERSION] - YYYY-MM-DD" section
set -euo pipefail

DEFAULT_APP_VERSION="PersonalFinance/src/PersonalFinance.Shared/Constants/AppVersion.cs"
DEFAULT_CHANGELOG="CHANGELOG.md"

fail() {
  echo "::error::$*" >&2
  exit 1
}

# Value of a `const` field in AppVersion.cs: strips quotes and the trailing `;`.
const_value() {
  local name="$1" file="$2"
  sed -n -E "s/^[[:space:]]*public const [a-z?]+ ${name} = \"?([^\";]*)\"?;.*/\1/p" "$file" | head -n 1
}

cmd_current() {
  local file="${1:-$DEFAULT_APP_VERSION}"
  [[ -f "$file" ]] || fail "AppVersion file not found: $file"

  local current major minor patch suffix expected
  current="$(const_value Current "$file")"
  major="$(const_value Major "$file")"
  minor="$(const_value Minor "$file")"
  patch="$(const_value Patch "$file")"
  suffix="$(const_value Suffix "$file")"
  [[ "$suffix" == "null" ]] && suffix=""

  [[ -n "$current" && -n "$major" && -n "$minor" && -n "$patch" ]] || fail "Could not read the version from $file"
  expected="${major}.${minor}.${patch}${suffix:+-$suffix}"
  [[ "$current" == "$expected" ]] || fail "AppVersion.Current is '$current' but Major.Minor.Patch[-Suffix] is '$expected'"
  echo "$current"
}

# SemVer precedence for MAJOR.MINOR.PATCH[-PRERELEASE]: a release outranks its own pre-releases.
cmd_is_newer() {
  local new="$1" old="$2"
  local new_core="${new%%-*}" old_core="${old%%-*}"
  local new_pre="" old_pre=""
  [[ "$new" == *-* ]] && new_pre="${new#*-}"
  [[ "$old" == *-* ]] && old_pre="${old#*-}"

  if [[ "$new_core" != "$old_core" ]]; then
    [[ "$(printf '%s\n%s\n' "$new_core" "$old_core" | sort -V | tail -n 1)" == "$new_core" ]]
    return
  fi
  [[ -z "$new_pre" && -n "$old_pre" ]] && return 0
  [[ -z "$new_pre" || -z "$old_pre" || "$new_pre" == "$old_pre" ]] && return 1
  [[ "$(printf '%s\n%s\n' "$new_pre" "$old_pre" | sort -V | tail -n 1)" == "$new_pre" ]]
}

cmd_notes() {
  local version="$1" file="${2:-$DEFAULT_CHANGELOG}"
  [[ -f "$file" ]] || fail "CHANGELOG not found: $file"

  local escaped notes
  escaped="$(printf '%s' "$version" | sed 's/[.]/\\./g')"
  grep -q -E "^## \[${escaped}\] - [0-9]{4}-[0-9]{2}-[0-9]{2}[[:space:]]*$" "$file" \
    || fail "CHANGELOG.md has no '## [$version] - YYYY-MM-DD' section"

  # Lines after the heading, up to the next version heading or `---` rule; blank lines at either end trimmed
  notes="$(awk -v heading="## [${version}] - " '
    index($0, heading) == 1 { found = 1; next }
    found && (/^## \[/ || /^---[[:space:]]*$/) { exit }
    found { print }
  ' "$file" | sed -e '/./,$!d' | sed -e ':a' -e '/^\n*$/{$d;N;ba' -e '}')"
  [[ -n "$notes" ]] || fail "The CHANGELOG.md section for $version is empty"
  printf '%s\n' "$notes"
}

case "${1:-}" in
  current) shift; cmd_current "$@" ;;
  is-newer) shift; [[ $# -eq 2 ]] || fail "usage: $0 is-newer NEW OLD"; cmd_is_newer "$@" ;;
  notes) shift; [[ $# -ge 1 ]] || fail "usage: $0 notes VERSION [CHANGELOG]"; cmd_notes "$@" ;;
  *) fail "usage: $0 {current|is-newer|notes} ..." ;;
esac
