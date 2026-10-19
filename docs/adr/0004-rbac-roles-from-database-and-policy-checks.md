# 0004 – RBAC: ApiService Reads Roles from the Database, Code Checks Policies

**Date:** 2026-10-18  
**Status:** Accepted (amended 2026-10-18: `User` role added, shared secret for ApiService, one-time backfill)

## Context

The application needs admin-only pages and API endpoints, and must later support restrictive roles such as ReadOnly and Guest. The Web app forwards the signed-in account to ApiService through an `X-User-Id` header. ApiService can be exposed publicly (`exposePublicly` in `infra/modules/api-service.bicep`), and in that case any caller can set any header.

## Decision

1. **Roles are never forwarded in a header.** Web keeps sending only `X-User-Id`. `XUserIdAuthenticationHandler` loads the account's roles from the Identity tables (`UserManager.GetRolesAsync`) on every request, without caching, and adds them as role claims. An unknown user id is still authenticated, just with no roles.
2. **Code checks named Policies, never Role names.** Role and Policy names live in `PersonalFinance.Shared/Constants` (`AppRoles`, `AppPolicies`), and pages and endpoints use `[Authorize(Policy = ...)]`. Policies grant or deny access by role; a signed-in account with no Role is still an ordinary user (amended: a built-in `User` role now exists, see Amendments).
3. **The Admin is granted by configuration, add-only.** On startup the database seeder creates every role in `AppRoles`. It grants Admin to the account matching `Rbac:AdminEmail`, but only if that account exists and its email is confirmed. It never removes roles.
4. **Web proves itself to ApiService with a shared secret.** Web sends `X-Internal-Key` (from `Internal:ApiKey`) next to `X-User-Id`. When the secret is configured, ApiService authenticates only requests that carry it (fixed-time comparison). When it is not configured, ApiService attaches no roles outside Development, so a spoofed `X-User-Id` can never reach Admin policies.

## Considered Options

- **`X-User-Roles` header:** rejected. When ApiService is public, anyone can make themselves Admin.
- **Header guarded by a shared secret (for the roles themselves):** rejected. Roles still come from the database; the secret instead protects the identity header (decision 4, added later).
- **`[Authorize(Roles = "Admin")]` everywhere:** rejected. Restrictive roles (ReadOnly) would mean editing every controller instead of one policy definition.

## Consequences

- Role changes take effect immediately in ApiService. In the Web cookie they take effect at the next security-stamp check (up to 30 minutes). The seeder updates the security stamp when it grants Admin.
- Admin rights depend on email confirmation being trustworthy, so external logins with a typed-in email are never auto-confirmed or auto-linked.
- Non-admins get 404 for Web admin pages (the area stays hidden) and 403 ProblemDetails from ApiService.
- Production must set the same `Internal__ApiKey` on Web and ApiService, otherwise ApiService attaches no roles and admin API calls return 403.

## Amendments

- **`User` role (replaces "no User role").** Requested later: `AppRoles.User` is a built-in role seeded at startup and assigned to every new account (password and external-login registration). Policies still check named policies, never role names, so restrictive roles remain possible. A failed assignment is logged and not fatal; an Admin can assign the role in the Admin page.
- **One-time backfill.** Existing accounts get `User` only when the seeder first creates the role, using a database-side anti-join. Later startups do not re-add it, so an Admin's removal of `User` is permanent.
- **Spoofable `X-User-Id` closed.** Reading roles from the database did not authenticate the caller; the shared secret (decision 4) does. ApiService also now calls `UseAuthentication()` before `UseAuthorization()`.
- **Last-Admin protection** is serialized in-process. It is not safe across multiple ApiService instances, which is acceptable at current scale.

## Related Decisions

- [0001 – Standardize ApiService on Direct Service Injection](0001-standardize-cqrs-feature-organization.md)
