---
name: financial-security
description: Safely change authentication, credentials, financial calculations, persistence, or user-specific data in PersonalFinanceApp. Use whenever a change can expose secrets, alter money values, or affect authorization.
---

# Financial Data and Security

Treat all credentials, financial records, and user identifiers as sensitive.

## Secrets and credentials

- Never commit API keys, passwords, OAuth secrets, access tokens, or connection strings containing secrets.
- Use .NET User Secrets for local development and environment/container secret stores for deployed environments.
- Preserve `CredentialProtector` and existing encryption boundaries; do not log plaintext credentials or decrypted
  financial data.
- Do not weaken authentication, email confirmation, authorization, antiforgery, TLS, or cookie protections to make a
  test or integration pass.
- Validate ownership and authorization server-side; hiding a UI control is not authorization.

## Financial and database correctness

- Use `decimal` with explicit precision for money; never use `float` or `double` for financial amounts.
- Keep authoritative calculations server-side and document assumptions such as contribution timing, inflation, return
  rates, taxes, and rounding.
- Test zero, negative values where permitted, rounding, maximum/minimum values, invalid date ranges, and missing data.
- Use EF Core async operations, `AsNoTracking()` for read-only queries, parameterized queries, database-side filtering,
  and deterministic ordering.
- Inspect existing provider-specific migrations before changing the model. Do not reset databases, drop tables, truncate
  data, or apply production migrations without explicit authorization.
- Keep user-specific queries scoped to the authenticated user and avoid leaking records through error messages, logs,
  DTOs, or views.

## HTTP and UI protections

- Use strongly typed request/view models and prevent over-posting.
- Preserve the established antiforgery strategy on state-changing MVC actions.
- Encode untrusted output; use `textContent` rather than unsafe HTML insertion in JavaScript.
- Keep browser validation as usability support only; repeat security and business validation on the server.

When changing security-sensitive behavior, add a focused regression test for the failure path as well as the successful
path.