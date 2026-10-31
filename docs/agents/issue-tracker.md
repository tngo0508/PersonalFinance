# Issue tracker: GitHub

Issues and specs for this repo live as GitHub issues. Use the `gh` CLI for all operations.

## Project board

Issues are tracked visually on the **PersonalFinance Roadmap** project board:

- **URL:** <https://github.com/users/tngo0508/projects/7>
- **Project number:** 7 (owner: `tngo0508`)

When creating or triaging issues, ensure they appear on this board. Use `gh project item-archive` / `gh project item-add`
to move items between boards if needed.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <number> --json number,title,body,labels,comments`.
- **List issues**:
  `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`
  with appropriate `--label` and `--state` filters.
- **Make an issue a sub-issue of a parent**: `gh issue create --parent <parent> ...`, or
  `gh issue edit <parent> --add-sub-issue <child>` afterwards (`gh` 2.94+). Older `gh`:
  `gh api --method POST repos/<owner>/<repo>/issues/<parent>/sub_issues -F sub_issue_id=<child>` (database id, as
  in **Blocking** below). Without sub-issues, put `Part of #<parent>` at the top of the child body.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

Infer the repo from `git remote -v`; `gh` does this automatically when run inside a clone.

## Pull requests as a triage surface

**PRs as a request surface: no.** _(Set to `yes` if this repo treats external PRs as feature requests; `/triage` reads
this flag.)_

When set to `yes`, PRs run through the same labels and states as issues, using the `gh pr` equivalents:

- **Read a PR**: `gh pr view <number> --comments` and `gh pr diff <number>` for the diff.
- **List external PRs for triage**:
  `gh api --paginate 'repos/{owner}/{repo}/pulls?state=open' --jq '.[] | select(.author_association | IN("OWNER","MEMBER","COLLABORATOR") | not) | {number, title, author: .user.login, author_association, labels: [.labels[].name]}'`.
- **Comment / label / close**: `gh pr comment`, `gh pr edit --add-label`/`--remove-label`, `gh pr close`.

GitHub shares one number space across issues and PRs, so a bare `#42` may be either: resolve with `gh pr view 42` and
fall back to `gh issue view 42`.

## When a skill says "publish to the issue tracker"

Create a GitHub issue.

## When a skill says "fetch the relevant ticket"

Read it as in **Read an issue** above.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single issue with **child** issues as tickets.

- **Map**: a single issue labelled `wayfinder:map`, holding the Notes / Decisions-so-far / Fog body.
  `gh issue create --label wayfinder:map`.
- **Child ticket**: an issue linked to the map as a GitHub sub-issue (see **Make an issue a sub-issue of a parent**).
  Where sub-issues aren't enabled, add the child to a task list in the map body and put `Part of #<map>` at the top of
  the child body. Labels: `wayfinder:<type>` (`research`/`prototype`/`grilling`/`task`). Once claimed, the ticket is
  assigned to the driving dev.
- **Blocking**: GitHub's **native issue dependencies**, the canonical, UI-visible representation. Add an edge with
  `gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`, where
  `<blocker-db-id>` is the blocker's numeric **database id** (`gh api repos/<owner>/<repo>/issues/<n> --jq .id`, _not_
  the `#number` or `node_id`). GitHub reports `issue_dependencies_summary.blocked_by` (open blockers only, the live
  gate). Where dependencies aren't available, fall back to a `Blocked by: #<n>, #<n>` line at the top of the child body.
  A ticket is unblocked when every blocker is closed.
- **Frontier query**: list the map's open children (`gh issue list --state open`, scoped to the map's sub-issues / task
  list), drop any with an open blocker (`issue_dependencies_summary.blocked_by > 0`, or an open issue in the
  `Blocked by` line) or an assignee; first in map order wins.
- **Claim**: `gh issue edit <n> --add-assignee @me`, the session's first write.
- **Resolve**: `gh issue comment <n> --body "<answer>"`, then `gh issue close <n>`, then append a context pointer
  (gist + link) to the map's Decisions-so-far.

## Scheduled ticket agent

`.github/workflows/claude-ticket-agent.yml` works the `ready-for-agent` queue unattended, one issue per run, back to
back from 00:00 to 19:00 Pacific every day. It stays off 19:00-24:00, when the owner reviews PRs and works locally.
A run that succeeds starts the next one itself (`-f chained=true`) while at least 45 minutes of the window are left.
The chain stops when a run fails, finds no eligible ticket or hits the open-PR cap. A cron tick every 30 minutes
restarts it, but GitHub delays or drops many ticks, so do not rely on cron alone. The last start is about 18:15, and a
late run's timeout is cut so that it ends by 19:00. Trigger it by hand at any time with
`gh workflow run claude-ticket-agent.yml` (it then chains), or `-f issue=<n>` for one issue only (no chain).

- **Picks** the oldest open `ready-for-agent` issue assigned to the owner and no one else, with no `agent:in-progress`, `wayfinder:map` or
  `atelier:content` label, no sub-issues, no open blockers and no open PR that closes it. It pauses while 5 PRs from `agent/issue-*` branches
  are open (drafts included). Anyone can open an issue on this public repository, so the assignment is the owner's
  go-ahead: assign a ticket to yourself to hand it to the agent. A local session that takes a `ready-for-agent` ticket
  itself removes that label, or the agent may pick the same ticket.
