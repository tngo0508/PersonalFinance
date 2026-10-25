# IRS retirement contribution limits, from the official source

Research for issue #299 (child of map #297, Money Priorities). All sources are irs.gov pages, read on 2026-10-24.
Every figure below was checked against **Notice 2025-67** (the authoritative document) and the IRS pages that
restate it; where a page gives a different presentation (for example a total instead of a catch-up) that is noted.

## TL;DR

- **The current tax year is 2026.** Its figures come from Notice 2025-67, announced in news release IR-2025-111 on
  **2025-11-13**: 401(k)/403(b)/457(b)/TSP deferral **$24,500**; age-50 catch-up **$8,000**; age 60–63 catch-up
  **$11,250**; IRA **$7,500** with a **$1,100** catch-up; 415(c) total **$72,000**; Roth IRA phase-out
  **$153,000–$168,000** (single/head of household) and **$242,000–$252,000** (married filing jointly).
- **The 2027 figures are not yet announced** as of 2026-10-24. The IRS newsroom for October 2026 lists releases up to
  IR-2026-128 (Oct. 23) and none is the annual limits release; the COLA table stops at 2026 and no Notice 2026-xx on
  limits exists. Everything circulating about 2027 ($25,500 and so on) is a third-party projection and must not be
  used.
- **Announcement timing:** the release lands between late October and mid-November for the following calendar year
  (2022-10-21, 2023-11-01, 2024-11-01, 2025-11-13). The limits take effect January 1.
- **No machine-readable source exists.** The IRS publishes HTML pages and a PDF notice only: no JSON, XML, CSV or API,
  and no RSS for the limits. The nearest "feed" is the GovDelivery e-mail subscription for Employee Plans news.
- **Recommendation:** hold the figures as a versioned, per-year C# constant table in `PersonalFinance.Shared`
  (keyed by tax year, with the source URL and notice number in the record), pick the row by the current tax year with
  a fallback to the latest row plus a "figures for YYYY" label, and refresh with a small yearly PR each November. Do
  not scrape irs.gov. Details in section 5.

## 1. Figures for tax year 2026 (current)

Source of record: Notice 2025-67, "2026 Amounts Relating to Retirement Plans and IRAs, as Adjusted for Changes in
Cost-of-Living", https://www.irs.gov/pub/irs-drop/n-25-67.pdf (published in IRB 2025-49, p. 761). The notice opens
"Effective January 1, 2026" and the amounts are expressed as changes from 2025.

| Item | Code section | 2026 | 2025 | Notice 2025-67 wording |
|---|---|---|---|---|
| 401(k), 403(b), governmental 457(b), TSP employee deferral | 402(g)(1); 457(e)(15) | **$24,500** | $23,500 | "increased from $23,500 to $24,500" |
| Catch-up, age 50 and over (401(k)/403(b)/457(b)/TSP) | 414(v)(2)(B)(i) | **$8,000** | $7,500 | "increased from $7,500 to $8,000" |
| Higher catch-up, individuals who attain age 60, 61, 62 or 63 in the year (SECURE 2.0) | 414(v)(2)(E)(i) | **$11,250** | $11,250 | "remains $11,250" |
| Total DC annual additions (employee + employer), excluding catch-up | 415(c)(1)(A) | **$72,000** | $70,000 | "increased in 2026 from $70,000 to $72,000" |
| Annual compensation limit | 401(a)(17) | $360,000 | $350,000 | "increased from $350,000 to $360,000" |
| IRA contribution limit (traditional + Roth combined) | 219(b)(5)(A) | **$7,500** | $7,000 | "increased from $7,000 to $7,500" |
| IRA catch-up, age 50 and over | 219(b)(5)(B)(ii) | **$1,100** | $1,000 | "increased from $1,000 to $1,100" |
| Roth catch-up wage threshold (prior-year FICA wages above this force Roth catch-ups) | 414(v)(7)(A) | $150,000 | $145,000 | "increased from $145,000 to $150,000" |
| SIMPLE deferral | 408(p)(2)(E)(i)(III) | $17,000 | $16,500 | "increased from $16,500 to $17,000" |
| SIMPLE catch-up 50+ / 60–63 | 414(v)(2)(B)(ii) / (E)(ii) | $4,000 / $5,250 | $3,500 / $5,250 | |

