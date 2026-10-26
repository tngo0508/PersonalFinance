# 0017 – API Errors Carry Localization Keys, Web Translates Them

**Date:** 2026-10-25  
**Status:** Accepted

## Context

ADR 0015 puts the localizer and every translation in Web, and the localization spec stores Notifications as a key plus
arguments (§2.3). Neither says anything about the text ApiService puts in a 4xx response: FluentValidation messages in
each Feature's `Validators/`, ad-hoc `BadRequest("…")` strings, and `ArgumentException` messages thrown by services and
rewrapped by controllers. That text reaches Vietnamese users in English. ApiService returns it in five body shapes (a
bare string, `{ error }`, `{ errors: [string] }`, `{ errors: [{ ErrorCode, ErrorMessage }] }`, a Feature DTO) and Web
flattens them to one string in `ApiErrorMessages`. Two pages work around it by matching the English text and swapping in
a translation (`MoneyPrioritiesText.Messages`), which breaks silently when the English changes. Web's own fallbacks
(`JsonApiErrorsFilter.UnavailableMessage` and the rest) are English constants too. Raised in #385 (part of #295).

## Decision

1. **Every rejection ApiService returns is one shape**, `ApiErrorDto` in `Shared/Contracts`: a list of errors, each with
   a localization `Key`, an English `Message`, an optional `Field` and typed named `Args`. Keys are constants in
   `Shared/Constants/ApiErrorKeys.cs` and are keys of Web's `en.json` (`<Feature>.Validation.<Rule>` for validators,
   `<Feature>.Error.<Rule>` for other rejections).
2. **The shape is produced in one place.** A service or controller that rejects a request throws `ApiErrorException`
   (key, English, field, args); one exception filter in `ApiService/Infrastructure/` renders it as the fitting 4xx.
   Validators set `.WithErrorCode(ApiErrorKeys.…)` and one extension turns a `ValidationResult` into the same shape.
   Controllers stop catching and rewrapping.
3. **ApiService stays language-free.** It never reads the request language and holds no translation. Its English
   `Message` is for logs, tests and Swagger; what a user sees comes from `en.json` / `vi.json`.
4. **`en.json` is the single source of display text.** A test asserts that every `ApiErrorKeys` constant has an
   `en.json` entry, so the English `Message` is never the text shown for a converted Feature. A body without keys (a
   Feature not yet converted) keeps today's best-effort parsing; ASP.NET's own model-binding 400 (`ValidationProblemDetails`
   from `[ApiController]`) is shown as one generic localized message, never parsed for text.
5. **Web translates at the edge**, in `ApiErrorMessages`, through `IStringLocalizer`. Arguments are typed `Text`,
   `Number` or `Money` (with its currency code), sent in the invariant culture and formatted by Web in the request
   culture, money through `MoneyFormatter` (ADR 0016). Web's own error messages are `Errors.*` keys, and JavaScript
   receives the already-translated text from Web's JSON error responses.
6. **The contract is not versioned.** Web is ApiService's only caller and both ship in the same release (ADR 0006).
7. **Scope is every Feature whose pages are localized.** The Admin area is not localized and keeps English errors.

## Considered Options

- **ApiService localizes, given the language by Web:** rejected. It needs a second localizer over the same JSON files or
  a second copy of the strings, breaking ADR 0015's one home for translations, and it would have to format amounts in a
  culture it otherwise never sees.
- **Validators in `Shared`, run by Web before the call:** rejected. No contract change, but validation moves into Web
  (against the thin-controller rule and ADR 0007's Feature folders) and it does nothing for rejections raised inside a
  service, so the contract is needed anyway.
- **Keys only, no English `Message`:** rejected. The English costs a few bytes, keeps logs and ApiService tests readable,
  and matches how Notifications store an English snapshot.
- **Matching the English text in Web (today's per-page shims):** rejected. Silent breakage on any wording change,
  repeated per Feature.
- **The Notification argument shape (`Dictionary<string, string>`):** rejected for errors. Untyped strings cannot tell
  Web which value is an amount to format, or in which currency.
- **Converting every `BadRequest` site in place:** rejected. Forty-two sites in five shapes; one exception filter and one
  validator extension replace them.

## Consequences

- Every Feature's validators and rejections gain a key and matching `en.json` / `vi.json` entries, converted in three
  releases (see the spec).
- `MoneyPrioritiesText`'s English-matching table is deleted. Id-keyed lookups (leaf labels, Community stats and
  categories) are unaffected: they were never English matching.
- `ApiErrorMessages` keeps the repo's helper pattern (static, `IStringLocalizer` parameter, like `NotificationText`).
- An architecture test fails when a converted Feature returns a `BadRequest` or throws a rejection without a key, and
  when `.WithErrorCode` is given a literal instead of an `ApiErrorKeys` constant.
