# Localized API Errors, Slice E1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ApiService rejections carry a localization key and typed arguments, Web translates them, and Money Priorities and Community are the first two Features converted, with the English-matching table in `MoneyPrioritiesText` deleted.

**Architecture:** `ApiErrorDto` in `Shared` is the one 4xx body. In ApiService, `ApiErrorException` + one global `ApiErrorExceptionFilter` and `ValidationResult.ToApiError()` produce it; nothing else builds an error body. In Web, `ApiErrorMessages` resolves each entry through `IStringLocalizer` (key → `en.json`/`vi.json`, unknown key → the English `message`), formats typed args in the request culture, and still parses legacy bodies for Features not yet converted. Two tests guard the contract: every `ApiErrorKeys` constant is in `en.json`, and converted Features contain no literal error bodies.

**Tech Stack:** .NET 10, ASP.NET Core MVC, FluentValidation (`WithErrorCode`, `WithState`), Refit, System.Text.Json, xUnit, the in-house JSON localizer (ADR 0015), `MoneyFormatter` (ADR 0016), `npm run i18n:translate`.

**Spec:** `docs/specs/localized-api-errors.md` (ADR: `docs/adr/0017-api-errors-carry-localization-keys.md`). Read both first.

## Global Constraints

- Paths below are under `PersonalFinance/src/` (code) and `PersonalFinance/tests/PersonalFinance.Tests/` (tests) unless noted.
- Verify with `dotnet test PersonalFinance.sln` and `npm run lint` (repo root). CI builds Release with warnings as errors: before the final commit run `dotnet build PersonalFinance.sln -c Release -p:EnforceWarningsAsErrors=true`. `IDE0008`: `var` only when the type is on the right-hand side.
- `ApiService` never reads the request language and holds no translation (ADR 0017 §3). `Data` references neither `Web` nor `ApiService`; `Shared` references nothing of ours.
- Keys: `<Feature>.Validation.<Rule>` for a validator rule, `<Feature>.Error.<Rule>` for other rejections, Web's own under `Errors.`. Reuse the 19 existing `MoneyPriorities.Validation.*` keys unchanged.
- Arg `value` is invariant-culture text; `kind` ∈ `Text | Number | Money`; `currency` required for `Money`. Web formats; ApiService never formats for display.
- No hand-typed Vietnamese: new `en.json` keys get `vi.json` entries from `npm run i18n:translate`, committed on their own as `i18n: translate N keys (vi)`.
- Tests that read the culture pin it (`UiCultureScope`, `CultureInfo.CurrentCulture` set and restored), as #384 does.
- Version bump **minor** → `7.14.0`; `CHANGELOG.md` section `## [7.14.0] - <date>`. Renumber if `main` moved.
- Never `[Required]` on a `bool`; `[ValidateAntiForgeryToken]` unchanged on existing MVC form actions; no exception text in any `message`.

## Review Focus

1. A `Number`/`Money` arg whose `value` does not parse as a decimal — expected: shown as given, never an exception on the error path (Task 3, `ApiErrorMessagesTests.UnparsableNumberArg_IsShownAsGiven`).
2. A 404 thrown as `ApiErrorException` — expected: Web's JSON filter answers 404 with the localized message, not 400 (Task 2 `Filter_UsesTheExceptionStatusCode`, Task 3 `JsonApiErrorsFilter_KeepsTheStatusCode`).
3. Two failures on one field (NotNull and a range rule both set) — expected: `Fields` keeps both, in validator order, no duplicates (Task 4, `Fields_KeepsEveryMessageOfAField_InOrder`).
4. A converted Feature's key missing from `vi.json` only — expected: English from `en.json`, never the key text, never the ApiService `message` (Task 3, `KeyInEnglishOnly_FallsBackToEnglishText`).
5. A `Text` arg containing markup (`<b>x</b>`) — expected: the formatter inserts it as text; Razor encodes on output and the Money Priorities JS sets `textContent` (Task 3 `TextArg_IsInsertedUnencoded_EncodingIsTheViewsJob`; Task 4 step 9 checks the JS).

---

### Task 1: The contract in `Shared`

**Files:**
- Create: `PersonalFinance.Shared/Contracts/ApiErrorDto.cs`
- Create: `PersonalFinance.Shared/Constants/ApiErrorKeys.cs`
- Test: `tests/PersonalFinance.Tests/Shared/ApiErrorDtoTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public class ApiErrorDto { public List<ApiErrorEntryDto> Errors { get; set; } = []; }
  public class ApiErrorEntryDto
  {
      public string Key { get; set; } = string.Empty;
      public string Message { get; set; } = string.Empty;
      [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Field { get; set; }
      [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, ApiErrorArgDto>? Args { get; set; }
  }
  public class ApiErrorArgDto
  {
      public ApiErrorArgKind Kind { get; set; }
      public string Value { get; set; } = string.Empty;
      [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Currency { get; set; }
  }
  [JsonConverter(typeof(JsonStringEnumConverter))] public enum ApiErrorArgKind { Text, Number, Money }
  public static class ApiErrorKeys { public static class MoneyPriorities { … } public static class Community { … } }
  ```
  `ApiErrorKeys` nested classes hold `public const string` members whose values are `en.json` keys. Task 4 and Task 5 add the members; this task creates the file with an empty `MoneyPriorities` and `Community` class and the XML doc that says what a key is.