- **Claims** it with `agent:in-progress`, then runs `/implement` (tdd, code-review), `dotnet test`, `npm run lint` and,
  when the change touches `e2e-tests/` or the Web project, the E2E suites through `.github/scripts/e2e-inmemory.sh`
  (both apps on the InMemory provider, as CI's E2E job runs them, so the runner needs no database; #545 showed what an
  E2E check nobody ran does in CI). Then it opens a PR from `agent/issue-<n>` that requests the owner's review. The
  agent never merges.
- **Checks the version before the PR**: right before pushing, the agent rebases onto `main` and runs
  `.github/scripts/agent-version-check.sh`. It applies the release check's rules: AppVersion is above `main` and every
  other open agent PR, and CHANGELOG.md's top section is that version and not empty. After the run, the workflow repeats
  the check on the pushed PR. A PR that would fail it becomes a draft with a comment listing the problems.
- **Auto-merges**: `.github/workflows/agent-auto-merge.yml` merges an `agent/issue-*` PR that is not a draft, comes
  from a branch of this repository, was opened by the claude app and closes an issue assigned to the owner alone, as soon
  as every check on its head commit has finished, none failed, and the release check and `claude-review` both passed.
  It also waits until every PR workflow run on that commit has finished and passed, because a run whose jobs have not
  started yet shows no checks (#535, #537 and #539 merged red that way). A failing check or a draft waits for a human. The merge deploys like any other. This needs the `AGENT_GH_TOKEN`
  secret, because a merge made with `GITHUB_TOKEN` would not trigger the deploy or the Release.
- **Leaves Atelier content to the owner** ([ADR 0019](../adr/0019-atelier-content-is-code.md)): Agent Auto-Merge lists
  a PR's changed files (a renamed file's old path too) and never merges one that touches
  `PersonalFinance/src/PersonalFinance.ApiService/Features/Learning/Content/`. It leaves one comment saying the owner
  reviews and merges it, and the PR stays green. No commit counts as approval, the i18n bot's `i18n: translate N keys
  (vi)` included: the guard looks only at the files. The ticket agent also never picks an issue labelled
  `atelier:content`, even one that is `ready-for-agent`.
- **Translates**: `.github/workflows/i18n-translate.yml` runs on every PR from a branch of this repository, drafts the
  missing or stale Vietnamese with `npm run i18n:translate` and pushes it to the PR branch as `i18n: translate N keys
  (vi)`, with `AGENT_GH_TOKEN` so the PR workflows re-run on that commit. Its `Translate (i18n)` check is required by
  Agent Auto-Merge. The agent adds English keys only and never gets the Translator key (secret `AZURE_TRANSLATOR_KEY`,
  variable `AZURE_TRANSLATOR_REGION`): the key goes to that one deterministic step, in no Claude step, so a malicious
  issue cannot leak it. A fork PR gets no secrets and is skipped with a passing check. A placeholder mismatch fails
  the check and waits for a hand correction to `vi.json`.
- **Ignores outsiders**: a fork PR can name its branch `agent/issue-*` too, so the rebase picker and Agent Auto-Merge
  skip cross-repository PRs, and `.github/workflows/claude.yml` answers an `@claude` mention only from the owner.
- **Rebases first**: before picking a ticket, a run looks for an open, non-draft `agent/issue-*` PR that now
  conflicts with `main`. That usually happens because a sibling merged and both PRs touched `AppVersion` and
  `CHANGELOG.md`. Rebasing it is the whole run, even when the open-PR cap is full. The agent re-bumps to the next
  free version, moves its CHANGELOG section to the top, re-runs the checks and force-pushes, and the new CI run lets
  Agent Auto-Merge merge it. A PR it cannot rebase gets `agent:rebase-failed` and waits for a human. Remove that label
  to let the agent retry. A rebase onto the `main` the run started from counts as done even if another PR merged
  meanwhile; the next run rebases it again.
- **Hands back** unclear tickets as `needs-info`, and ones needing a schema migration as `ready-for-human`, unless the
  issue carries `agent:migration-ok`. Unresolved review findings make the PR a draft. A failed or timed-out run moves
  the ticket to `ready-for-human`.
- Only label an issue `ready-for-agent` if you would hand it to a contractor as-is. Split large Specs into tickets
  first.

## Monthly Atelier re-check

`.github/workflows/atelier-recheck.yml` runs on the 1st of each month (and on dispatch) and keeps one open
`ready-for-human` issue, "Atelier notes due for a re-check", listing the Atelier country notes due for a re-check:
notes on Term Entries last reviewed more than 12 months ago, and Tax notes whose Rules-as-of year is before the
current year. With nothing due it updates the open issue to say so and opens none. The selection is
`PersonalFinance.Web/tools/atelier-recheck.js` (`npm run atelier:recheck` runs it locally). The workflow runs with
`GITHUB_TOKEN`, which cannot add an issue to the Roadmap project, so add that issue to the board on triage. Close it
once the notes are re-checked in a content PR; the next run opens a fresh one when something is due again.
