# 0004 – RBAC: ApiService Reads Roles from the Database, Code Checks Policies

**Date:** 2026-10-18  
**Status:** Accepted

## Context

The application needs admin-only pages and API endpoints, and must later support restrictive roles such as ReadOnly and Guest. The Web app forwards the signed-in account to ApiService through an `X-User-Id` header. ApiService can be exposed publicly (`exposePublicly` in `infra/modules/api-service.bicep`), and in that case any caller can set any header.

## Decision

1. **Roles are never forwarded in a header.** Web keeps sending only `X-User-Id`. `XUserIdAuthenticationHandler` loads the account's roles from the Identity tables (`UserManager.GetRolesAsync`) on every request, without caching, and adds them as role claims. An unknown user id is still authenticated, just with no roles.
2. **Code checks named Policies, never Role names.** Role and Policy names live in `PersonalFinance.Shared/Constants` (`AppRoles`, `AppPolicies`), and pages and endpoints use `[Authorize(Policy = ...)]`. Signed-in accounts with no Role are ordinary users, so there is no "User" role.
3. **The Admin is granted by configuration, add-only.** On startup the database seeder creates every role in `AppRoles`. It grants Admin to the account matching `Rbac:AdminEmail`, but only if that account exists and its email is confirmed. It never removes roles.

## Considered Options

- **`X-User-Roles` header:** rejected. When ApiService is public, anyone can make themselves Admin.
- **Header guarded by a shared secret:** rejected. Adds a secret to manage, while a DB lookup costs one indexed query.
- **`[Authorize(Roles = "Admin")]` everywhere:** rejected. Restrictive roles (ReadOnly) would mean editing every controller instead of one policy definition.

## Consequences

- Role changes take effect immediately in ApiService. In the Web cookie they take effect at the next security-stamp check (up to 30 minutes). The seeder updates the security stamp when it grants Admin.
- Admin rights depend on email confirmation being trustworthy, so external logins with a typed-in email are never auto-confirmed or auto-linked.
- Non-admins get 404 for Web admin pages (the area stays hidden) and 403 ProblemDetails from ApiService.

## Related Decisions

- [0001 – Standardize ApiService on Direct Service Injection](0001-standardize-cqrs-feature-organization.md)
