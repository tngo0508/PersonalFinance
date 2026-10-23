# 0005 – Account Purge Hard-Deletes After a 30-Day Grace Period

**Date:** 2026-10-22  
**Status:** Accepted

## Context

User-owned entities are soft-deleted and never hard-deleted (AGENTS.md, Data rules). GDPR's right to erasure, and the
Privacy Policy's promise that erasure requests are completed within 30 days by removing or anonymizing soft-deleted
data, cannot be met by soft deletion alone. Issue #65 asked for soft deletion only.

## Decision

1. **Deletion Request soft-deletes first.** Confirming a Deletion Request soft-deletes all of the Account's rows with
   one shared `DeletedAt` stamp and blocks sign-in. A cancel link in the confirmation email restores only the rows with
   that exact stamp, so data the user deleted earlier stays deleted.
2. **Purge is the one sanctioned hard delete.** A daily, idempotent `BackgroundService` in the ApiService purges
   Accounts whose Grace Period (30 days) has ended, in one transaction per Account. It hard-deletes every user-owned
   row, including rows soft-deleted earlier, `UserConsent`, `Feedback`, `AiUsageRecord` and `GoogleDriveConnection`
   (after revoking the OAuth token), and then the Identity user.
3. **Community content is anonymized, not deleted.** Stories and comments stay, with `AuthorName` set to
   "Deleted User" and the `UserId` unlinked, so threads other users replied to keep their context. Likes and poll votes
   are deleted.
4. **Only a minimal audit record survives.** A dedicated deletion audit entity records when the request was made and
   when the Purge ran, with no email or name. `UserConsent` is not reused: it records policy consent.

## Considered Options

- **Soft-delete only, rewrite the Privacy Policy:** rejected. It keeps personal financial data indefinitely and does
  not meet erasure.
- **Anonymize all rows in place, no hard delete:** rejected. Financial records stay re-identifiable through amounts,
  dates and descriptions, and every query filter would need to understand tombstoned users.
- **Immediate hard delete:** rejected. Irreversible after an accidental or malicious request.
- **Container Apps Job or Admin-triggered purge:** rejected. A Job adds infrastructure; a manual button can miss the
  30-day promise.

## Consequences

- New user-owned entities must be added to both the Deletion Request and the Purge, or erasure silently misses them.
- The Purge must be safe when several ApiService replicas run it at once.
- The last Admin cannot make a Deletion Request.

## Related Decisions

- [0004 – RBAC: ApiService Reads Roles from the Database, Code Checks Policies](0004-rbac-roles-from-database-and-policy-checks.md)
