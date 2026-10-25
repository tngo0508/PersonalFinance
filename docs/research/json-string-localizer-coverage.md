# Routing Identity and DataAnnotations messages through a custom JSON `IStringLocalizer`

**Research date:** 2026-10-25 (all versions, dates and line numbers below are as of this date)
**Ticket:** [#338](https://github.com/tngo0508/PersonalFinance/issues/338), part of the map [#335](https://github.com/tngo0508/PersonalFinance/issues/335)
**Target:** ASP.NET Core 10 (`release/10.0` branch of `dotnet/aspnetcore`), `net10.0` per `PersonalFinance/Directory.Build.props`

## Question

The map locked JSON over `.resx` with a custom `IStringLocalizer` / `IStringLocalizerFactory`. Can every built-in
message path be served from that factory, and what gaps remain (culture fallback, pluralization, `IViewLocalizer`,
`RequestLocalizationOptions`, the pinned `AppCulture`)?

## Short answer

**Yes for four of the five paths, with one path that never touches the localizer (Identity) and one that touches it
only when a key is present (DataAnnotations).** Every framework consumer resolves strings through the single
`IStringLocalizerFactory` registered in DI; replacing that registration is the whole integration. The gaps are not in
the factory but in (a) attributes without an `ErrorMessage`, (b) Identity pages still served from the default UI RCL,
(c) `IdentityErrorDescriber`, which has no localizer hook at all, and (d) pluralization, which the abstraction does not
model.

| # | Message path | Served from the custom factory? | How |
|---|---|---|---|
| 1 | Razor views: `IViewLocalizer`, `IHtmlLocalizer<T>` | **Yes** | `HtmlLocalizerFactory` wraps `IStringLocalizerFactory`; `ViewLocalizer` calls it with a base name built from the view path. |
| 2 | DataAnnotations (`[Required]`, `[StringLength]`, ...) | **Yes, when `ErrorMessage` is set** | `AddDataAnnotationsLocalization` + `DataAnnotationLocalizerProvider`. Attributes with no `ErrorMessage` bypass the localizer and use the English `System.ComponentModel.Annotations` resource. |
| 3 | Identity `IdentityErrorDescriber` | **No hook; subclass** | A plain class with 22 `virtual` methods reading an embedded English `.resx`. Subclass it, inject `IStringLocalizer`, register with `AddErrorDescriber<T>()`. |
| 4 | Model binding (`DefaultModelBindingMessageProvider`) | **Yes, via accessor delegates** | Eleven `Set*Accessor(Func<...>)` setters on `MvcOptions.ModelBindingMessageProvider`; the delegates can call the localizer at request time. |
| 5 | jQuery unobtrusive validation | **Yes, for free** | Messages are read from `data-val-*` attributes the server renders from paths 2 and 4; the script has no message table of its own. |

## Path 1: Razor views (`IViewLocalizer`, `IHtmlLocalizer<T>`)

- `AddViewLocalization()` registers `IHtmlLocalizerFactory -> HtmlLocalizerFactory` (singleton), `IHtmlLocalizer<>`
  and `IViewLocalizer -> ViewLocalizer` (transient), and calls `services.AddLocalization()` — all `TryAdd`, so an
  earlier registration of `IStringLocalizerFactory` wins.
  ([MvcLocalizationServices.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.Localization/src/MvcLocalizationServices.cs)
  lines 20-41)
- `HtmlLocalizerFactory` takes `IStringLocalizerFactory` in its constructor and both `Create` overloads delegate to
  `_factory.Create(...)`.
  ([HtmlLocalizerFactory.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.Localization/src/HtmlLocalizerFactory.cs)
  lines 14-50)
- `ViewLocalizer` resolves `viewContext.ExecutingFilePath` (or `View.Path`) and calls
  `_localizerFactory.Create(BuildBaseName(path), _applicationName)`, where the base name is the application name plus
  the view path with `/` turned into `.` (e.g. `PersonalFinance.Web.Views.Home.Index`).
  ([ViewLocalizer.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.Localization/src/ViewLocalizer.cs)
  lines 83-107)
- The docs confirm the shape: "`ViewLocalizer` implements the localizer using `IHtmlLocalizer`, so Razor doesn't
  HTML-encode the localized string" and "There's no option to use a global shared resource file" for `IViewLocalizer`;
  a shared file needs `@inject IHtmlLocalizer<SharedResource>`.
  ([Make content localizable](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/make-content-localizable?view=aspnetcore-10.0),
  "View localization")

**Consequence for the JSON factory:** `Create(string baseName, string location)` will be called with per-view base
names. The factory decides whether a per-view JSON file exists or whether every base name maps to one shared JSON
document (the map's "one format shared by Razor and JS" suggests the latter). Either way, nothing in MVC assumes
`.resx`.

## Path 2: DataAnnotations validation and display names

### Wiring

- `AddDataAnnotationsLocalization()` calls `services.AddLocalization()` (again `TryAdd`, so a custom
  `IStringLocalizerFactory` registered first is kept) and either applies the caller's `setupAction` or registers
  `MvcDataAnnotationsLocalizationOptionsSetup`.
  ([DataAnnotationsLocalizationServices.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/DataAnnotationsLocalizationServices.cs))
- The default `DataAnnotationLocalizerProvider` is
  `(modelType, stringLocalizerFactory) => stringLocalizerFactory.Create(modelType)` — one localizer per model type.
  ([MvcDataAnnotationsLocalizationOptionsSetup.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/DependencyInjection/MvcDataAnnotationsLocalizationOptionsSetup.cs))
  The property is `Func<Type, IStringLocalizerFactory, IStringLocalizer>` with no default of its own.
  ([MvcDataAnnotationsLocalizationOptions.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/MvcDataAnnotationsLocalizationOptions.cs))
- The docs show the shared-resource form:
  `options.DataAnnotationLocalizerProvider = (type, factory) => factory.Create(typeof(SharedResource));` — "With this
  approach, DataAnnotations only uses `SharedResource`, rather than the resource for each class."
  ([Make content localizable](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/make-content-localizable?view=aspnetcore-10.0),
  "How to use one resource string for multiple classes")
- Three framework components ask the provider for a localizer and pass it down: `DataAnnotationsModelValidatorProvider`
  (server-side messages), `DataAnnotationsClientModelValidatorProvider` (the `data-val-*` attributes) and
  `DataAnnotationsMetadataProvider` (`[Display(Name/Description)]` and enum member names).
  ([DataAnnotationsModelValidatorProvider.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/DataAnnotationsModelValidatorProvider.cs)
  lines 46-51;
  [DataAnnotationsClientModelValidatorProvider.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/DataAnnotationsClientModelValidatorProvider.cs)
  lines 48-55;
  [DataAnnotationsMetadataProvider.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/DataAnnotationsMetadataProvider.cs)
  lines 98-100, 179-181, 391-400)

### The key is the `ErrorMessage` text, and only when it is set

Both the server validator and the attribute adapters gate on the same four conditions:

```csharp
if (_stringLocalizer != null &&
    !string.IsNullOrEmpty(Attribute.ErrorMessage) &&
    string.IsNullOrEmpty(Attribute.ErrorMessageResourceName) &&
    Attribute.ErrorMessageResourceType == null)
{
    return _stringLocalizer[Attribute.ErrorMessage, arguments];
}
return Attribute.FormatErrorMessage(modelMetadata.GetDisplayName());
```

([ValidationAttributeAdapterOfTAttribute.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/ValidationAttributeAdapterOfTAttribute.cs)
lines 65-78; the identical guard is in
[DataAnnotationsModelValidator.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/DataAnnotationsModelValidator.cs)
lines 87-97.)

So:

- `[StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.")]` is localized:
  the English sentence is the lookup key, and `{0}`/`{1}`/`{2}` are formatted by the localizer's indexer with the
  display name and attribute arguments.
- `[Required]` with no `ErrorMessage` is **not** localized. It falls to `Attribute.FormatErrorMessage`, which reads the
  runtime's own resource: `RequiredAttribute_ValidationError = "The {0} field is required."`.
  ([Strings.resx in dotnet/runtime](https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.ComponentModel.Annotations/src/Resources/Strings.resx))
  That project ships only `Strings.resx`; there are no satellite languages, so the text is English in every culture.
- The **implicit** `[Required]` that MVC adds for non-nullable reference types is created as `new RequiredAttribute()`
  with no `ErrorMessage`, so it is also English-only unless handled.
  ([DataAnnotationsMetadataProvider.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/DataAnnotationsMetadataProvider.cs)
  lines 320-369; the docs: "The validation system treats non-nullable parameters or bound properties as if they had a
  `[Required(AllowEmptyStrings = true)]` attribute",
  [Model validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0))
- `[Display(Name = "...")]` and `[Display(Description = "...")]` are localized through the same provider when
  `ResourceType == null` ("Non-validation attributes are localized", same docs page).

**Repo inventory (2026-10-25):** in `PersonalFinance.Web` 25 validation attributes carry an `ErrorMessage`, and 7
`[Required]` attributes (Identity pages and `Models/`) do not. Those 7 plus any implicit required will stay English
until given a key.

**Closing the gap, two options.** (i) Put an `ErrorMessage` key on every attribute (mechanical, visible in code
review). (ii) Add an `IValidationMetadataProvider` that walks `context.ValidationMetadata.ValidatorMetadata` and sets
`ErrorMessage` on any `ValidationAttribute` whose `ErrorMessage`, `ErrorMessageResourceName` and
`ErrorMessageResourceType` are all empty (e.g. to a conventional key such as `Validation.Required`). `ErrorMessage` is a
public settable property on `ValidationAttribute`
([ValidationAttribute.cs](https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.ComponentModel.Annotations/src/System/ComponentModel/DataAnnotations/ValidationAttribute.cs)
line 152), and metadata providers run once per model type, so the cost is startup-only. Option (ii) also covers the
implicit required attribute.

Attributes that get a client adapter (and therefore a `data-val-*` message) in ASP.NET Core 10: `RegularExpression`,
`MaxLength`, `Required`, `Compare`, `MinLength`, `CreditCard`, `StringLength`, `Range`, `EmailAddress`, `Phone`, `Url`
and `FileExtensions` (via `DataTypeAttributeAdapter`).
([ValidationAttributeAdapterProvider.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/ValidationAttributeAdapterProvider.cs)
lines 26-64)

## Path 3: ASP.NET Core Identity `IdentityErrorDescriber`

- `IdentityErrorDescriber` is `public class IdentityErrorDescriber` with 22 `public virtual` methods
  (`DefaultError`, `PasswordTooShort(int)`, `DuplicateEmail(string)`, ...). Each returns an `IdentityError` with
  `Code = nameof(Method)` and `Description` from a static `Resources` class generated from the project's own
  `Resources.resx`. Its summary reads "Service to enable localization for application facing identity errors", but it
  takes no `IStringLocalizer`.
  ([IdentityErrorDescriber.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Identity/Extensions.Core/src/IdentityErrorDescriber.cs))
- `src/Identity/Extensions.Core/src` contains a single `Resources.resx`; there are no satellite cultures, so the
  built-in text is English only.
- Registration: `AddIdentityCore` does `services.TryAddScoped<IdentityErrorDescriber>()`, and `IdentityBuilder`
  exposes `AddErrorDescriber<TDescriber>()` which does `Services.AddScoped<IdentityErrorDescriber, TDescriber>()`.
  ([IdentityServiceCollectionExtensions.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Identity/Extensions.Core/src/IdentityServiceCollectionExtensions.cs)
  line 47;
  [IdentityBuilder.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Identity/Extensions.Core/src/IdentityBuilder.cs)
  lines 104-108)
- The official "Configure Identity" page documents options, lockout, password rules and cookies but says nothing about
  localizing errors; there is no built-in localizer path to configure.
  ([Configure ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0))

**Answer: subclass, not localizer.** `LocalizedIdentityErrorDescriber : IdentityErrorDescriber` takes
`IStringLocalizer<...>` (resolved from the custom factory), overrides each of the 22 methods, keeps `Code` unchanged and
sets `Description = localizer["Identity.PasswordTooShort", length]`. Register it with
`.AddErrorDescriber<LocalizedIdentityErrorDescriber>()` on the existing `AddDefaultIdentity<IdentityUser>(...)` chain
in `Web/Program.cs` (line 43). The describer is scoped, so `CultureInfo.CurrentUICulture` is the request culture by the
time `UserManager` calls it. `Register.cshtml.cs` already surfaces these as
`ModelState.AddModelError(string.Empty, error.Description)` (line 145), so no page change is needed for the message
text.

### The larger Identity gap: pages still served from the RCL

`AddDefaultIdentity` calls `AddDefaultUI()`, which serves every page not present in the project from the
`Microsoft.AspNetCore.Identity.UI` Razor class library (referenced in `PersonalFinance.Web.csproj` line 34).
([IdentityServiceCollectionUIExtensions.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Identity/UI/src/IdentityServiceCollectionUIExtensions.cs)
lines 53-54; docs: "Customized Identity code overrides the default implementation provided by the Identity RCL",
[Scaffold Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/scaffold-identity?view=aspnetcore-10.0))

The RCL's V5 pages carry hard-coded English in both markup and page models (e.g. `Login.cshtml.cs`:
`ModelState.AddModelError(string.Empty, "Invalid login attempt.");`, `[Display(Name = "Remember me?")]`).
([Login.cshtml.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Identity/UI/src/Areas/Identity/Pages/V5/Account/Login.cshtml.cs)
lines 75, 147)

Repo state on 2026-10-25: `Areas/Identity/Pages/Account` holds `ExternalLogin`, `Lockout`, `Login`, `Register`,
`ResendEmailConfirmation` plus the custom `CancelDeletion`/`DeletionRequested`, and `Manage/` holds
`DeletePersonalData`, `DownloadPersonalData`, `PersonalData`. The RCL still serves `AccessDenied`, `ConfirmEmail`,
`ConfirmEmailChange`, `ForgotPassword(Confirmation)`, `LoginWith2fa`, `LoginWithRecoveryCode`, `Logout`,
`RegisterConfirmation`, `ResetPassword(Confirmation)` and eleven `Manage/*` pages (`ChangePassword`, `Email`,
`EnableAuthenticator`, `ExternalLogins`, `Index`, `TwoFactorAuthentication`, ...). **Every one of those must be
scaffolded into the project before it can be localized**; the custom factory cannot reach strings compiled into the RCL.
The scaffolded page models also contain the 16 hard-coded `ModelState.AddModelError`/`StatusMessage` strings already in
the repo, which need `IStringLocalizer` injection like any other code.

## Path 4: Model binding messages (`DefaultModelBindingMessageProvider`)

- `MvcOptions.ModelBindingMessageProvider` is a `DefaultModelBindingMessageProvider` created in the `MvcOptions`
  constructor; "Changes here are copied to the `ModelMetadata.ModelBindingMessageProvider` property of all
  `ModelMetadata` instances".
  ([MvcOptions.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.Core/src/MvcOptions.cs)
  lines 47, 167-172)
- The provider has **no** `IStringLocalizer` dependency. It stores eleven `Func` accessors with setters:
  `SetMissingBindRequiredValueAccessor`, `SetMissingKeyOrValueAccessor`, `SetMissingRequestBodyRequiredValueAccessor`,
  `SetValueMustNotBeNullAccessor`, `SetAttemptedValueIsInvalidAccessor`,
  `SetNonPropertyAttemptedValueIsInvalidAccessor`, `SetUnknownValueIsInvalidAccessor`,
  `SetNonPropertyUnknownValueIsInvalidAccessor`, `SetValueIsInvalidAccessor`, `SetValueMustBeANumberAccessor`,
  `SetNonPropertyValueMustBeANumberAccessor`. Defaults come from `Mvc.Core`'s single `Resources.resx`, e.g.
  `"The value '{0}' is invalid."`, `"The field {0} must be a number."`,
  `"A value for the '{0}' parameter or property was not provided."`.
  ([DefaultModelBindingMessageProvider.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.Core/src/ModelBinding/Metadata/DefaultModelBindingMessageProvider.cs);
  [Resources.resx](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.Core/src/Resources.resx))
- The docs show the pattern:
  `options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "The field is required.");`
  ([Model validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0),
  "[Required] validation on the server")
- `NumericClientModelValidator` renders `data-val-number` from `ValueMustBeANumberAccessor(name)` at render time, so
  these accessors also feed client-side validation for numeric inputs (every `decimal` amount field).
  ([NumericClientModelValidator.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/NumericClientModelValidator.cs)
  lines 21-43)

**Answer: yes, via `IConfigureOptions<MvcOptions>`.** Register a class that takes `IStringLocalizerFactory`, creates
one localizer, and sets all eleven accessors to lambdas such as
`name => localizer["ModelBinding.ValueMustBeANumber", name]`. The lambdas run per request (binding and rendering happen
inside the request), so `CultureInfo.CurrentUICulture` is correct even though the options object is a singleton. Use
`IConfigureOptions<MvcOptions>` rather than the `AddControllersWithViews(options => ...)` callback already in
`Web/Program.cs` (line 127), because the callback has no DI access to the factory.

## Path 5: Client-side jQuery unobtrusive validation

Confirmed: the messages are server-rendered; the client library has no message catalogue of its own.

- Docs: "Tag Helpers and HTML helpers use the validation attributes and type metadata from model properties to render
  HTML 5 `data-` attributes for the form elements that need validation. jQuery Unobtrusive Validation parses the
  `data-` attributes and passes the logic to jQuery Validation, effectively 'copying' the server-side validation logic
  to the client." and "The `data-val-required` attribute contains an error message to display if the user doesn't fill
  in the release date field."
  ([Model validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0),
  "Client-side validation")
- Source: each adapter does `MergeAttribute(context.Attributes, "data-val-required", GetErrorMessage(context))`, the
  same `GetErrorMessage` as path 2.
  ([RequiredAttributeAdapter.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/RequiredAttributeAdapter.cs)
  lines 30-31;
  [StringLengthAttributeAdapter.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.DataAnnotations/src/StringLengthAttributeAdapter.cs)
  lines 28-38)
- `jquery.validate.unobtrusive.js` (Apache-2.0, .NET Foundation) `parseElement` reads
  `message = $element.attr("data-val-" + adapterName)` and `setValidationValues` copies it to
  `options.messages[ruleName]`; it parses `[data-val=true]` elements on DOM ready.
  ([jquery.validate.unobtrusive.js](https://github.com/aspnet/jquery-validation-unobtrusive/blob/main/src/jquery.validate.unobtrusive.js))

Two residual client-side concerns, neither about the localizer:

1. **Dynamically inserted forms** need `$.validator.unobtrusive.parse(form)`; otherwise the messages rendered into
   them are never read ([Model validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0),
   "Add Validation to Dynamic Forms"). The repo includes `_ValidationScriptsPartial` in 6 views.
2. **Number and date parsing** in jQuery Validation is en-US. The docs warn: "To support jQuery validation for
   non-English locales that use a comma (",") for a decimal point, and non US-English date formats, you must take steps
   to globalize your app." ([Make content localizable](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/make-content-localizable?view=aspnetcore-10.0),
   note under "Configure localization services"). Vietnamese uses `,` as the decimal separator, so the `number` and
   `range` client rules need a culture-aware override if `SupportedCultures` (not just UI cultures) ever includes
   `vi-VN`. The upstream `jquery-validation` project (MIT) ships `localization/messages_vi.js` on cdnjs for
   `jquery-validate@1.21.0`, the version pinned in `libman.json`; it only translates the *built-in* jQuery Validation
   messages, which unobtrusive overrides with `data-val-*` anyway, so it is not needed for message text.

## Culture fallback chain (`vi-VN -> vi -> en`)

Two separate mechanisms, and the custom factory owns the second one.

### 1. Request culture selection (middleware, built in)

`RequestLocalizationMiddleware` resolves the provider's culture name against `SupportedUICultures`; when
`FallBackToParentUICultures` is `true` (the default) and the name is not supported, it retries with
`CultureInfo.GetCultureInfo(name).Parent.Name`, up to `MaxCultureFallbackDepth = 5`. It then sets
`CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture` for the request.
([RequestLocalizationMiddleware.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Middleware/Localization/src/RequestLocalizationMiddleware.cs)
lines 20, 82-87, 151-172, 123-126;
[RequestLocalizationOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.requestlocalizationoptions?view=aspnetcore-10.0):
"`FallBackToParentUICultures` ... Defaults to `true`")

`CultureInfo.Parent`: "the parent of a specific culture is a neutral culture, the parent of a neutral culture is the
`InvariantCulture`, and the parent of the `InvariantCulture` is the invariant culture itself."
([CultureInfo.Parent](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.parent?view=net-10.0))
So a browser sending `vi-VN` with `SupportedUICultures = [en, vi]` lands on `vi` without any custom code.

### 2. Resource lookup (the localizer, custom)

The interface is tiny: two indexers and `GetAllStrings(bool includeParentCultures)`. There is no culture parameter and
no fallback contract; `WithCulture` is gone from the .NET 10 abstraction.
([IStringLocalizer](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.localization.istringlocalizer?view=net-10.0-pp);
[IStringLocalizer.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Localization/Abstractions/src/IStringLocalizer.cs))

The built-in `ResourceManagerStringLocalizer` therefore implements fallback itself: it reads
`CultureInfo.CurrentUICulture`, delegates to `ResourceManager.GetString(name, culture)` (which walks the parent chain),
walks `culture.Parent` until `culture == culture.Parent` for `GetAllStrings`, and returns a `LocalizedString` with
`ResourceNotFound = true` and the key as the value when nothing matches.
([ResourceManagerStringLocalizer.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Localization/Localization/src/ResourceManagerStringLocalizer.cs))
The docs describe the same chain for `.resx`: `Welcome.fr-CA.resx`, then `Welcome.fr.resx`, then `Welcome.resx`.
([Provide resources](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/provide-resources?view=aspnetcore-10.0),
"Culture fallback behavior")

**A custom JSON factory must do the same.** The lookup for key `k` under `CultureInfo.CurrentUICulture = c` is:

1. `c.Name` file/section (`vi-VN`), 2. `c.Parent.Name` (`vi`), 3. repeat until `Parent == self` (invariant),
4. the English baseline (`en`), 5. return `new LocalizedString(k, k, resourceNotFound: true)`.

Step 4 is the map's "fall back to English at runtime" and is not the invariant culture: either store the English
strings under the invariant/neutral slot or hard-code `en` as the terminal culture in the factory. Set
`ResourceNotFound` honestly at step 5 so the CI "missing-key report" can be produced from the same code path
(`GetAllStrings(includeParentCultures: false)` per culture, diffed against `en`). Also honour `SearchedLocation` so log
lines say which file was consulted.

Note on `includeParentCultures`: `GetAllStrings(true)` must union keys from the whole parent chain and return each
key's *resolved* value, matching the built-in behaviour; `GetAllStrings(false)` returns only the exact culture's keys.
The missing-key report wants the `false` variant.

## Pluralization

- Neither `IStringLocalizer` nor the ASP.NET Core localization docs offer plural forms; the only abstraction is
  `string.Format`-style arguments through the indexer.
  ([IStringLocalizer](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.localization.istringlocalizer?view=net-10.0-pp);
  [.NET Localization](https://learn.microsoft.com/en-us/dotnet/core/extensions/localization))
- CLDR 47 cardinal rules: **English** has `one` (`i = 1 and v = 0`) and `other`; **Vietnamese has only `other`**
  (minimal pair "15 ngày" / "1,5 ngày").
  ([CLDR Language Plural Rules](https://www.unicode.org/cldr/charts/47/supplemental/language_plural_rules.html))

For en + vi the problem is therefore small: English needs two forms, Vietnamese one. Options:

| Option | Licence | Shape | Notes |
|---|---|---|---|
| Key-per-form convention in the JSON (`"Items.one"`, `"Items.other"`) with a ~20-line `Plural(count, key)` helper that picks `one` when `count == 1` and the UI culture is `en`, else `other` | n/a | In-house | Enough for two languages; a third language with `few`/`many` needs a rule table. |
| **OrchardCore.Localization.Abstractions / .Core** | BSD-3-Clause ([repo licence](https://github.com/OrchardCMS/OrchardCore/blob/main/LICENSE)) | `localizer.Plural(count, singular, plural, args)` and `Plural(count, string[] forms, args)` extensions, `IPluralStringLocalizer`, `IPluralRuleProvider` with CLDR rules per culture | Allowed by ADR 0009. Heavy (`OrchardCore.Localization.Core 2.1.5` on NuGet, net8.0, depends on `ZString`), and its localizer is PO-file based, so only the abstractions and rule provider would be reused. ([StringLocalizerExtensions.cs](https://github.com/OrchardCMS/OrchardCore/blob/main/src/OrchardCore/OrchardCore.Localization.Abstractions/Extensions/StringLocalizerExtensions.cs) lines 19-34) |
| **SmartFormat.NET** | MIT (NuGet `licenseExpression`) | `string.Format` replacement with `{0:plural:one item\|many items}` and a localization extension; v3.6.2 released 2026-10-25 ([releases](https://github.com/axuno/SmartFormat/releases)) | General-purpose formatter; would wrap the values *inside* the JSON strings. Adds a second template syntax next to `{0}` placeholders. |
| **Humanizer.Core** | MIT (NuGet `licenseExpression`), 3.0.10 published 2026-03-07 | `ToQuantity`, `Pluralize` with per-culture satellite packages | English-centric inflection; no Vietnamese rules of use here. Not a fit for message plurals. |

Recommendation for the spec: the key-per-form convention, implemented in the same in-house localizer class, with the
CLDR category names as suffixes so a later language can add `few`/`many` without renaming keys. Revisit Orchard's
`IPluralRuleProvider` only if a third language with more than two categories is added.

## What must change in the current setup

### `Web/Program.cs` lines 115-125 (request localization)

Current code pins everything to en-US so that "a browser Accept-Language header cannot change currency or number
formats". The map now wants the **language** to follow the user while the **reporting currency** is a separate user
setting, so the two culture axes must be split:

- `SupportedUICultures = [en, vi]` (language of the strings); `DefaultRequestCulture = new RequestCulture(culture:
  "en-US", uiCulture: "en")`.
- `SupportedCultures` decides formatting of numbers, dates and model binding. Keeping it `[en-US]` leaves
  `decimal` parsing and `ToString("C")` exactly as today and avoids the jQuery-validation comma issue; adding `vi-VN`
  later is a separate decision tied to the currency ticket. The docs explain the split: "`SupportedCultures` determines
  the results of culture-dependent functions, such as date, time, number, and currency formatting ... `SupportedUICultures`
  determines which translated strings ... are looked up".
  ([Provide resources](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/provide-resources?view=aspnetcore-10.0))
- Provider order. The defaults are `QueryString`, `Cookie`, `Accept-Language`
  ([RequestLocalizationOptions.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Middleware/Localization/src/RequestLocalizationOptions.cs)
  lines 22-26). The map says language is a *user setting*, so add a custom provider first
  (`options.AddInitialRequestCultureProvider(new CustomRequestCultureProvider(...))` reading the signed-in user's
  preference) and keep `CookieRequestCultureProvider` (`.AspNetCore.Culture`, value `c=en-US|uic=vi`) for anonymous
  visitors and the landing page. Whether `AcceptLanguageHeaderRequestCultureProvider` stays is a product decision (the
  map lists "language detection for anonymous visitors" as unspecified); with it removed, unauthenticated requests get
  `DefaultRequestCulture`.
  ([Select language and culture](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/select-language-culture?view=aspnetcore-10.0))
- `app.UseRequestLocalization()` already sits before `UseRouting`/`UseAuthentication` (line 151). A provider that
  reads the user's stored preference needs the authenticated principal, which is not available at that point: either
  read the preference from a claim set at sign-in, or from the culture cookie written when the user saves the setting
  (the cookie is the simpler and cacheable option). Moving the middleware after `UseAuthentication` is possible but
  then `UseRouting` runs first; the docs only require it before "any middleware that might check the request culture".
- Optionally `ApplyCurrentCultureToResponseHeaders = true` to emit `Content-Language` (useful for `hreflang`/SEO
  work listed in the map).

### `Shared/Constants/AppCulture.cs` (process culture)

`ApplyDefault()` sets `DefaultThreadCurrentCulture`, `DefaultThreadCurrentUICulture`, `CurrentCulture` and
`CurrentUICulture` to en-US and is called at startup of both Web and ApiService. **It stays.** The middleware overrides
`CurrentCulture`/`CurrentUICulture` per request, so `ApplyDefault` now defines the culture of everything *outside* a
request: background services, Serilog formatting (documented in `ServiceDefaults/LoggingExtensions.cs`), and the
ApiService, which has no request-localization middleware. Two consequences to write into the spec:

1. The XML doc comment and the `Program.cs` comment ("Lock request localization to en-US so a browser Accept-Language
   header cannot change currency or number formats") must be rewritten: the process default is en-US, the UI culture is
   per request.
2. Anything localized **outside a request** (emails sent from a background job, the Research refresher, scheduled
   notifications) has `CurrentUICulture = en-US` and must set the culture explicitly from the recipient's stored
   preference, e.g. by giving the in-house localizer a `WithCulture(CultureInfo)`-style method of its own (the
   interface no longer has one) or by wrapping the call in a `CultureInfo.CurrentUICulture` swap.
   `EmailTemplateHelper` (used by `Register`, `ExternalLogin`, `ResendEmailConfirmation`) runs inside requests today, so
   it is fine, but the deletion/purge notifications (ADR 0005) run from a job.

The ICU warning in `AppCulture` still applies: `vi`/`vi-VN` resolve only with ICU present in the runtime image
(`InvariantGlobalization` must stay off), and `CultureInfo.GetCultureInfo("vi")` silently returns an invariant-like
culture otherwise. The Dockerfile uses parameterised `RUNTIME_IMAGE`; the spec should state the ICU requirement
explicitly.

### Service registration (new)

```text
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();   // before AddMvc/AddLocalization
builder.Services.AddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));   // reuse the built-in generic wrapper
builder.Services.AddControllersWithViews(...).AddViewLocalization().AddDataAnnotationsLocalization(o =>
    o.DataAnnotationLocalizerProvider = (type, f) => f.Create(typeof(SharedResource)));
builder.Services.AddDefaultIdentity<IdentityUser>(...).AddErrorDescriber<LocalizedIdentityErrorDescriber>();
builder.Services.ConfigureOptions<LocalizedModelBindingMessages>();                      // IConfigureOptions<MvcOptions>
```

`AddLocalization` (called internally by `AddViewLocalization` and `AddDataAnnotationsLocalization`) uses
`TryAddSingleton<IStringLocalizerFactory, ResourceManagerStringLocalizerFactory>()`, so the custom singleton must be
registered **first** or it is silently shadowed.
([LocalizationServiceCollectionExtensions.cs](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Localization/Localization/src/LocalizationServiceCollectionExtensions.cs)
lines 57-58) The built-in `StringLocalizer<T>` just forwards to `factory.Create(typeof(T))`, so it can be reused as
is.

## Package versus in-house

| Candidate | Licence | State on 2026-10-25 | Fit |
|---|---|---|---|
| **My.Extensions.Localization.Json** (Hisham Bin Ateya) | MIT (NuGet `licenseExpression`) | 4.1.0 published 2026-06-04, targets `net10.0`, depends on `Microsoft.Extensions.Localization 10.0.0`; repo last push 2026-09-21, 122 stars | Closest fit. `AddJsonLocalization(o => { o.ResourcesPath = ["Resources"]; o.ResourcesType = ResourcesType.TypeBased \| CultureBased; })`. `JsonResourceManager.GetString` walks `culture.Parent` while `FallBackToParentUICultures` is true; `JsonStringLocalizer` mirrors `ResourceManagerStringLocalizer` (`GetAllStrings` over the culture hierarchy, `ResourceNotFound`, logging, optional throw on missing). Culture-based mode (`vi.json`, `en.json`) is one file per language, which is what the map's "one format shared by Razor and JS" wants. ([README](https://github.com/hishamco/My.Extensions.Localization.Json), [JsonResourceManager.cs](https://github.com/hishamco/My.Extensions.Localization.Json/blob/main/src/My.Extensions.Localization.Json/Internal/JsonResourceManager.cs) lines 174-199) |
| Askmethat.Aspnet.JsonLocalizer | MIT (repo) | **Archived** (last push 2023-10-23), last release 4.0.1 on 2022-07-15 | Do not adopt. |
| OrchardCore.Localization.Core | BSD-3-Clause | 2.1.5, net8.0 | PO-file based; only interesting for plural abstractions (see above). |
| In-house (~150 lines) | n/a | — | `JsonStringLocalizerFactory` (singleton; loads `wwwroot/i18n/{culture}.json` or `Resources/*.json` once, `IOptionsMonitor`/file watcher optional), `JsonStringLocalizer` (parent-chain lookup, `en` terminal fallback, `ResourceNotFound`, `GetAllStrings`), plus the `Plural` helper. Nothing in the five paths needs more than `Create(Type)`, `Create(string, string)` and the three interface members. |

**Trade-off.** The hishamco package gives a tested parent-culture walk, logging and the two naming modes for one
MIT dependency, and its author co-wrote the official localization docs. Against it: it is a single-maintainer project,
its "missing key" behaviour (log or throw) does not produce the CI report the map wants, it reads `.json` files from
disk by convention (so the JS side still needs its own loader), and the flat-key JSON it expects would have to be the
same document the browser fetches. The in-house version is small because the hard parts (culture selection, parent
chain, attribute plumbing) are all framework-side; what it buys is ownership of the key schema, the English terminal
fallback, the plural suffix convention and the missing-key report, all of which the spec needs anyway. Either choice is
licence-clean under ADR 0009.

## Gaps summary (for the spec)

1. **Attributes without `ErrorMessage`** (7 `[Required]` in the repo plus implicit required on non-nullable
   properties) bypass the localizer. Fix with keys on every attribute or an `IValidationMetadataProvider` that
   assigns default keys.
2. **Identity pages served from the RCL** (about 11 Account + 11 Manage pages) cannot be localized until scaffolded;
   the scaffolded code then needs `IStringLocalizer` for its 16+ hard-coded `ModelState`/`StatusMessage` strings.
3. **`IdentityErrorDescriber`** has no localizer hook: subclass all 22 methods and register with
   `AddErrorDescriber<T>()`.
4. **Model binding messages** need an `IConfigureOptions<MvcOptions>` that sets all eleven accessors; they also feed
   `data-val-number` on every decimal input.
5. **Pluralization** is absent from the abstraction; en needs `one`/`other`, vi only `other`. Key-suffix convention
   recommended.
6. **Culture fallback** is two-layered: the middleware handles `vi-VN -> vi` for *selection*; the factory must handle
   `vi-VN -> vi -> en` for *lookup* and report `ResourceNotFound` for the CI missing-key report.
7. **Out-of-request code** (background jobs, ApiService) keeps `CurrentUICulture = en-US` from `AppCulture`; emails
   from jobs need an explicit culture.
8. **jQuery Validation number parsing** is en-US; only relevant if `SupportedCultures` (formatting) ever includes
   `vi-VN`, which this ticket recommends deferring to the currency work.
9. **`SupportedCultures` vs `SupportedUICultures`** must be configured separately; the current block in
   `Web/Program.cs` lines 115-125 and the comments in `AppCulture.cs` need rewriting.

## Sources

Primary sources consulted on 2026-10-25:

- learn.microsoft.com: [Make content localizable](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/make-content-localizable?view=aspnetcore-10.0),
  [Provide resources](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/provide-resources?view=aspnetcore-10.0),
  [Select language and culture](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/select-language-culture?view=aspnetcore-10.0),
  [Model validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0),
  [Configure Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0),
  [Scaffold Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/scaffold-identity?view=aspnetcore-10.0),
  [.NET Localization](https://learn.microsoft.com/en-us/dotnet/core/extensions/localization),
  [RequestLocalizationOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.requestlocalizationoptions?view=aspnetcore-10.0),
  [IStringLocalizer](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.localization.istringlocalizer?view=net-10.0-pp),
  [CultureInfo.Parent](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.parent?view=net-10.0).
- `dotnet/aspnetcore` `release/10.0`: the files linked inline under `src/Mvc/Mvc.DataAnnotations`,
  `src/Mvc/Mvc.Localization`, `src/Mvc/Mvc.Core`, `src/Identity/Extensions.Core`, `src/Identity/UI`,
  `src/Localization`, `src/Middleware/Localization`.
- `dotnet/runtime` `release/10.0`: `System.ComponentModel.Annotations` `Strings.resx` and `ValidationAttribute.cs`.
- `aspnet/jquery-validation-unobtrusive` `main`: `src/jquery.validate.unobtrusive.js`.
- Unicode CLDR 47 [Language Plural Rules](https://www.unicode.org/cldr/charts/47/supplemental/language_plural_rules.html).
- NuGet registration metadata (api.nuget.org) and GitHub repository metadata for `My.Extensions.Localization.Json`,
  `Askmethat.Aspnet.JsonLocalizer`, `OrchardCore.Localization.Core`, `SmartFormat.NET`, `Humanizer.Core`; cdnjs
  file listing for `jquery-validate@1.21.0`.
- This repository at commit `59d6ced`: `Web/Program.cs`, `Shared/Constants/AppCulture.cs`, `Web/libman.json`,
  `Web/Areas/Identity/Pages/**`, `Web/Views/Shared/_ValidationScriptsPartial.cshtml`, `ServiceDefaults/LoggingExtensions.cs`.
