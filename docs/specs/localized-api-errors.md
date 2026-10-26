# Localized API Errors — Implementation Spec

**Status:** Ready for implementation
**Decided in:** [#385](https://github.com/tngo0508/PersonalFinance/issues/385) (part of umbrella
[#295](https://github.com/tngo0508/PersonalFinance/issues/295)), a gap left by the L3 slices of the
[localization spec](localization-and-multi-currency.md).
**ADR:** [0017 API errors carry localization keys, Web translates them](../adr/0017-api-errors-carry-localization-keys.md).

## 1. Goal

Every rejection ApiService returns (a validator message, a "not found", a "already exists") reaches the user in their
Language, on every localized Feature, through the same localizer and JSON files as the rest of the app (ADR 0015).
ApiService stays language-free. The two Web pages that today translate by matching English text stop doing so.

Out of scope: the Admin area (not localized; its errors stay English), 5xx responses (already a generic message),
translating text ASP.NET itself writes into a model-binding 400 (shown as one generic message instead), and errors from
third-party services passed through as written (Google Drive API text, AI provider text).

## 2. Decisions, by area

### 2.1 The contract (`Shared`)

- **`ApiErrorDto`** in `PersonalFinance.Shared/Contracts/ApiErrorDto.cs` is the body of every 4xx ApiService returns
  for a converted Feature:

  ```json
  {
    "errors": [
      {
        "key": "BudgetPlanner.Validation.MonthlyIncomeRange",
        "message": "Monthly income must be over $0 and at most $10,000,000.",
        "field": "MonthlyIncome",
        "args": {
          "min": { "kind": "Money", "value": "0", "currency": "USD" },
          "max": { "kind": "Money", "value": "10000000", "currency": "USD" }
        }
      }
    ]
  }
  ```

  `key` is required. `message` is the English snapshot (logs, tests, Swagger; see 2.4). `field` is the DTO property
  path as FluentValidation reports it (`Allocations.Housing`), or absent for a whole-request error. `args` is absent
  when the template has no placeholders.
- **`ApiErrorArgDto`** is `{ kind, value, currency? }` with `kind` one of `Text`, `Number`, `Money`. `value` is a string
  in the invariant culture (`10000000`, `2.5`, `300`); `currency` is an ISO 4217 code and is required for `Money`.
- **`ApiErrorKeys`** in `PersonalFinance.Shared/Constants/ApiErrorKeys.cs` holds every key as a `const string`, grouped
  in nested static classes per Feature (`ApiErrorKeys.BudgetPlanner.MonthlyIncomeRange`). The constant's value is the
  `en.json` key: `<Feature>.Validation.<Rule>` for a validator rule, `<Feature>.Error.<Rule>` for any other rejection.
  Keys that already exist in `en.json` (`MoneyPriorities.Validation.*`) are reused, not renamed.
- **Not versioned.** Web is the only caller and ships with ApiService (ADR 0006). A Feature that has not been converted
  sends its old shape and Web handles it as today (2.3).
- `MoneyPrioritiesValidationErrorsDto` is removed: its `Errors` + `FieldErrors` are `ApiErrorDto` with `field` set.

### 2.2 Producing it (`ApiService`)

Two paths, both in `PersonalFinance.ApiService/Infrastructure/`; no controller builds an error body by hand.

- **Validators.** Every rule gets `.WithErrorCode(ApiErrorKeys.<Feature>.<Rule>)` and keeps `.WithMessage("…")` as
  the English snapshot. Arguments go through `.WithState(_ => ErrorArgs.Money("max", limit, currency))` (a small
  builder returning `Dictionary<string, ApiErrorArgDto>`). The extension `validationResult.ToApiError()` maps each
  `ValidationFailure` to one entry (`ErrorCode` → key, `ErrorMessage` → message, `PropertyName` → field, `CustomState`
  → args). A controller writes `return BadRequest(validation.ToApiError());` and nothing else.
- **Rejections raised in a service.** `ApiErrorException(string key, string message, string? field = null,
  IReadOnlyDictionary<string, ApiErrorArgDto>? args = null, int statusCode = 400)` replaces the `ArgumentException`,
  `KeyNotFoundException` and `DuplicateCategoryException` throws that controllers currently catch and rewrap. One
  `ApiErrorExceptionFilter` (registered globally in `Program.cs`) renders it as `ApiErrorDto` with its status code.
  Controllers drop the try/catch. `DuplicateCategoryException` and similar Feature exceptions become subclasses or are
  deleted in favour of the base type; their status stays 400 so Web's JS keeps its current status checks.
- **Ad-hoc `BadRequest("…")` and `BadRequest(new { error = "…" })`** become `throw new ApiErrorException(...)` or
  `BadRequest(ApiError.Single(key, message))` where a throw would read worse. The `NetWorthProjectionValidator`
  (a hand-written static validator returning a string) returns an `ApiErrorDto` entry instead.
- **Framework 400s stay.** `[ApiController]`'s automatic `ValidationProblemDetails` for model-binding failures is not
  suppressed; Web recognizes the shape (2.3).
- ApiService never formats an argument for display. The English `message` may contain a formatted figure
  (`$10,000,000`) only because it is a snapshot; nothing parses it.

### 2.3 Resolving it (`Web`)

- **`ApiErrorMessages`** stays a static helper and gains the localizer as a parameter, like `NotificationText`:
  `ForUser(IStringLocalizer, Exception, string fallbackKey)`, `FromApiException(IStringLocalizer, ApiException)`, and
  `Fields(IStringLocalizer, ApiException)` → `IReadOnlyDictionary<string, IReadOnlyList<string>>` for pages that render
  errors under their inputs (Money Priorities). Resolution of one entry: `localizer[key]`; `ResourceNotFound` → the
  entry's `message`; placeholders filled by `LocalizerExtensions.FormatNamed` after each arg is formatted (2.5).
- **Three body shapes, detected in order:** (1) `errors` is an array whose items have `key` → `ApiErrorDto`;
  (2) `title` plus `errors` as an object, or a `type` URL → ASP.NET `ProblemDetails`, shown as the status code's key
  (`Errors.InvalidRequest` for 400, `Errors.NotFound` for 404, `Errors.Conflict` for 409, otherwise `Errors.Generic`)
  with no text taken from the body; (3) anything else → today's best-effort parsing, kept until the last Feature is
  converted, then deleted (slice 3).
- **Web's own messages become keys** in `en.json` / `vi.json` under `Errors.`: `Errors.Unavailable` ("The service is
  temporarily unavailable. Please try again."), `Errors.Unexpected`, `Errors.Server`, `Errors.NotFound`,
  `Errors.Conflict`, `Errors.InvalidRequest`, `Errors.Generic`. The `const string`s on `JsonApiErrorsFilter` go; the
  filter resolves the keys through the localizer it is given by DI. Callers that pass their own English fallback
  (`ForUser(ex, "Could not save the budget.")`) pass a key instead.
- **JavaScript** keeps reading `{ "error": "…" }` from Web's JSON endpoints and shows it; the text is already in the
  user's Language. No `<js-strings>` keys are added for API errors. Pages whose JS renders per-field errors receive the
  field map from the Web action as JSON, resolved server-side.
- **The shims.** The `Messages` table in `MoneyPrioritiesText` (English text → localizer call) and its `AmountRange`
  helper are deleted; the controller calls `ApiErrorMessages.Fields` instead. The id-keyed lookups stay
  (`MoneyPrioritiesText.LeafLabel`, the Purpose Note state, `CommunityText.Category`, `StatTitle`): they translate by a
  stable id, not by English text, and are not what #385 is about.

### 2.4 One source of English

`en.json` is the display text; the ApiService `message` is never shown for a converted Feature. Two tests keep it so:

- `ApiErrorKeysTests`: every `const string` on `ApiErrorKeys` (reflection over the nested classes) has an `en.json`
  entry, and the entry's placeholders are a subset of the arg names the key is used with. Fails the build on a missing
  key, which is how a forgotten translation surfaces, instead of English leaking at runtime.
- `ApiErrorArchitectureTests`: in every converted Feature folder, no `BadRequest(` whose argument is a string literal or
  an anonymous object, no `throw new ArgumentException(`/`KeyNotFoundException(` in a service, and no
  `.WithErrorCode("` with a literal. Converted Features are listed in the test and the list grows per slice; slice 3
  replaces the list with "all Features except Admin".

Vietnamese entries are produced by `npm run i18n:translate` in its own commit per PR (ADR 0015 §4); no English is typed
into `vi.json` by hand.

### 2.5 Arguments and formatting

Web formats every argument in the request culture (`CultureInfo.CurrentCulture`, `vi-VN` for Vietnamese):

| `kind` | Formatting | Example `en-US` / `vi-VN` |
|---|---|---|
| `Text` | as given | `Housing` / `Housing` |
| `Number` | `decimal.Parse(value, Invariant)` then `ToString("N", culture)` with trailing zeros trimmed | `10,000,000` / `10.000.000` |
| `Money` | `MoneyFormatter.FormatWhole` or `Format` (ADR 0016) with the arg's `currency` | `$10,000,000` / `10.000.000 $` |

Limits in validators are `Shared` constants today (`MoneyPrioritiesLimits.MonthlyAmountMax`); the validator passes them
as args, so Web no longer needs a table of which constant fills which placeholder. A `Money` limit carries
`SupportedCurrencies.Default.Code` until a validator is given the request's currency (not this work).

## 3. Slices

Each PR is a release (ADR 0006), **minor** bump, with its `i18n: translate` commit. Nothing here touches the schema.

| Slice | Content |
|---|---|
| **E1** | ADR 0017; `ApiErrorDto`, `ApiErrorArgDto`, `ApiErrorKeys`; `ApiErrorException`, `ApiErrorExceptionFilter`, `ToApiError()`, `ErrorArgs`; `ApiErrorMessages` with the localizer, three-shape detection, `Fields`; `Errors.*` keys and the end of the `JsonApiErrorsFilter` constants; **Money Priorities** and **Community** converted (validator, `CommunityService` throws, the vote result's `ErrorMessage`), `MoneyPrioritiesValidationErrorsDto` and the `MoneyPrioritiesText.Messages` table removed; both tests with the converted list `[MoneyPriorities, Community]`; `DIAGRAMS.md` §16 and the `ARCHITECTURE.md` add-a-feature checklist updated. |
| **E2** | **Budget Tracker** and **Budget Planner**: seven validators, `MonthlyBudgetService` throws (`DuplicateCategoryException`, `KeyNotFoundException`, `ArgumentException`), the `"Invalid year or month."` checks, `BudgetController`'s `{ ErrorCode, ErrorMessage }` shape. Web's `BudgetController` and the budget JS keep working unchanged apart from fallback keys. |
| **E3** | **Feedback, Account Lifecycle, AI Assistant, Watchlist + News, Net Worth, Research, Analysis, Google Drive**: remaining validators and rejections. Google Drive and AI provider text that is passed through from a third party is wrapped in one key with the text as a `Text` arg (`GoogleDrive.Error.Provider`: "Google Drive said: {detail}"). Delete the legacy parsing branch of `ApiErrorMessages`; the architecture test switches to "every Feature except Admin"; close #385. |

Slices are independent of each other after E1; E2 and E3 may run in parallel worktrees (`AGENTS.md`), renumbering on
rebase.

## 4. Adding a rejection (future, per Feature)

1. Add the constant to `ApiErrorKeys.<Feature>` and the English sentence to `en.json`.
2. `.WithErrorCode(...)` on the rule, or `throw new ApiErrorException(...)` in the service, with args for every
   placeholder.
3. `npm run i18n:translate`; commit separately.
4. Nothing in Web: `ApiErrorMessages` resolves it. The two tests fail if a step was skipped.

This is added as a step to the add-a-feature checklist in `ARCHITECTURE.md`.

## 5. Tests the slices must include

- xUnit, `Shared`/`ApiService`: `ToApiError()` maps code, message, field and state; the filter renders
  `ApiErrorException` with its status and `ApiErrorDto` body; each converted validator asserts error **codes**, not
  English; `ApiErrorKeysTests` and `ApiErrorArchitectureTests` (2.4).
- xUnit, `Web`: `ApiErrorMessages` resolves a known key in `vi`, falls back to `message` for an unknown key, formats a
  `Money` arg in `vi-VN` with the arg's currency, maps `ProblemDetails` to its status code's key, and (until E3) still
  reads the legacy shapes; `Fields` groups by field; `JsonApiErrorsFilter` returns the localized `Errors.Unavailable`.
  Tests that read the culture pin it (as #384 does).
- Playwright: one smoke in E1: switch to Vietnamese, submit the Money Priorities form with an out-of-range amount, assert
  the field error is Vietnamese and shows `1.000.000`. No further E2E per Feature.
- Security (`financial-security` skill): the filter never writes exception details or stack traces into `message`;
  `Text` args are HTML-encoded by Razor on output and never inserted as markup; third-party text is passed as an arg,
  never as a key.

## 6. Hand-off

Implementation sessions start from this document and ADR 0017. A question this spec does not answer is new work: open an
issue under #295 rather than deciding in a PR.
