# 0016 – Per-Amount Currency with a Per-User Reporting Currency

**Date:** 2026-10-25  
**Status:** Accepted

## Context

Every amount in the database is a bare `decimal(18,2)` with US dollars assumed; there is no currency column and no ledger
of assets, only parameter documents (`NetWorthParameters`, `UserBudgetAllocation`, `PriorityWalk`) and line items
(`BudgetTransaction`, `MonthlyBudgetLine`, `SavingsGoal`, the spreadsheet mirrors). The owner needs USD and VND now and
plans a multi-currency calculator later, so the model must survive "I earn in VND and hold USD stocks". Decided in
Wayfinder map #335 (tickets #337, #342, #343).

## Decision

1. **Each money-bearing row carries its own ISO 4217 `CurrencyCode`** (char 3, non-null, default `USD`): document-level
   on the parameter documents, row-level on the line items, never per field. Columns stay `decimal(18,2)`; minor-unit
   rendering (VND has none) comes from a `SupportedCurrencies` constant in `Shared`, not from the culture.
2. **Each user has a Reporting Currency** (on the user preference Entity, default `USD`). Totals are converted into it;
   stored amounts are never rewritten.
3. **Conversion happens only server-side**, through one `ICurrencyConverter`, and DTOs carry both the original
   `(Amount, CurrencyCode)` and the `ReportingAmount`. Dated items convert at their own date's rate; balances at the latest.
   Same-currency items short-circuit. A missing rate is shown, never silently zero.
4. **Rates are system data**: an `ExchangeRate` table (USD base only, inverse computed) filled daily by a refresher from
   Frankfurter (primary) and fawazahmed0 currency-api (fallback), both key-free and licence-compatible with ADR 0009.

## Considered Options

- **Per-user currency only, all amounts stored as USD:** rejected. No migration of money columns, but a VND salary would be
  stored as a converted USD figure that drifts with the rate, and mixed holdings become impossible.
- **Per-field currency:** rejected. A document's fields never mix currencies; the column explosion buys nothing.
- **Currency symbol from the request culture (`"C"` format):** rejected. A Vietnamese user's USD stock holding must still
  say `$`; the culture decides separators and dates, the amount decides its symbol.
- **ECB / Fed H.10 as the rate source:** not possible; neither publishes VND. **Keyed free tiers** (exchangerate.host, Open
  Exchange Rates) rejected on non-commercial or attribution terms.
- **User-entered rates:** deferred (out of scope on the map); live rates first.

## Consequences

- Two schema migrations (preference Entity; currency columns + `ExchangeRate`), each a major version bump per `AGENTS.md`.
- Every service that sums money must sum `ReportingAmount`, and every money input gains a currency selector defaulting
  to the Reporting Currency.
- Adding a currency is a `SupportedCurrencies` entry plus the refresher quoting it; the schema does not change.
