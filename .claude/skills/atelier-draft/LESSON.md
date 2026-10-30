# The Lesson run

`/atelier-draft --lesson <lesson-file>` writes the Lesson file from the Term Entries already drafted on its branch, then
opens the content PR (#513: a Lesson ships in one PR with its Term Entries). It adds no facts: every sentence restates a
fact from one of those entries. The plain-English rules in [`SKILL.md`](SKILL.md) apply.

## The lesson file

The owner writes the parts that are decisions, from the Journey table in #513:

```markdown
---
slug: build-a-cushion
day: 1
title: Build a cushion
terms: emergency-fund, compound-interest
try-it: Savings Goals | /SavingsGoals
keepsake: 🪴 | Seedling
---
```

## Steps

1. **Branch.** Switch to `atelier/<slug>`. Done when every slug in `terms` has its `terms/<slug>.json` on the branch or
   on `main`; otherwise stop and name the missing ones.
2. **Draft** `lessons/<slug>.json` in the content folder, shaped like the fixture's `lessons/borrow-wisely.json`:
   - `slug`, `day`, `title`, `terms` (in the file's order), `tryIt` (`label`, `url`) and `keepsake` (`symbol`, `name`)
     from the lesson file.
   - `intro`: 2–3 sentences on what the Lesson shows.
   - `check`: 2–3 questions. `myth-or-fact` has the options `["Myth", "Fact"]`; `what-happens-next` has 3 options.
     Each `explanation` opens with the answer and gives the reason in one sentence. Every answer is stated in one of
     the Lesson's entries.
3. **Check.** Run `dotnet test PersonalFinance/tests/PersonalFinance.Tests --filter "FullyQualifiedName~LearningContentChecksTests"`,
   fix every problem, repeat until it passes.
4. **Release.** Bump `AppVersion` a minor version and add the `CHANGELOG.md` section, as `AGENTS.md` describes: an
   **Added** entry naming the Lesson and its Term Entries. Commit as `atelier: Lesson <day>, <title>` and push.
5. **PR.** Open it with `gh pr create --label atelier:content`, titled `Atelier: Lesson <day>, <title>`, the body
   following the `pr` skill, with `Closes #<issue>` when the owner named the Lesson's issue. Add this checklist for
   the owner:
   - [ ] Every fact in each entry matches its cited source.
   - [ ] Worked-example arithmetic is right.
   - [ ] Each Vietnamese name is right.
   - [ ] Each *In Vietnam* note is right, or rightly absent.
   - [ ] The Vietnamese the i18n workflow drafts on this PR reads well.
   - [ ] The Lesson and its entries read well on a local run, in light and dark mode.

   Done when the PR is open; give the owner its link.
