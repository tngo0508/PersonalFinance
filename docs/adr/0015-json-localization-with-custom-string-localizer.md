# 0015 – JSON Localization with a Custom String Localizer

**Date:** 2026-10-25  
**Status:** Accepted

## Context

The app is English-only: culture is pinned to `en-US` in `AppCulture` and `Web/Program.cs`, no `IStringLocalizer` is in
use, and about 55 views, 18 Identity pages and 45 JS modules hold hardcoded text. The owner wants Vietnamese first and
more languages later, with translations produced by a machine (Azure AI Translator, free tier) and reviewed in pull
requests, never by hired translators. The default ASP.NET path is `.resx` satellite assemblies; the obvious alternative is
JSON with a custom localizer. Decided in Wayfinder map #335 (tickets #338, #340, #344).

## Decision

1. **Translations live in one flat JSON file per language**, `PersonalFinance.Web/Resources/i18n/<lang>.json`, with flat
   dotted PascalCase keys, named `{placeholder}`s and `Key.one` / `Key.other` plurals. The same key serves Razor and JS.
2. **An in-house `IStringLocalizer` / `IStringLocalizerFactory`** (~150 lines in `Web/Infrastructure/Localization/`)
   loads every language once at startup, walks `CurrentUICulture → Parent → en`, and reports `ResourceNotFound` so a
   missing-key report can be derived. It is registered before `AddViewLocalization()`.
3. **English is the runtime fallback and CI only reports gaps.** A missing translation never breaks a page or a build;
   malformed JSON or a placeholder mismatch does fail CI.
4. **Translations are machine-drafted in the feature PR** by `npm run i18n:translate`, which sends only missing or stale
   keys (tracked by a hash of the English source in `i18n.lock.json`) to Azure AI Translator and commits the result for
   review. The `i18n Translate` workflow runs it on every PR from a branch of this repository and pushes the commit
   itself, so authors and the ticket agent add English keys only and never hand-type Vietnamese; the Translator key
   reaches that one step and no Claude step. OpenAI is not used for this.
5. **JS receives its strings inline per page**, as a JSON island read by one core module, never a fetched bundle.

## Considered Options

- **`.resx` satellite assemblies:** rejected. Zero plumbing on the server, but XML diffs make every translation PR
  unreviewable, the layout sprawls to ~150 files for two languages, JS cannot read them without a generator, and the one
  real advantage (Visual Studio tooling) is irrelevant to an agent-driven workflow. `.resx` also would not have eased the
  hard part: the Identity RCL pages are precompiled with English text and must be scaffolded regardless of format.
- **`My.Extensions.Localization.Json` (MIT):** acceptable but not chosen. The in-house version covered every requirement in
  about 150 lines and owns the key schema, plural handling and the report.
- **A hosted translation service (Crowdin, Weblate) or human translators:** rejected on cost and on the owner's intent to
  automate.
- **Fetched per-language JS bundle with content hash:** rejected in the prototype; inline per page keeps one request and
  resolves fallback server-side.

## Consequences

- Every user-facing string becomes a key; concatenated sentences are forbidden in favour of named placeholders.
- `"C"` currency formats are replaced by a money formatter that takes the currency code (see ADR 0016), because the
  culture now varies per request while the currency symbol must not.
- Adding a language is a data change: a JSON file, a glossary file, a supported-list entry and one translator run.
