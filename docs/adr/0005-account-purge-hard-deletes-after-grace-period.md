# 0005 – Account Purge Hard-Deletes After a 30-Day Grace Period

**Date:** 2026-10-22  
**Status:** Accepted (amended 2026-10-23: the Purge runs as a daily Container Apps Job, see Amendments)

## Context

User-owned entities are soft-deleted and never hard-deleted (AGENTS.md, Data rules). GDPR's right to erasure, and the
Privacy Policy's promise to remove or anonymize soft-deleted data after an erasure request, cannot be met by soft
deletion alone. The original #65 asked for soft deletion only.

## Decision

1. **Deletion Request and Data Export live on the Identity Manage `PersonalData` page**, which the account nav already
   links to. Password Accounts confirm a Deletion Request by re-entering their password; Accounts without a password
   (Google sign-in only) type `DELETE`. The last Admin cannot make a Deletion Request.
2. **A Deletion Request soft-deletes first.** Confirming it soft-deletes all of the Account's rows with one shared
   `DeletedAt` stamp, and locks the Identity user out (Identity lockout, which also stops external logins) until the
   Purge.
3. **Cancel is a one-time link.** The confirmation email carries a single-use cancel link, valid until the Grace Period
   ends. Using it clears the lockout and restores only the rows with that exact stamp, so data the Account deleted
   earlier stays deleted.
4. **Purge is the one sanctioned hard delete.** A daily, idempotent Container Apps Job runs the ApiService image with
   `--purge-only` and purges Accounts whose Grace Period (30 days) has ended, in one transaction per Account (amended:
   originally a `BackgroundService`). It hard-deletes every user-owned row (every `AuditableEntity` of the Account),
   including rows soft-deleted earlier, the cached Google Drive file listings, and then the Identity user with its
   roles, claims, logins and tokens.
5. **Community content is anonymized, not deleted.** Stories and comments stay, with `AuthorName` set to "Deleted User"
   and `UserId` set to a fixed placeholder id that matches no Account, so no schema change is needed and threads other
   users replied to keep their context. Likes and poll votes are deleted. Stories and comments the Account had already
   deleted itself are erased, with other people's likes on them, since nobody can see them.
6. **Only a minimal audit record survives.** A dedicated deletion record keeps when the request was made and when the
   Purge ran, with no email or name. The Purge also replaces its `UserId` with the same placeholder id as anonymized
   community content, so no row refers to the erased Account. `UserConsent` is not reused: it records policy consent.
7. **Data Export is one JSON file**, downloaded straight away, with the Account's live rows only (soft-deleted rows
   excluded). It never changes data.

## Considered Options

- **Soft-delete only, rewrite the Privacy Policy:** rejected. It keeps personal financial data indefinitely and does
  not meet erasure.
- **Anonymize all rows in place, no hard delete:** rejected. Financial records stay re-identifiable through amounts,
  dates and descriptions.
- **Immediate hard delete:** rejected. Irreversible after an accidental or malicious request.
- **`BackgroundService` in the ApiService:** first chosen, then rejected (see Amendments). The ApiService scales to zero,
  so an in-app timer runs only while traffic keeps it awake and cannot promise 31 days.
- **Admin-triggered purge:** rejected. A manual button can miss the promised timeline.

## Consequences

- New user-owned entities must be covered by the Deletion Request, the cancel restore and the Purge, or erasure
  silently misses them.
- The Purge must be safe when two runs overlap (a retried job execution, or a manual run). `DeletionRequest.Status` and the
  Identity user's concurrency stamp are concurrency tokens, so a request cancelled or purged meanwhile fails the save
  and is skipped.
- A daily runner purges up to a day after the Grace Period ends, so erasure completes within 31 days of the Deletion
  Request. The Privacy Policy's "within 30 days" becomes "within 31 days" (#144); the Grace Period stays 30 days.

## Amendments

- **Daily Container Apps Job instead of a `BackgroundService` (2026-10-23, #143).** The ApiService has
  `minReplicas: 0` and scheduled scaling is off by default, so a timer inside it does not run on days without traffic.
  `infra/modules/purge-job.bicep` runs the same image with `--purge-only` (migrate, purge, exit non-zero if any
  Account failed) on a daily cron, default 15:00 UTC, when the database is usually awake. It runs for seconds a day,
  within the Container Apps free grant.
- **No OAuth token to revoke (2026-10-23, #143).** The app stores no Google OAuth token: Google sign-in keeps none, and
  a Google Drive connection holds the Account's own API key, encrypted, which cannot be revoked from here. The Purge
  deletes it with the connection row.
- **Production now has a warm window (2026-10-24, ADR 0011).** Scheduled scaling is on in production: the ApiService
  keeps one replica from 08:00 to 22:00 Pacific and scales to zero overnight. The Purge stays a job: the window is
  deployment configuration that can be switched off, and a timer inside the ApiService would still stop overnight.

## Related Decisions

- [0004 – RBAC: ApiService Reads Roles from the Database, Code Checks Policies](0004-rbac-roles-from-database-and-policy-checks.md)
- [0011 – Production Stays Warm 08:00–22:00 Pacific, Scales to Zero Overnight](0011-warm-window-instead-of-always-on-replicas.md)