Derived totals (the IRS pages state these too):

- Age 50–59 or 64+: deferral + catch-up = **$32,500** (IR-2025-111: "the catch-up ... is $8,000 ... can generally
  contribute up to $32,500").
- Age 60–63: deferral + higher catch-up = **$35,750**. The higher catch-up **replaces** the $8,000, it is not added to
  it (IR-2025-111: "this higher catch-up contribution limit remains $11,250 instead of the $8,000 noted above").
- 415(c) including catch-up: **$80,000** (50+) or **$83,250** (60–63) (Retirement Topics 401(k) page: "$72,000 in
  2026 ($80,000 including catch-up contributions or up to $83,250 for those age 60 to 63)").
- IRA, age 50+: **$8,600** (IRA limits page: "$7,500 ($8,600 if you're age 50 or older)").

### Roth IRA MAGI phase-out ranges, 2026

Notice 2025-67, section 408A(c)(3): "the adjusted gross income phase-out range for taxpayers making contributions to
a Roth IRA is between $242,000 and $252,000 for married couples filing jointly ... For singles and heads of household,
the income phase-out range is between $153,000 and $168,000 ... For a married individual filing a separate return, the
phase-out range is not subject to an annual cost-of-living adjustment and remains between $0 and $10,000."

| Filing status | 2026 phase-out (MAGI) | 2025 |
|---|---|---|
| Single, head of household, or married filing separately and did not live with spouse | **$153,000 – $168,000** | $150,000 – $165,000 |
| Married filing jointly, or qualifying surviving spouse | **$242,000 – $252,000** | $236,000 – $246,000 |
| Married filing separately and lived with spouse at any time in the year | **$0 – $10,000** (never indexed) | $0 – $10,000 |

Below the range the full $7,500 (or $8,600) is allowed; inside the range it is reduced pro rata (Publication 590-A
worksheet); at or above the top it is $0.

For completeness, the **traditional IRA deduction** phase-outs for active plan participants (same notice): single/HoH
$81,000–$91,000; MFJ, contributing spouse covered $129,000–$149,000; not covered but spouse covered
$242,000–$252,000; MFS $0–$10,000. The Priority Tree does not need these, but the Roth and "spouse covered" ranges are
identical and easy to confuse.

### Which year applies

A limit applies to the **calendar/tax year in which the contribution is made** (plan deferrals) or **for which it is
designated** (IRA contributions can be made until the un-extended filing deadline, April 15 of the following year).
On 2026-10-24 the figures above are the ones in force. The age tests are "attains age 50 before the close of the
taxable year" and "attains age 60, 61, 62 or 63 in [the year]": a person who turns 64 during the year drops back to
the $8,000 catch-up.

## 2. Tax year 2027: not announced

Checked on 2026-10-24:

- `https://www.irs.gov/newsroom/news-releases-for-current-month` covers October 2026 with IR-2026-117 (Oct. 1)
  through IR-2026-128 (Oct. 23). None concerns contribution limits. (IR-2026-126, Oct. 22, is about the EPCRS
  correction program, not limits.)
- `https://www.irs.gov/newsroom/news-releases-for-november-2026` does not exist yet (404).
- The COLA table (section 3) has columns 2026, 2025, 2024, 2023 only, "Page Last Reviewed or Updated: 24-Aug-2026".
- "Inflation-adjusted tax items by tax year" lists 2026 as the latest year, "Page Last Reviewed or Updated:
  01-Dec-2025".
- A general web search returns only third-party **projections** for 2027 (mostly $25,500 / $8,500 / $11,750 / $75,000,
  based on the Milliman forecast). These are not IRS figures and are out of scope by the ticket's terms.

When the IRS releases the 2027 figures they will appear as a news release titled "401(k) limit increases to $X for
2027, IRA limit ..." plus a notice numbered **Notice 2026-NN** at `https://www.irs.gov/pub/irs-drop/n-26-NN.pdf`.

## 3. When the IRS announces each year's figures

| Tax year | News release | Date | Notice | Days before Jan 1 |
|---|---|---|---|---|
| 2023 | IR-2022-188 | 2022-10-21 | Notice 2022-55 | 72 |
| 2024 | IR-2023-203 | 2023-11-01 | Notice 2023-75 | 61 |
| 2025 | IR-2024-285 | 2024-11-01 | Notice 2024-80 | 61 |
| 2026 | IR-2025-111 | 2025-11-13 | Notice 2025-67 | 49 |
| 2027 | not yet | — | — | — |

The amounts are indexed under section 415(d) "under adjustment procedures similar to those used to adjust benefit
amounts under section 215(i)(2)(A) of the Social Security Act", i.e. they depend on the Q3 CPI-U, which is why the
release follows the September CPI print and the SSA COLA announcement (mid-October) and lands in late October or
November. Figures take effect January 1. Expect the 2027 release in the **last week of October or November 2026**.

## 4. Is there a machine-readable source?

**No.** Everything the IRS publishes on this subject is HTML or PDF:

| Source | Format | Notes |
|---|---|---|
| Notice (e.g. Notice 2025-67) | PDF | Authoritative; prose paragraphs, no table |
| Annual news release (IR-2025-111) | HTML | Prose; the most readable restatement; includes Roth and deduction phase-outs and Saver's Credit |
| "COLA increases for dollar limitations on benefits and contributions" | HTML table | The only tabular presentation (4 years × ~30 rows); lacks Roth phase-outs; the 60–63 catch-up is in a footnote |
| "Retirement topics - 401(k) and profit-sharing plan contribution limits" | HTML | Current-year prose with 415(c) totals |
| "Retirement topics - IRA contribution limits" | HTML | Totals by year back to 2023 |
| "Retirement topics - Catch-up contributions" | HTML | Current year only |
| "Amount of Roth IRA contributions that you can make for YYYY" | HTML table | **Exists only for 2023 and 2024** as of today; the Roth IRAs hub links no later year. Use the notice or release for 2026. |
| Publication 590-A | HTML/PDF | Annual; the worksheet for the reduced Roth contribution |
| Inflation-adjusted tax items by tax year | HTML index | Links the release per year; no data files |
| Employee Plans news | HTML + GovDelivery e-mail | `https://service.govdelivery.com/service/multi_subscribe.html?code=USIRS`; no RSS for limits |

No irs.gov endpoint returns JSON/XML/CSV for these amounts, there is no IRS API, and the page HTML has no stable
machine-readable markup (no `<table>` ids, no schema.org data). The release URL slug embeds the dollar amount, so it
cannot even be predicted. Scraping is therefore brittle and unwarranted for a handful of numbers that change once a
year; it would also sit badly with ADR 0012 (official sources, but the limits are not "data" the app displays live).

## 5. Recommendation for the app

1. **Hold the figures as code, per tax year.** A record type in `PersonalFinance.Shared/Constants/`, e.g.
   `RetirementContributionLimits` with `TaxYear`, `EmployeeDeferral`, `CatchUp50`, `CatchUp60To63`,
   `TotalAnnualAdditions415c`, `IraLimit`, `IraCatchUp`, `RothPhaseOutSingleStart/End`, `RothPhaseOutJointStart/End`,
   `RothPhaseOutSeparateStart/End`, `SourceNotice` ("Notice 2025-67"), `SourceUrl`, `AnnouncedOn`. Money as
   `decimal`. A static table holds one row per year (2025 and 2026 now), so a Walk saved in 2026 and viewed in 2027
   still shows the figures the user answered against (the Walk already stores a tree version; store `LimitsTaxYear`
   alongside, or make the tree version include it).
2. **Select by tax year.** `ForYear(int year)` returns the row for the current year, falling back to the **latest
   known row** when the new year's figures have not been entered yet, and the page says "2026 IRS limits" explicitly
   so a January gap is visible, not silent. A unit test asserts the table has a row for `DateTime.UtcNow.Year` or
   the preceding year (so CI starts failing in the first week of February if nobody updated it; a test on the current
   year alone would fail on January 1 before anyone can act).
3. **Refresh ritual, once a year in November.** A small PR (patch version, Changed: "IRS 2027 limits") driven by the
   release: read Notice 2026-NN, add a row, update the test's expected year, cite the notice URL in the row. Add a
   calendar reminder / a GitHub Issue template "Update IRS limits for YYYY" and optionally subscribe the owner to the
   GovDelivery Employee Plans e-mail. No scraping, no scheduled job, no database table: the figures are law-like
   constants, not user data, and a wrong value silently fetched is worse than a stale one shown with its year.
4. **State the figures on the page, do not compute eligibility.** The Priority Tree asks "are you at the limit?" and
   shows the deferral limit, the catch-up for the user's age band, and the IRA limit with its catch-up. The Roth
   phase-out ranges are only needed if the tree later asks "Roth or traditional"; even then show the range and let
   the user answer, since the app does not know MAGI or filing status. Mention the 60–63 rule only when the user's
   age (if asked) is in that band.
5. **Disclaimer text** next to the figures: "IRS limits for tax year 2026 (Notice 2025-67). Plan rules may be lower."
   This also covers employer plans that do not adopt the 60–63 catch-up (it is optional for plans).

## Sources (all read 2026-10-24)

- Notice 2025-67 (PDF): https://www.irs.gov/pub/irs-drop/n-25-67.pdf
- IR-2025-111, "401(k) limit increases to $24,500 for 2026, IRA limit increases to $7,500", Nov. 13, 2025:
  https://www.irs.gov/newsroom/401k-limit-increases-to-24500-for-2026-ira-limit-increases-to-7500
- COLA increases for dollar limitations on benefits and contributions (updated 24-Aug-2026):
  https://www.irs.gov/retirement-plans/cola-increases-for-dollar-limitations-on-benefits-and-contributions
- Retirement topics - 401(k) and profit-sharing plan contribution limits (updated 08-Apr-2026):
  https://www.irs.gov/retirement-plans/plan-participant-employee/retirement-topics-401k-and-profit-sharing-plan-contribution-limits
- Retirement topics - IRA contribution limits (updated 23-Sep-2026):
  https://www.irs.gov/retirement-plans/plan-participant-employee/retirement-topics-ira-contribution-limits
- Retirement topics - Catch-up contributions (updated 07-May-2026):
  https://www.irs.gov/retirement-plans/plan-participant-employee/retirement-topics-catch-up-contributions
- Roth IRAs hub (links the "Amount of Roth IRA contributions" pages for 2023 and 2024 only):
  https://www.irs.gov/retirement-plans/roth-iras
- Amount of Roth IRA contributions that you can make for 2024 (latest year with its own page):
  https://www.irs.gov/retirement-plans/plan-participant-employee/amount-of-roth-ira-contributions-that-you-can-make-for-2024
- Inflation-adjusted tax items by tax year (updated 01-Dec-2025):
  https://www.irs.gov/newsroom/inflation-adjusted-tax-items-by-tax-year
- News releases for the current month (October 2026, through IR-2026-128):
  https://www.irs.gov/newsroom/news-releases-for-current-month
- Prior-year releases for the timing table: IR-2024-285 (Nov. 1, 2024)
  https://www.irs.gov/newsroom/401k-limit-increases-to-23500-for-2025-ira-limit-remains-7000; IR-2023-203
  (Nov. 1, 2023) https://www.irs.gov/newsroom/401k-limit-increases-to-23000-for-2024-ira-limit-rises-to-7000;
  IR-2022-188 (Oct. 21, 2022) https://www.irs.gov/newsroom/401k-limit-increases-to-22500-for-2023-ira-limit-rises-to-6500
- Employee Plans news (GovDelivery subscription): https://www.irs.gov/retirement-plans/employee-plans-news
