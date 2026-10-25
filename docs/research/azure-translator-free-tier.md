# Azure AI Translator F0 (free) tier: limits, auth, API shape

**Researched:** 2026-10-25 for [#336](https://github.com/tngo0508/PersonalFinance/issues/336) (part of #335, the
localization + currency spec). Sources are Microsoft Learn and the Azure pricing page only; each claim links to the
page it comes from. Learn pages carry an `ms.date` in June–August 2026, so they are current at the time of writing.

## Summary

| Question | Answer |
|---|---|
| F0 price | Free. Includes **2 million characters per month** of "any combination of standard translation and custom translation training". |
| F0 throughput cap | **2 million characters per hour**, consumed evenly (about 33,300 chars/minute sliding window). |
| One free resource? | Yes: "Only one free tier resource is available per subscription." Same features as paid tiers; does not expire. |
| Vietnamese (`vi`) | Supported for cloud text translation, Custom Translator, auto-detect, Dictionary and LLM translation. |
| Auth | Resource key (`Ocp-Apim-Subscription-Key`, plus `Ocp-Apim-Subscription-Region` for regional/multi-service), or Microsoft Entra ID / managed identity bearer token (role **Cognitive Services User**, header `Ocp-Apim-ResourceId` on the global endpoint, or the resource's custom-domain endpoint). Entra auth needs a **global** resource. |
| Batch REST shape | `POST https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from=en&to=vi`, JSON array of `{"text": "..."}`; **up to 1,000 elements and 50,000 characters per request**, counted across all `to` languages. |
| Pin "net worth" etc. | Yes, two ways: per-request **dynamic dictionary** markup (`<mstrans:dictionary translation="...">net worth</mstrans:dictionary>`, requires `from`, case-sensitive), or a Custom Translator **phrase dictionary** trained dictionary-only and selected with `category=<CategoryID>`. Neural dictionary does **not** cover Vietnamese. |
| ADR 0009 fit | The .NET SDK `Azure.AI.Translation.Text` 2.0.0 is **MIT** (depends only on `Azure.Core`, also MIT). The service itself is a metered Azure service under Microsoft's service terms, not a package; F0 costs nothing. The ADR speaks only to packages, so the localization ADR should state the service dependency explicitly. |

The ~150–200K characters/month estimated in #335 sits well inside both the 2M/month and the 2M/hour limits.

## 1. Pricing and the F0 tier

- The pricing page lists tier **F0 – Free** with "2 million characters of any combination of standard translation and
  custom translation training free per month". Features listed under F0: Text Translation, Language Detection,
  Bilingual Dictionary, Transliteration, Custom Translation Training.
  Source: <https://azure.microsoft.com/en-us/pricing/details/cognitive-services/translator/>
- The S1 pay-as-you-go dollar amounts are rendered client-side (the static page shows `$-`), so they could not be read
  here. The only visible figures are the D3 volume rows ("$40 per million characters of custom translation", "$10 per
  million source + target characters of training data (max. $300/training)"). Verify S1 in the Azure portal or the
  pricing calculator before relying on an overage number.
  Source: same pricing page.
- Custom-model hosting is a flat fee "per hosted custom translation model per region, per month", "NOT pro-rated if the
  model is hosted for less than the full month". F0 includes *training* characters in the 2M; the page does not say
  the hosting fee is waived on F0. Treat hosting a custom model as a possible monthly charge until the portal confirms
  otherwise.
  Source: pricing page FAQ, same URL.
- "Each subscription has a free tier. The free tier has the same features and functionality as the paid plans and
  doesn't expire. Only one free tier resource is available per subscription." Document translation is paid-tier only
  (not needed here).
  Source: <https://learn.microsoft.com/en-us/azure/ai-services/translator/how-to/create-translator-resource>
- Resource region: "Choose **Global** unless your business or application requires a specific region." The global
  endpoint is `api.cognitive.microsofttranslator.com`.
  Source: same create-resource page.

## 2. Service limits

Per-request limits for the `Translate` operation (from the service-limits table):

| Operation | Max size of array element | Max number of array elements | Max request size (chars) |
|---|---|---|---|
| Translate | 50,000 | 1,000 | 50,000 |

"Each translate request is limited to 50,000 characters, across all the target languages. For example, sending a
translate request of 3,000 characters to translate to three different languages results in a request size of
3,000 × 3 = 9,000 characters."

Hourly limits by tier:

| Tier | Character limit |
|---|---|
| F0 | 2 million characters per hour |
| S1 | 40 million characters per hour |

"The hourly quota should be consumed evenly throughout the hour. For example, at the F0 tier limit of 2 million
characters per hour, characters should be consumed no faster than roughly 33,300 characters per minute." Bursting a
large share of the quota in a short window returns an out-of-quota response (HTTP 429). "There are no limits on
concurrent requests." Custom models are limited to 3,600 characters per second per model. Max latency is 15 s for
standard models and 120 s for custom models.

Source: <https://learn.microsoft.com/en-us/azure/ai-services/translator/service-limits>

Billing is by character, not by request: the `X-metered-usage` response header reports the chargeable count.
Source: <https://learn.microsoft.com/en-us/azure/ai-services/translator/text-translation/reference/v3/translate>

## 3. Vietnamese support

The language-support table row for Vietnamese:

| Language | Code | Cloud text + document translation | Containers | Custom Translator | Auto detection | Dictionary | LLM translation | Adaptive custom translation |
|---|---|---|---|---|---|---|---|---|
| Vietnamese | `vi` | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | |

Vietnamese is not in the Transliteration list (irrelevant: Vietnamese is Latin-script).

Source: <https://learn.microsoft.com/en-us/azure/ai-services/translator/language-support>

## 4. Authentication options

Source for this section unless noted:
<https://learn.microsoft.com/en-us/azure/ai-services/translator/text-translation/reference/authentication>

### 4a. Resource key

| Header | When |
|---|---|
| `Ocp-Apim-Subscription-Key` | Always, when using a key. |
| `Ocp-Apim-Subscription-Region` | Required for a regional or multi-service resource; "optional when using a global translator resource". |

```bash
curl -X POST "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to=es" \
    -H "Ocp-Apim-Subscription-Key:<your-key>" \
    -H "Content-Type: application/json" \
    -d "[{'Text':'Hello, what is your name?'}]"
```

A key can also be exchanged for a 10-minute bearer token at `https://api.cognitive.microsoft.com/sts/v1.0/issueToken`
(global) or `https://<region>.api.cognitive.microsoft.com/sts/v1.0/issueToken`. This still needs the key, so it buys
nothing for a server-side job.

### 4b. Microsoft Entra ID / managed identity

"Translator v3.0 also supports authorizing access to managed identities. If a managed identity is enabled for a
translator resource, you can pass the bearer token generated by managed identity in the request header."

| Header | Value |
|---|---|
| `Authorization` | `Bearer <token>` from Entra ID (scope `https://cognitiveservices.azure.com/.default`). |
| `Ocp-Apim-ResourceId` | The Translator resource's ARM id (`/subscriptions/<id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<name>/`). Needed on the **global** endpoint. |
| `Ocp-Apim-Subscription-Region` | Region of the resource; optional for a global resource. |

Or skip `Ocp-Apim-ResourceId` by calling the resource's custom-domain endpoint:
`https://<your-custom-domain>.cognitiveservices.azure.com/translator/text/v3.0/translate?api-version=3.0&to=es`.

Requirements and caveats:

- "Assign **Cognitive Services User** role to the service principal."
- "Regional endpoints don't support Microsoft Entra authentication." Create a **single-service global** resource.
- To make Entra the sole method, set `DisableLocalAuth` to `$true` on the resource (`Set-AzCognitiveServicesAccount
  ... -DisableLocalAuth $true`).
- Entra access tokens last 60–90 minutes; the `issueToken` key-exchange token lasts 10 minutes.
- The token scope and the `Ocp-Apim-ResourceId` usage are shown in the Entra how-to.

Source for the caveats: <https://learn.microsoft.com/en-us/azure/ai-services/translator/how-to/microsoft-entra-id-auth>

### 4c. Recommendation for this repo

Translation runs as a developer/CI job that drafts JSON strings for PR review (#335), not in the deployed app.

- **Locally and in CI:** the F0 resource key in user secrets / a GitHub Actions secret, with the global endpoint and
  `Ocp-Apim-Subscription-Key`. Simplest, and the key is never shipped. `AGENTS.md` already bans hardcoding secrets.
- **If the job ever moves into the Container App (or a GitHub OIDC-federated identity):** use Entra. Grant the
  identity *Cognitive Services User* on the Translator resource and call the custom-domain endpoint with
  `DefaultAzureCredential`, then disable local auth. That matches the Bicep/managed-identity pattern the repo already
  uses for Azure and removes the key entirely.

## 5. REST shape for batch text translation

Source: <https://learn.microsoft.com/en-us/azure/ai-services/translator/text-translation/reference/v3/translate>

```http
POST https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from=en&to=vi
Ocp-Apim-Subscription-Key: <key>
Content-Type: application/json; charset=UTF-8
X-ClientTraceId: <guid>            (optional)

[
  {"text": "Net worth"},
  {"text": "Add an account"},
  {"text": "<div>Hello <b>{0}</b></div>"}
]
```

- Query parameters: `api-version=3.0` (required), `to` (required, repeatable: `to=vi&to=fr`), `from` (optional,
  auto-detect if omitted, **required when using the dynamic dictionary**), `textType=plain|html`, `category`
  (Custom Translator Category ID; default `general`), `allowFallback`, `profanityAction`, `includeAlignment`,
  `includeSentenceLength`.
- Body: a JSON array; each element has a string `text` (the docs write `Text`; the property is case-insensitive).
- Limits: 1,000 elements and 50,000 characters per request (section 2). For ~200K characters that is at least
  4 requests; batching ~100 UI strings per call keeps each request far below the cap.
- Response: an array in the same order as the input. Each element has `translations: [{ "text": ..., "to": "vi" }]`
  and, when `from` is omitted, `detectedLanguage: { language, score }`. Response headers include `X-RequestId`,
  `X-mt-system` (`Custom` or `Team`) and `X-metered-usage`.
- Status codes to handle: `400` bad parameter, `401`/`403` auth or exhausted free quota, `429` rate limit (back off,
  the hourly quota is a sliding window), `408` custom model not yet deployed, `503` retry.
- `textType=html` plus `class="notranslate"` protects markup that must survive untouched. Placeholders such as `{0}`
  are not documented as protected in plain text; wrapping them in `<span class="notranslate">` under `textType=html`,
  or in a dynamic-dictionary tag that maps the placeholder to itself, is the documented mechanism.

### .NET SDK equivalent

`Azure.AI.Translation.Text` 2.0.0 (targets `netstandard2.0`, `net8.0`, `net10.0`; sole dependency `Azure.Core`
≥ 1.57.0). SDK 1.0.0 speaks API 3.0; 2.0.0 speaks API `2026-06-06`.

```csharp
TextTranslationClient client = new TextTranslationClient(new AzureKeyCredential(apiKey), new Uri(endpoint), region);
// or, with Entra ID on a custom-domain endpoint:
TextTranslationClient client = new TextTranslationClient(new DefaultAzureCredential(), new Uri(customEndpoint));

Response<IReadOnlyList<TranslatedTextItem>> response = client.Translate("vi", inputText);
```

Sources: <https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.translation.text-readme>,
<https://www.nuget.org/packages/Azure.AI.Translation.Text>

The REST call is small enough that a Refit or `HttpClient` call (the pattern Web already uses) is a reasonable
alternative to taking the SDK; the SDK mainly adds retry policy, `DefaultAzureCredential` plumbing and typed errors.

## 6. Pinning financial terms ("net worth", "budget", ...)

Three mechanisms exist; two apply to Vietnamese.

### 6a. Dynamic dictionary (per request, no setup, works on F0)

Markup inside the text:

```html
<mstrans:dictionary translation="giá trị tài sản ròng">net worth</mstrans:dictionary>
```

- "You must include the `From` parameter in your API translation request instead of using the autodetect feature."
- "The `From` and `To` languages must include English and another supported language." (en→vi qualifies.)
- Case-sensitive, exact match; works identically with `textType=plain` and `textType=html`.
- Microsoft's own caveat: "The dynamic dictionary is safe only for compound nouns like proper names and product
  names" and "should be used sparingly", because replacing a phrase removes its context for the rest of the sentence.
  Noun phrases such as "net worth" or "emergency fund" are the intended case; verbs and adjectives are not.

Source: <https://learn.microsoft.com/en-us/azure/ai-services/translator/text-translation/how-to/use-dynamic-dictionary>
and the Translate reference above.

Fit for this repo: a small glossary file (`term → vi`) applied by the translation script before each request is the
lowest-effort, zero-cost way to pin the GLOSSARY.md vocabulary. It is also easy to test offline.

### 6b. Custom Translator phrase dictionary (trained model, `category=` parameter)

- A phrase dictionary is "a brute force 'copy and replace'": "case-sensitive. It's an exact find-and-replace
  operation. When you include a phrase dictionary in training your model, any word or phrase listed is translated in
  the way specified."
- **Dictionary-only training** needs no parallel corpus: "there's no minimum number of training sentences required.
  ... The resulting models use the Microsoft baseline models for translation with the addition of the dictionaries you
  add." Full training needs 10,000 parallel sentences.
- Dictionary files must have equal, exactly aligned source/target line counts; duplicates resolve to the last entry;
  avoid numbers-only and two/three-letter entries.
- Vietnamese has the Custom Translator tick (section 3). Custom systems are "to and from English".
- The deployed model is called with `category=<Category ID>`; `allowFallback=false` forces an error instead of the
  general model when the custom one is missing.
- Cost: training characters count against the F0 2M/month (the pricing page lists "Custom Translation Training" under
  F0). Hosting the deployed model is a flat monthly fee per region (section 1) whose waiver on F0 is **not**
  documented; expect a charge unless the portal says otherwise. Only one model can be published per project.

Sources: <https://learn.microsoft.com/en-us/azure/ai-services/translator/custom-translator/concepts/dictionaries>,
<https://learn.microsoft.com/en-us/azure/ai-services/translator/custom-translator/how-to/train-custom-model>,
<https://learn.microsoft.com/en-us/azure/ai-services/translator/custom-translator/how-to/publish-model>,
<https://learn.microsoft.com/en-us/azure/ai-services/translator/custom-translator/faq>,
<https://learn.microsoft.com/en-us/azure/ai-services/translator/custom-translator/overview>

### 6c. Neural dictionary: not for Vietnamese

The neural phrase dictionary (inflects the pinned term to fit the sentence) supports only en↔zh-Hans, fr, de, it, ja,
ko, pl, ru, es, sv. Vietnamese is absent.
Source: <https://learn.microsoft.com/en-us/azure/ai-services/translator/text-translation/how-to/use-neural-dictionary>

### Recommendation

Start with 6a (dynamic dictionary driven by a checked-in glossary). Because drafts are reviewed in PRs (#335), the
glossary can grow from review feedback. Move to 6b only if the glossary grows past what per-request markup handles
comfortably, and only after confirming the hosting fee on F0.

## 7. License policy (ADR 0009)

ADR 0009 governs **direct dependencies**: "every NuGet, npm and libman entry, and anything shipped to the browser or
deployed", allowing MIT, BSD, Apache 2.0 and ISC and banning paid, dual-licensed and copyleft packages.

- `Azure.AI.Translation.Text` 2.0.0 on NuGet declares the **MIT** license (<https://licenses.nuget.org/MIT>); the
  `Azure/azure-sdk-for-net` repository `LICENSE.txt` is "The MIT License (MIT), Copyright (c) 2015 Microsoft". Its one
  dependency, `Azure.Core`, ships from the same repository under the same license. `Azure.Identity` (needed only for
  Entra auth) also lives there. All are admissible as direct NuGet dependencies.
  Sources: <https://www.nuget.org/packages/Azure.AI.Translation.Text>,
  <https://github.com/Azure/azure-sdk-for-net/blob/main/LICENSE.txt>
- Calling the REST API directly adds no package at all.
- The Translator **service** is not a package; it is a metered Azure service accepted under the Azure service terms
  shown at resource creation ("Review the service terms, and select Create"). At F0 the usage is free; the pricing
  page lists no overage rate for F0, and the Translate reference describes a `403` once "all the free translations"
  are used, so the free tier cannot turn into a bill on its own.
  ADR 0009 does not cover cloud services, so the localization ADR from #335 should record this dependency and its
  limit explicitly, as the Research Feature does for its external data providers.
  Source: <https://learn.microsoft.com/en-us/azure/ai-services/translator/how-to/create-translator-resource>

## Open points

- S1 overage price and the custom-model hosting fee could not be read from the static pricing page; check the portal
  if either becomes relevant.
- Whether the F0 tier can *publish* (host) a Custom Translator model without charge is undocumented.
- The 2M/month quota behaviour on exhaustion is implied by the `403` description ("used all the free translations
  provided with a trial subscription"); the exact reset time (calendar month vs. rolling) is not stated.
