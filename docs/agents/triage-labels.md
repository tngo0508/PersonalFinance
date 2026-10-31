# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those roles to the actual label strings used in
this repo's issue tracker.

| Label in mattpocock/skills | Label in our tracker | Meaning                                  |
|----------------------------|----------------------|------------------------------------------|
| `needs-triage`             | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`               | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`          | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`          | `ready-for-human`    | Requires human implementation            |
| `wontfix`                  | `wontfix`            | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding label string from this
table.

**`ready-for-agent` always comes with the owner as assignee.** The scheduled ticket agent works only on
`ready-for-agent` issues assigned to the repository owner and no one else (see "Scheduled ticket agent" in
`issue-tracker.md`), so whenever a skill applies the label it assigns the issue to the user running it in the same
call: `gh issue create --label ready-for-agent --assignee @me ...` or
`gh issue edit <n> --add-label ready-for-agent --add-assignee @me`. Never assign anyone else alongside.

Edit the "Label in our tracker" column to match whatever vocabulary you actually use.
