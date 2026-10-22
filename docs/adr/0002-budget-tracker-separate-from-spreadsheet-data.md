# 0002 – Budget Tracker Data Lives in Separate Entities, Read Through a Source-Selected Provider

**Date:** 2026-10-16  
**Status:** Accepted

## Context

Users keep their household budget in a Google Drive sheet, and the application only reads it back for analysis. The
Budget Tracker lets users enter and maintain a monthly budget inside the application. Its data must be stored in Azure
SQL, and Analysis and the Dashboard must be able to show either the synced Google Sheet data or the Budget Tracker data.

Two storage options were considered:

1. **Reuse the synced `Spreadsheet*` tables** for manual entries. Google sync rewrites snapshot rows for a file and
   month, so manual entries stored there would be overwritten by a later sync, and the sync logic would need to
   distinguish manual from synced rows.
2. **Create new, user-owned entities** for the Budget Tracker and leave the synced tables untouched.

Option 2 requires readers to know which source to query. Without a single decision point, Analysis, the Dashboard, and
any future feature would each need their own branching logic, and it would be easy to mix figures from both sources
silently.

## Decision

### 1. Separate entities for Budget Tracker data

Budget Tracker data is stored in new, user-owned entities: budget categories, monthly plans, monthly plan lines (planned
amount and optional Actual Override), budget transactions, and a per-user source preference. All inherit from
`AuditableEntity` and use soft deletes. The `Spreadsheet*` tables are neither changed nor reused, so a Google sync
cannot overwrite manual entries.

Transactions carry no month reference. A month's transactions are those whose date falls within the calendar month.

### 2. A single source-selected provider for budget reads

Analysis and the Dashboard budget views obtain month data through one provider. The provider resolves the user's data
source and delegates to one of two readers. Both readers return the same normalized shape. Each month comes from exactly
one source, and there is no per-month fallback to the other source.

Sheet months aggregate totals across snapshots for the same month, but preserve their source-record count. Trend reads
deliberately retain one observation per synced snapshot (including multiple points in one month) to preserve the
existing Analysis chart and snapshot count; Tracker trend reads return one observation per available month, and its
source-record count is one for each month with a plan or transactions. Consumers can obtain both through the provider
without reading either store directly.

The source is resolved at read time. If the user has no stored preference, the source is the Google Sheet when the user
has synced sheet data and the Budget Tracker otherwise. A preference is stored only when the user explicitly switches
the source.

### 3. Sync code reads the synced tables directly; Global Search follows the Data Source

Google sync methods continue to read and write the synced tables directly. They are not routed through the provider.

_Amended 2026-10-21 (#93):_ Global Search originally read only the synced tables, so a Budget Tracker user could never
find their transactions. It now resolves the user's Data Source with the same rule as the provider and searches only
that source's transactions. It does not read through the provider, because it needs the source decision but not month
data.

## Consequences

**Positive:**

- A Google sync cannot overwrite or corrupt manual Budget Tracker entries.
- Each view reads exactly one source, so figures are never silently mixed.
- The source decision lives in one place, so new budget consumers reuse it without duplicating branching logic.
- The synced data model and sync behavior remain as they were.

**Negative:**

- Two sets of tables hold similar budget concepts, and developers must know which set a given feature uses.
- Reads that depend on the source go through an extra abstraction.
- Moving or merging the two stores later would be a significant migration.

## Related Decisions

- [0001 – Standardize ApiService on Direct Service Injection](0001-standardize-cqrs-feature-organization.md) — the
  provider and services follow direct injection.
- GLOSSARY.md — Budget Tracker, Actual Override, Diff, and Data Source are defined there.
