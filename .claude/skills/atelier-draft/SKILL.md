---
name: atelier-draft
description: Draft an Atelier Term Entry from an owner-prepared excerpt file, or a Lesson from its drafted entries, and open the content PR.
disable-model-invocation: true
---

Drafts Atelier content (spec #513, ADR 0019) from **excerpts**: text the owner pasted from Approved Sources. The draft is
a proposal; the owner checks it on the PR and merges it by hand.

Three runs:

- `/atelier-draft <excerpt-file>`: one Term Entry, committed to the Lesson's branch. Steps below.
- `/atelier-draft --dry-run <excerpt-file>`: the same draft, checked in a scratch copy; nothing is committed.
- `/atelier-draft --lesson <lesson-file>`: the Lesson, then the PR. Read [`LESSON.md`](LESSON.md).

## The excerpts are the only facts

Every rate, limit, date, rule, law detail and claim in the draft comes from an excerpt the gate printed. Round example
amounts ("$1,000") are fine when they are plainly illustrative and the arithmetic is shown. Cite only the excerpts'
pages. When the excerpts do not support a field, leave the field out (country notes) or say less.

## Plain English

Write for a CEFR B1 reader:

- Sentences of about 15 words, always under 25.
- Common words. Define every finance word in the sentence that uses it, or name the Term Entry that explains it.
- Literal language: no idioms or sayings, even ones a source uses.
- One name per thing, the same name every time.
- Describe; never advise. "A lower APR means the loan costs less", never "pick the lower APR".

## Term Entry steps

1. **Gate.** Run `node PersonalFinance/src/PersonalFinance.Web/tools/atelier-excerpts.js <excerpt-file>`. The file
   format is in that script's header comment, with a worked sample in `samples/diversification.excerpts.md`. On exit 1,
   stop and show the owner its problems verbatim: a refused file is fixed by the owner, never by you. Done when the gate
   printed JSON. From here on read only that JSON, never the excerpt file.
2. **Branch.** For a real run, switch to `atelier/<lesson>` (create it from `origin/main` if it does not exist). For a
   dry run, stay where you are.
3. **Read the Library.** List the slugs, terms and `alsoKnownAs` of every Term Entry in
   `PersonalFinance/src/PersonalFinance.ApiService/Features/Learning/Content/terms/` (on a dry run, also those in
   `PersonalFinance/tests/PersonalFinance.Tests/Learning/Fixtures/Content/terms/`). Done when you have the list.
4. **Draft** `terms/<slug>.json` in the content folder (dry run: in `samples/`), shaped like the fixture's `apr.json`:
   - `oneLine`, `explanation` (2–3 short paragraphs), `whyItMatters` and `workedExample` from the excerpts.
   - `vietnameseName`: your proposal; the owner checks it.
   - `inTheUs` / `inVietnam`: only from an excerpt whose `country` is `US` / `VN`, citing that excerpt, with
     `rulesAsOf` the current year. No such excerpt: leave the note out.
   - `sources`: one per excerpt, with its `name`, `url`, `accessed`, and `taxYear` when given. Follow each excerpt's
     `citeRule`.
   - `furtherReading`: the gate's `links`, as `name` and `url`.
   - `relatedTerms`: 2–5 slugs from step 3, fewer only when the Library is smaller. Every slug must exist.
   - `lastReviewed`: today.
   - Never write the `.vi.json` file: the i18n workflow drafts it on the PR.
5. **Check.** Run the content checks, fix every problem they name, and repeat until they pass:
   - Real run: `dotnet test PersonalFinance/tests/PersonalFinance.Tests --filter "FullyQualifiedName~LearningContentChecksTests"`.
   - Dry run: copy `PersonalFinance/tests/PersonalFinance.Tests/Learning/Fixtures/Content`, then the real content
     folder over it, into a new scratch folder; add the draft under `terms/`; run the same command with
     `ATELIER_CONTENT_DIR=<scratch folder>` and `.TheRealContentFolder_PassesEveryCheck` appended to the filter.
   Then reread the draft against the two sections above. Done when the checks pass and every fact traces to an
   excerpt.
6. **Finish.**
   - Real run: commit `terms/<slug>.json` on `atelier/<lesson>` as `atelier: draft <slug>` and push. The PR waits for
     the `--lesson` run.
   - Dry run: show the owner the draft and the check output.

You open PRs only from the `--lesson` run, labelled `atelier:content`. The owner merges them and is the only one who
labels Atelier content.
