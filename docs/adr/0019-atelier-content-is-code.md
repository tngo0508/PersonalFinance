# 0019 – Atelier Content Is Code, Reviewed and Published Through Pull Requests

**Date:** 2026-10-27  
**Status:** Accepted

## Context

The Atelier ([spec #513](https://github.com/tngo0508/PersonalFinance/issues/513)) publishes Term Entries and Lessons
that an AI drafts from Approved Sources, and nothing AI-written may reach users unreviewed. One owner writes a handful of
entries a month, and every change in this repo is already reviewed as a pull request. Charted on
[#495](https://github.com/tngo0508/PersonalFinance/issues/495).

## Decision

1. **Content lives as files in the repo**: one JSON file per Term Entry and per Lesson, in English and Vietnamese, with
   a permanent slug. The ApiService loads them at startup. The database holds only users' Journey progress.
2. **A pull request is the review, and merging publishes.** Only Published content is on `main`; a draft exists only as
   an open pull request.
3. **Drafting is an owner-run project skill**, fed only owner-supplied excerpts of Approved Sources, not ApiService
   code.
4. **Build checks enforce the measurable rules** (lengths, sources, references, plain English). The owner reviews the
   facts, the tone and clarity.
5. **Agent Auto-Merge never merges a pull request that touches Atelier content**, and the i18n bot's commit never
   counts as approval.

## Considered Options

- **Content in the database with an Admin review page.** Publishing would need no deploy, but it means content tables,
  an editor, a review queue and a separate translation path, since the i18n Translate workflow only works on repo
  files. Rejected as far more to build for one reviewer.

## Consequences

- A typo fix needs a deploy. Content pull requests follow the release rule (minor for new content, patch for fixes).
- The i18n Translate workflow is extended to the content files.
- Unattended automation must never merge content: the auto-merge guard is part of the review gate, not an
  optimisation.