- [ ] **Step 1: Write the failing tests** in `ApiErrorDtoTests`:
  - `Serializes_KindAsText_AndOmitsNullFieldAndArgs`: serialize an entry with `Key`, `Message` only → JSON has `"key"`, `"message"`, no `"field"`, no `"args"` (use `JsonSerializerOptions { PropertyNamingPolicy = CamelCase }`, the Web/Refit default).
  - `Serializes_MoneyArg_WithKindNameAndCurrency`: `Args = { ["max"] = new() { Kind = Money, Value = "10000000", Currency = "USD" } }` → JSON contains `"kind":"Money"` and `"currency":"USD"`.
  - `RoundTrips`: deserialize the serialized form → equal `Key`, `Field`, arg `Kind`/`Value`/`Currency`.
- [ ] **Step 2: Run** `dotnet test PersonalFinance.sln --filter ApiErrorDtoTests` → FAIL (types missing).
- [ ] **Step 3: Create the two files** with the types above; `ApiErrorKeys` documents the naming rule from Global Constraints.
- [ ] **Step 4: Run** the filter again → PASS.
- [ ] **Step 5: Commit** `feat(shared): ApiErrorDto and ApiErrorKeys contract (#385)`.

---

### Task 2: Producing the shape in ApiService

**Files:**
- Create: `PersonalFinance.ApiService/Infrastructure/ApiErrorException.cs`
- Create: `PersonalFinance.ApiService/Infrastructure/ApiErrorExceptionFilter.cs`
- Create: `PersonalFinance.ApiService/Infrastructure/ValidationResultExtensions.cs`
- Create: `PersonalFinance.ApiService/Infrastructure/ErrorArgs.cs`
- Modify: `PersonalFinance.ApiService/Program.cs:83` (`AddControllers()` → `AddControllers(options => options.Filters.Add<ApiErrorExceptionFilter>())`)
- Test: `tests/PersonalFinance.Tests/Infrastructure/ApiErrorProductionTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces:
  ```csharp
  public sealed class ApiErrorException(string key, string message, string? field = null,
      IReadOnlyDictionary<string, ApiErrorArgDto>? args = null, int statusCode = StatusCodes.Status400BadRequest) : Exception(message)
  { public string Key {get;} public string? Field {get;} public IReadOnlyDictionary<string, ApiErrorArgDto>? Args {get;} public int StatusCode {get;}
    public ApiErrorDto ToDto(); }
  public sealed class ApiErrorExceptionFilter(ILogger<ApiErrorExceptionFilter> logger) : IExceptionFilter  // ObjectResult(ToDto()) { StatusCode }, ExceptionHandled = true; LogInformation(key, status); other exceptions untouched
  public static class ValidationResultExtensions { public static ApiErrorDto ToApiError(this ValidationResult result); }
  // ErrorCode → Key, ErrorMessage → Message, PropertyName → Field (null when empty), CustomState as IReadOnlyDictionary<string, ApiErrorArgDto> → Args
  public static class ErrorArgs
  {
      public static IReadOnlyDictionary<string, ApiErrorArgDto> Text(string name, string value);
      public static IReadOnlyDictionary<string, ApiErrorArgDto> Number(string name, decimal value);
      public static IReadOnlyDictionary<string, ApiErrorArgDto> Money(string name, decimal value, string currencyCode);
      public static IReadOnlyDictionary<string, ApiErrorArgDto> MoneyRange(decimal min, decimal max, string currencyCode); // "min" and "max"
  }
  ```
  `Number`/`Money` write `value.ToString(CultureInfo.InvariantCulture)` with trailing zeros trimmed (`10000000`, not `10000000.00`).

- [ ] **Step 1: Write the failing tests** in `ApiErrorProductionTests`:
  - `Filter_RendersApiErrorException_AsItsStatusAndDto`: throw `new ApiErrorException("Community.Error.PollNotFound", "Poll not found or inactive.", statusCode: 404)` through an `ExceptionContext`; assert `ObjectResult.StatusCode == 404`, value is `ApiErrorDto` with one entry whose `Key` and `Message` match, `ExceptionHandled`.
  - `Filter_LeavesOtherExceptionsAlone`: `InvalidOperationException` → `ExceptionHandled == false`, `Result == null`.
  - `ToApiError_MapsCodeMessageFieldAndState`: a `ValidationResult` with one `ValidationFailure("MonthlyAmount", "msg") { ErrorCode = "K", CustomState = ErrorArgs.MoneyRange(0m, 1_000_000m, "USD") }` → entry `Key == "K"`, `Field == "MonthlyAmount"`, `Args["max"].Value == "1000000"`, `Args["max"].Kind == Money`, `Currency == "USD"`.
  - `ToApiError_WithoutState_HasNullArgs_AndEmptyPropertyIsNullField`.
  - `ErrorArgs_Number_WritesInvariantWithoutTrailingZeros`: `Number("max", 2000.00m)["max"].Value == "2000"`; `Number("x", 2.5m)` → `"2.5"`.
- [ ] **Step 2: Run** `--filter ApiErrorProductionTests` → FAIL.
- [ ] **Step 3: Implement the four files and register the filter.** `ErrorArgs` writes decimals as `value.ToString("0.############################", CultureInfo.InvariantCulture)`.
- [ ] **Step 4: Run** the filter → PASS. Also run `--filter FeatureLayoutTests` → PASS (new types are in `Infrastructure`).
- [ ] **Step 5: Commit** `feat(api): ApiErrorException, filter and ToApiError produce ApiErrorDto (#385)`.

---

### Task 3: Resolving the shape in Web

**Files:**
- Modify: `PersonalFinance.Web/Infrastructure/ApiErrorMessages.cs` (whole file)
- Create: `PersonalFinance.Web/Infrastructure/ApiErrorArgFormat.cs`
- Modify: `PersonalFinance.Web/Infrastructure/JsonApiErrorsAttribute.cs:30-67`
- Modify: `PersonalFinance.Web/Resources/i18n/en.json` (add `Errors.*`)
- Modify (one line each, add the `_localizer` argument): `Areas/Identity/Pages/Account/CancelDeletion.cshtml.cs:73`, `Areas/Identity/Pages/Account/Manage/DeletePersonalData.cshtml.cs:98`, `Areas/Identity/Pages/Account/Manage/PersonalData.cshtml.cs:66`, `Controllers/AdminController.cs:107,171`, `Controllers/BudgetController.cs:113`, `Controllers/FeedbackController.cs:77`, `Controllers/GoogleDriveController.cs:99,155,200,229,307`, `Controllers/NetworthController.cs:105,127`, `Controllers/SavingsGoalsController.cs:81,124,147`
- Modify: `tests/PersonalFinance.Tests/TestSupport/FilteredAction.cs:26` (filter ctor gains `TestLocalization.Localizer`)
- Modify: `tests/PersonalFinance.Tests/Web/ApiErrorMessagesForUserTests.cs` (signatures; `UnreachableApiService_ShowsUnavailableMessage` asserts the `en.json` text of `Errors.Unavailable`)
- Test: `tests/PersonalFinance.Tests/Web/ApiErrorMessagesTests.cs` (new)

**Interfaces:**
- Consumes: Task 1 types; `LocalizerExtensions.FormatNamed(string template, IReadOnlyDictionary<string, object?> args)`; `MoneyFormatter.Format(decimal, string currencyCode, CultureInfo?)` / `FormatWhole(...)`.
- Produces:
  ```csharp
  public static class ApiErrorMessages
  {
      public static string ForUser(IStringLocalizer localizer, Exception ex, string fallback);      // fallback is already-localized text, as today
      public static string FromApiException(IStringLocalizer localizer, ApiException ex);
      public static IReadOnlyDictionary<string, IReadOnlyList<string>> Fields(IStringLocalizer localizer, ApiException ex); // "" key for whole-request errors; empty dict when the body is not ApiErrorDto
      public static bool TryParse(string? content, out ApiErrorDto? dto);                          // shape (1) only: errors is an array whose items have "key"
      public static string Resolve(IStringLocalizer localizer, ApiErrorEntryDto entry);           // localizer[key]; ResourceNotFound → entry.Message; args via ApiErrorArgFormat then FormatNamed
  }
  internal static class ApiErrorArgFormat { public static string Format(ApiErrorArgDto arg, CultureInfo culture); }
  // Text → Value; Number → decimal (Invariant) → ToString("#,0.############", culture); Money → MoneyFormatter.Format (FormatWhole when the value has no fraction) with arg.Currency; unparsable → Value
  public sealed class JsonApiErrorsFilter(ILogger<JsonApiErrorsFilter> logger, IStringLocalizer localizer) : IExceptionFilter  // the two const strings are gone
  ```
  New `en.json` keys (English copied from today's constants): `Errors.Unavailable` "The service is temporarily unavailable. Please try again.", `Errors.Unexpected` "Something went wrong. Please try again.", `Errors.Server` "A server error occurred. Please try again.", `Errors.NotFound` "The requested resource was not found.", `Errors.Conflict` "A conflict occurred while processing the request.", `Errors.InvalidRequest` "The request was invalid.", `Errors.Generic` "An error occurred while processing the request."

  Body detection order in `FromApiException` (status < 500): (1) `TryParse` → join `Resolve` of each entry with `"; "`; (2) object with `"title"` and `"errors"` as an object, or a `"type"` string → `localizer["Errors.InvalidRequest"]`; (3) today's branches unchanged, their literal fallbacks replaced by the `Errors.*` keys. Status ≥ 500 → `Errors.Server`.

- [ ] **Step 1: Write the failing tests** in `ApiErrorMessagesTests` (use `TestLocalization.Localizer`, `UiCultureScope`, and the `CreateApiExceptionAsync` helper copied from `ApiErrorMessagesForUserTests`; add two test-only keys by asserting against real ones: use `MoneyPriorities.Validation.MonthlyAmountRange` and `MoneyPriorities.Validation.WholeCents`):
  - `KeyedBody_ResolvesInVietnamese`: `ui = vi`, body `{"errors":[{"key":"MoneyPriorities.Validation.WholeCents","message":"EN"}]}` → equals `vi.json`'s text for that key (read via `TestLocalization.Catalog.TryGet("vi", key, out text)`).
  - `UnknownKey_FallsBackToTheEnglishMessage`: key `"Nope.Nope"` → `"EN"`.
  - `KeyInEnglishOnly_FallsBackToEnglishText`: a temporary `JsonLocalizationCatalog` over a temp folder holding `en.json` `{"X.Error.Y":"English only"}` and an empty `vi.json` (as `JsonStringLocalizerTests:146` does), `ui = vi`, entry key `X.Error.Y`, message `"EN"` → `"English only"`, not the key, not `"EN"`.
  - `MoneyArg_FormatsInTheRequestCulture`: `ui = vi`, `CurrentCulture = vi-VN`, `MonthlyAmountRange` with `min` Money 0 USD and `max` Money 1000000 USD → contains `MoneyFormatter.FormatWhole(1_000_000m, "USD", vi-VN)` and no `{`.
  - `NumberArg_FormatsWithGroupSeparators`: Number `2000` in `en-US` → `"2,000"`; in `vi-VN` → `"2.000"` (use `FormatNamed` through a key whose template has `{max}`: `Community.Message.CommentTooLong` exists).
  - `UnparsableNumberArg_IsShownAsGiven`: Number `"abc"` → `"abc"` in the text.
  - `TextArg_IsInsertedUnencoded_EncodingIsTheViewsJob`: Text `"<b>x</b>"` → the string contains `<b>x</b>`.
  - `ProblemDetailsBody_ShowsInvalidRequest`: `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"MonthlyAmount":["The value 'x' is not valid."]}}` → `en.json` text of `Errors.InvalidRequest`, not containing `'x'`.
  - `LegacyErrorBody_StillShowsItsText`: `{"error":"Category 'Food' already exists."}` → that text.
  - `Fields_GroupsByField_AndPutsWholeRequestErrorsUnderEmptyKey`.
  - `JsonApiErrorsFilter_UnreachableApi_AnswersLocalizedUnavailable`: `ui = vi`, `HttpRequestException` → 502 and `vi.json` text of `Errors.Unavailable`.
  - `JsonApiErrorsFilter_KeepsTheStatusCode`: `ApiException` 404 with a keyed body → 404 and the resolved text.
- [ ] **Step 2: Run** `--filter "ApiErrorMessagesTests|ApiErrorMessagesForUserTests"` → FAIL (compile).
- [ ] **Step 3: Add the `Errors.*` keys to `en.json`** (alphabetical block where the other top-level groups sit).
- [ ] **Step 4: Rewrite `ApiErrorMessages`, add `ApiErrorArgFormat`, change `JsonApiErrorsFilter`.** Drop the `CA1502` suppression if the split brings the method under the ceiling; otherwise keep it with an updated justification.
- [ ] **Step 5: Update the 17 call sites and `FilteredAction`**; `AdminController` passes its English literals unchanged (Admin is not localized).
- [ ] **Step 6: Run** `dotnet test PersonalFinance.sln` → PASS (existing Web controller tests use `FilteredAction`).
- [ ] **Step 7: Commit** `feat(web): ApiErrorMessages resolves keyed API errors through the localizer (#385)`.

---

### Task 4: Convert Money Priorities and delete the English-matching table

**Files:**
- Modify: `PersonalFinance.Shared/Constants/ApiErrorKeys.cs` (`MoneyPriorities` members)
- Modify: `PersonalFinance.ApiService/Features/MoneyPriorities/Validators/SubmitPriorityWalkRequestValidator.cs`
- Modify: `PersonalFinance.ApiService/Features/MoneyPriorities/MoneyPrioritiesController.cs:36-45,97-103`
- Delete type: `MoneyPrioritiesValidationErrorsDto` in `PersonalFinance.Shared/DTOs/MoneyPriorities/MoneyPrioritiesDtos.cs:285-289`
- Create: `PersonalFinance.Web/Models/MoneyPriorities/MoneyPrioritiesErrorsViewModel.cs`
- Modify: `PersonalFinance.Web/Controllers/MoneyPrioritiesController.cs:78-124`
- Modify: `PersonalFinance.Web/Models/MoneyPriorities/MoneyPrioritiesText.cs` (remove `Messages`, `Message`, `AmountRange`, `Localize(errors)`; rewrite `Localize(walk)` and `Localize(note)`)
- Test: `tests/PersonalFinance.Tests/MoneyPriorities/SubmitPriorityWalkRequestValidatorTests.cs` (new or extend the existing validator test file if one exists under `MoneyPriorities/`)
- Modify: `tests/PersonalFinance.Tests/MoneyPriorities/MoneyPrioritiesLocalizationTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces:
  ```csharp
  public static class ApiErrorKeys.MoneyPriorities   // one const per en.json key, same member name as the key's last segment:
  // MonthlyAmountRequired, MonthlyAmountRange, WholeCents, PurposeTooLong, DebtAprRequired, DebtAprRange, DebtBalanceRequired,
  // DebtBalanceRange, EmergencyFundMonthsRequired, EmergencyFundMonthsRange, MonthlyExpensesRequired, MonthlyExpensesRange,
  // HasFullEmployerMatchRequired, EmployerMatchPercentRequired, EmployerMatchPercentRange, HorizonYearsRequired, HorizonYearsRange,
  // AgeBandRequired, AtContributionLimitRequired   = "MoneyPriorities.Validation.<Name>"
  public sealed class MoneyPrioritiesErrorsViewModel { public List<string> Errors {get;set;} = []; public Dictionary<string, List<string>> FieldErrors {get;set;} = []; }
  // same JSON the page's JS already reads (money-priorities.js:249-271): { errors, fieldErrors }
  public static class MoneyPrioritiesText
  {
      public static PriorityWalkResultDto Localize(IStringLocalizer, PriorityWalkResultDto walk);   // leaf label by id (unchanged); Disclaimer = L["MoneyPriorities.Result.EstimateDisclaimer"]
      public static PriorityWalkPurposeNoteDto Localize(IStringLocalizer, PriorityWalkPurposeNoteDto note); // Message by note.State: NoPurpose → "MoneyPriorities.Note.Invite", Unavailable and QuotaReached → "MoneyPriorities.Note.Unavailable", Refused → "MoneyPriorities.Note.Refused", Written → as written; Disclaimer = L["MoneyPriorities.Note.Disclaimer"]
      public static string LeafLabel(...); public static List<PriorityWalkSummaryDto> Localize(...)  // unchanged
  }
  ```
  Validator: every `.WithMessage(MoneyPrioritiesMessages.X)` gains `.WithErrorCode(ApiErrorKeys.MoneyPriorities.X)` before it. The three money range rules (`MonthlyAmountRange` ×2 rules, `DebtBalanceRange` ×2, `MonthlyExpensesRange` ×2) add `.WithState(_ => ErrorArgs.MoneyRange(0m, MoneyPrioritiesLimits.<Max>, SupportedCurrencies.Default.Code))`. Controller: `return BadRequest(validation.ToApiError());`, `ProducesResponseType(typeof(ApiErrorDto), 400)`, delete `ToErrors`. Web controller: `ValidationErrorsAsync` becomes `Errors(ApiException ex)`: `Fields(_localizer, ex)` → view model (`""` → `Errors`, others → `FieldErrors`); empty → `Errors = [L["MoneyPriorities.Message.CheckAnswers"]]`. The `request == null` branch returns the view model with `NoAnswers`.

- [ ] **Step 1: Write the failing validator test** `SubmitPriorityWalkRequestValidatorTests`:
  - `EveryFailure_HasAKeyFromApiErrorKeys`: validate `new SubmitPriorityWalkRequest()` (all null) and a request with `MonthlyAmount = 2_000_000m, DebtApr = 200m, DebtBalance = -1m` etc.; for each failure assert `ErrorCode` is one of the `ApiErrorKeys.MoneyPriorities` constants (reflection).
  - `RangeFailures_CarryMinAndMaxMoneyArgs`: `MonthlyAmount = 2_000_000m` → the failure's `CustomState` is a dictionary with `max.Value == "1000000"`, `Kind == Money`, `Currency == "USD"`.
  - `ResolvingEveryFailureInEnglish_LeavesNoPlaceholder`: for each failure, `ApiErrorMessages.Resolve(TestLocalization.Localizer, entry)` (via `ToApiError()`) contains no `{` and no `}` under `UiCultureScope("en")`.
- [ ] **Step 2: Run** `--filter SubmitPriorityWalkRequestValidatorTests` → FAIL.
- [ ] **Step 3: Add the `ApiErrorKeys.MoneyPriorities` constants, the `WithErrorCode`/`WithState` calls, and the controller change.** Delete `MoneyPrioritiesValidationErrorsDto` (build breaks in Web until step 5).
- [ ] **Step 4: Rewrite the Web tests** in `MoneyPrioritiesLocalizationTests`:
  - `Submit_WhenTheApiRejectsTheAnswers_ReturnsEachMessageInTheRequestLanguage` and `Submit_RangeMessage_FormatsItsAmountsWithTheMoneyFormatter`: the fake API now throws an `ApiException` whose body is an `ApiErrorDto` (keys from `ApiErrorKeys.MoneyPriorities`, `field` set, Money args); assert `MoneyPrioritiesErrorsViewModel` fields.
  - Add `Fields_KeepsEveryMessageOfAField_InOrder`: two entries for `MonthlyAmount` → `FieldErrors["MonthlyAmount"]` has both, in order.
  - `Submit_WhenThe400IsNotTheValidationShape_AnswersInTheRequestLanguage`: unchanged expectation (`CheckAnswers`).
  - `Note_ReturnsTheStateMessageAndDisclaimer_InTheRequestLanguage`: parametrize over `NoPurpose`, `Unavailable`, `QuotaReached`, `Refused` with the API's English `Message` set to anything (`"whatever"`) → the key's text; `Written` keeps its text.
  - `Walk_WithALeafOrDisclaimerThePageHasNoKeyFor_KeepsTheApiServiceText` → rename to `Walk_WithALeafThePageHasNoKeyFor_KeepsTheApiServiceLabel`; the disclaimer is now always the key's text.
  - Delete `EveryFixedMessage_ReadsInEnglishExactlyAsTheApiServiceWritesIt`.
- [ ] **Step 5: Implement the Web side** (view model, controller, `MoneyPrioritiesText` trimmed). Check `MoneyPrioritiesService` sets `AiAssistantMessages.PurposeNoteUnavailable` for `QuotaReached`; if it uses another constant, add `MoneyPriorities.Note.QuotaReached` to `en.json` instead of sharing `Note.Unavailable`.
- [ ] **Step 6: Run** `dotnet test PersonalFinance.sln` → PASS.
- [ ] **Step 7: Confirm `MoneyPrioritiesMessages` and `AiAssistantMessages` are still referenced only as `.WithMessage(...)` / DTO snapshot text**: `grep -rn "MoneyPrioritiesMessages\.\|AiAssistantMessages\." PersonalFinance.Web` → no hits.
- [ ] **Step 8: `npm run lint`** → clean (JS untouched; the step proves the JSON shape assumption, re-read `money-priorities.js:249-271`).
- [ ] **Step 9: Check `money-priorities.js` places messages with `textContent`, not `innerHTML`**; if `innerHTML`, change it (Review Focus 5).
- [ ] **Step 10: Commit** `feat(money-priorities): keyed validation errors; drop the English-matching table (#385)`.

---

### Task 5: Convert Community

**Files:**
- Modify: `PersonalFinance.Shared/Constants/ApiErrorKeys.cs` (`Community` members)
- Modify: `PersonalFinance.ApiService/Features/Community/ICommunityService.cs:54-59` (delete `CommunityVoteResultDto`; `VoteAsync` returns `Task<CommunityResponseDto>`)
- Modify: `PersonalFinance.ApiService/Features/Community/CommunityService.cs:47-127,130-165,204-211,243-251,273-302,348-355`
- Modify: `PersonalFinance.ApiService/Features/Community/CommunityController.cs:44-56,64-80,88-101,109-121,129-146,153-166`
- Modify: `PersonalFinance.Web/Resources/i18n/en.json` (add `Community.Error.*`, `Community.Validation.*`)
- Test: `tests/PersonalFinance.Tests/Community/CommunityServiceTests.cs` (extend the existing Community service tests; create if absent)

**Interfaces:**
- Consumes: Tasks 1–2.
- Produces `ApiErrorKeys.Community` constants and `en.json` entries (English from today's strings):
  | Const | Key | en.json | status | args |
  |---|---|---|---|---|
  | `PollCodeRequired` | `Community.Validation.PollCodeRequired` | Poll code is required. | 400 | |
  | `OptionIdRequired` | `Community.Validation.OptionIdRequired` | Option ID is required. | 400 | |
  | `PollNotFound` | `Community.Error.PollNotFound` | Poll not found or inactive. | 404 (`GetPollCommentsAsync`) / 400 (`VoteAsync`, keep today's code) | |
  | `InvalidOption` | `Community.Error.InvalidOption` | Invalid option. | 400 | |
  | `CommentEmpty` | `Community.Validation.CommentEmpty` | Comment text cannot be empty. | 400 | field `Text` |
  | `CommentTooLong` | `Community.Validation.CommentTooLong` | Comment text is too long (max {max} characters). | 400 | `max` Number 2000 |
  | `InvalidPoll` | `Community.Error.InvalidPoll` | Invalid poll. | 400 | |
  | `InvalidStory` | `Community.Error.InvalidStory` | Invalid story. | 400 | |
  | `CommentNotFound` | `Community.Error.CommentNotFound` | Comment not found. | 404 | |
  | `StoryNotFound` | `Community.Error.StoryNotFound` | Story not found. | 404 | |
  | `StoryTitle` | `Community.Validation.StoryTitle` | Title is required and must be under {max} characters. | 400 | `max` Number 500, field `Title` |
  | `StorySummary` | `Community.Validation.StorySummary` | Summary is required and must be under {max} characters. | 400 | `max` 2000, field `Summary` |
  | `StoryBodyMin` | `Community.Validation.StoryBodyMin` | Story body must be at least {min} characters. | 400 | `min` 100, field `Body` |
  | `StoryBodyMax` | `Community.Validation.StoryBodyMax` | Story body must be under {max} characters. | 400 | `max` 10000, field `Body` |
  | `StoryCategory` | `Community.Validation.StoryCategory` | Category is required and must be under {max} characters. | 400 | `max` 50, field `Category` |

  Every `throw new ArgumentException/KeyNotFoundException(...)` and every `return new CommunityVoteResultDto { Success = false, ... }` becomes `throw new ApiErrorException(key, english, field, args, status)`; the English `message` is built with the same literal number (`"… (max 2000 characters)."`). Controllers: delete every `try/catch` and the `if (!result.Success)`; `Vote` returns `Ok(await _communityService.VoteAsync(...))`. Web's `CommunityController` already sits behind `[JsonApiErrors]`, so nothing changes there.

- [ ] **Step 1: Write the failing tests** in `CommunityServiceTests` (InMemory DB as the existing Community tests do):
  - `VoteAsync_WithUnknownPoll_ThrowsPollNotFound`: `Assert.ThrowsAsync<ApiErrorException>` → `Key == ApiErrorKeys.Community.PollNotFound`.
  - `AddCommentAsync_WithLongText_ThrowsCommentTooLong_WithMaxArg`: 2001 chars → `Key == CommentTooLong`, `Field == "Text"`, `Args["max"].Value == "2000"`.
  - `SubmitStoryAsync_WithShortBody_ThrowsStoryBodyMin`.
  - `ToggleCommentLikeAsync_Unknown_Throws404`: `StatusCode == 404`.
  - `EveryCommunityKey_ResolvesInEnglish_WithoutPlaceholder`: for each `ApiErrorKeys.Community` constant, build an entry with the args the table names (a small switch in the test) and `Resolve` → no `{`.
  - Update existing Community controller/service tests that assert `ErrorMessage` or `BadRequest(string)`.
- [ ] **Step 2: Run** `--filter Community` → FAIL.
- [ ] **Step 3: Add the keys to `ApiErrorKeys` and `en.json`; convert service and controller.**
- [ ] **Step 4: Run** `dotnet test PersonalFinance.sln` → PASS.
- [ ] **Step 5: Commit** `feat(community): rejections throw ApiErrorException with keys (#385)`.

---

### Task 6: Guard tests

**Files:**
- Create: `tests/PersonalFinance.Tests/Architecture/ApiErrorKeysTests.cs`
- Create: `tests/PersonalFinance.Tests/Architecture/ApiErrorArchitectureTests.cs`

**Interfaces:**
- Consumes: `ApiErrorKeys` (reflection over nested static classes, `IsLiteral` string fields), `TestLocalization.Catalog.TryGet("en", key, out _)`, `RepositoryRoot.SourceProject("PersonalFinance.ApiService")`; the regex/`SourceFiles` style of `CurrencyFormatBanTests`.
- Produces: `private static readonly string[] ConvertedFeatures = ["MoneyPriorities", "Community"];` — E2 and E3 append to it.

- [ ] **Step 1: Write `ApiErrorKeysTests`**:
  - `EveryApiErrorKey_HasAnEnglishEntry`: all constants → `TryGet("en", …)` true; failure message lists the missing keys.
  - `EveryApiErrorKey_FollowsTheNamingRule`: value matches `^[A-Z][A-Za-z]+\.(Validation|Error)\.[A-Z][A-Za-z]+$` and starts with the nested class name.
  - `ApiErrorKeys_HasNoDuplicateValues`.
- [ ] **Step 2: Write `ApiErrorArchitectureTests`** — one `[Fact]` per rule over `Features/<Converted>/**/*.cs` (skip `bin`/`obj`):
  - no `BadRequest\(\s*"` or `BadRequest\(\s*\$"` or `BadRequest\(\s*new\s*\{` or `BadRequest\(\s*[a-zA-Z_.]+\.(Error)?Message\s*\)`;
  - no `throw new (ArgumentException|KeyNotFoundException|InvalidOperationException)\(` in a file whose name ends with `Service.cs`;
  - no `WithErrorCode\(\s*"`;
  - plus a `[Theory]` that the patterns match `BadRequest("x")`, `BadRequest(new { error = "x" })`, `BadRequest(ex.Message)`, `BadRequest(result.ErrorMessage)` and leave `BadRequest(validation.ToApiError())` alone.
- [ ] **Step 3: Run** `--filter "ApiErrorKeysTests|ApiErrorArchitectureTests"` → PASS on the first run (Tasks 4–5 already comply). If a rule fails, the offender is a missed conversion: fix the Feature, not the test.
- [ ] **Step 4: Commit** `test: guard ApiErrorKeys coverage and converted Features (#385)`.

---

### Task 7: Translations, docs, version, smoke

**Files:**
- Modify: `PersonalFinance.Web/Resources/i18n/vi.json` (generated), `i18n.lock.json` (generated)
- Modify: `docs/DIAGRAMS.md` §16 (before line 1291 `## Keeping this page current`)
- Modify: `docs/ARCHITECTURE.md:182-183` (checklist step 4, Validators)
- Modify: `PersonalFinance.Shared/Constants/AppVersion.cs`, `CHANGELOG.md`
- Modify: `e2e-tests/money-priorities-walkthrough.js`

- [ ] **Step 1: `npm run i18n:translate`** (needs the Azure Translator settings from `.env`; if missing, stop and report which keys lack `vi.json` entries: the `Errors.*` and `Community.Error/Validation.*` ones). Review the diff for placeholder damage (`{max}` must survive). Commit alone: `i18n: translate N keys (vi)`.
- [ ] **Step 2: `DIAGRAMS.md` §16**: a paragraph after the existing one ("ApiService is not localized …" → add: its rejections carry a key and typed args, Web resolves them) and a small `flowchart LR`: `Validator / Service` → `ApiErrorException | ToApiError()` → `ApiErrorExceptionFilter` → `ApiErrorDto {key, message, field, args}` → `Web ApiErrorMessages` → `IStringLocalizer + ApiErrorArgFormat` → `page / JSON {error}`. No `;` and no `call` inside the block. Render it with `mmdc` from the scratchpad to check it parses (see `docs/DIAGRAMS.md` "Keeping this page current").
- [ ] **Step 3: `ARCHITECTURE.md` step 4**: add that every rule has `.WithErrorCode(ApiErrorKeys.<Feature>.<Rule>)` with the key in `en.json`, args through `ErrorArgs`, and service rejections throw `ApiErrorException`; link ADR 0017 and the spec §4.
- [ ] **Step 4: Version and changelog**: `AppVersion` `Current = "7.14.0"`, `Minor = 14`, `Patch = 0`; `CHANGELOG.md` `## [7.14.0] - <today>` with **Added** (keyed API errors, `ApiErrorDto`, Money Priorities and Community in Vietnamese), **Changed** (`ApiErrorMessages` localized, Web error fallbacks as keys), **Removed** (`MoneyPrioritiesValidationErrorsDto`, the English-matching table).
- [ ] **Step 5: Playwright smoke** in `money-priorities-walkthrough.js`: after the existing walkthrough, switch to Vietnamese (reuse the switcher steps of `language-switch-smoke.js`), submit a monthly amount of `2000000`, assert the field error equals `vi.json["MoneyPriorities.Validation.MonthlyAmountRange"]` with `{min}`/`{max}` replaced by the `vi-VN` whole-dollar format of 0 and 1,000,000 (compute with `Intl.NumberFormat('vi-VN')` + the `$` placement `MoneyFormatter` uses; read one rendered example from the page if the format is not obvious). Run `npm run e2e:preflight`; run the walkthrough only if it passes; otherwise report the smoke as unrun and why.
- [ ] **Step 6: Final checks**: `dotnet test PersonalFinance.sln`; `npm run lint`; `dotnet build PersonalFinance.sln -c Release -p:EnforceWarningsAsErrors=true`; `git status` shows no `wwwroot/lib` line-ending noise staged (discard it if present).
- [ ] **Step 7: Commit** `docs: ADR 0017 wiring, 7.14.0 changelog, Money Priorities Vietnamese smoke (#385)`.
- [ ] **Step 8: PR** titled `Localized API errors, slice E1: contract, resolver, Money Priorities, Community (#385)`; body per the `pr` skill; mention E2/E3 follow as issues under #295.
