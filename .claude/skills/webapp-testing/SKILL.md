---
name: webapp-testing
description: Test PersonalFinanceApp browser workflows, MVC/Razor pages, forms, authentication, responsive layouts, accessibility, charts, and client-side interactions.
---

# Web Application Testing

Use this skill when validating user-visible behavior in `PersonalFinance.Web`, especially Razor views, Identity flows,
forms, dashboards, charts, tables, and JavaScript interactions.

## Before testing

1. Inspect the affected controller, view model, Razor view, layout or partial, JavaScript, styles, routes, and existing
   automated tests.
2. Identify the user journey, required authentication and authorization state, test data needs, external-service
   dependencies, and expected loading, empty, validation, error, and success states.
3. Use the repository's existing browser-testing framework and configuration when available. Do not add a new framework
   or commit credentials merely to test a page.

## Test coverage

- Verify the primary happy path and important negative paths, including invalid input, missing records, unauthorized
  access, duplicate submissions, and failed external requests.
- Check server-rendered content, navigation, redirects, form validation, antiforgery behavior, and preservation of user
  input after recoverable errors.
- Exercise keyboard navigation, visible focus, accessible names, semantic headings, validation announcements, and
  meaningful text alternatives for charts or color-coded values.
- Check responsive behavior at desktop and narrow viewport sizes, including tables, forms, charts, navigation, long
  numbers, and long labels.
- For asynchronous interactions, verify loading and failure feedback and ensure stale responses cannot overwrite newer
  selections.
- Keep financial assertions deterministic; use explicit test data and verify displayed currency, percentages, dates,
  rounding, and assumptions without inventing missing values.

## Test isolation and safety

- Use dedicated test users and data; never use production accounts, real financial records, API keys, OAuth secrets, or
  live destructive actions.
- Prefer stable semantic selectors such as accessible roles, labels, and visible text over brittle CSS or DOM structure
  selectors.
- Mock or isolate external APIs and email delivery where the existing test architecture supports it, while retaining at
  least one meaningful integration path when appropriate.
- Do not weaken authorization, antiforgery, validation, TLS, or security checks to make browser tests pass.
- Clean up data created by tests and avoid tests that depend on execution order or shared mutable state.

## Verification and reporting

- Run the narrowest relevant browser test first, then the broader browser or solution test suite when warranted.
- Capture the exact command, target URL or route, browser/viewport assumptions, and actual result.
- When browser tooling or required services are unavailable, report the limitation clearly rather than claiming the
  workflow passed.
- Review failures as product or implementation defects first; do not hide them with skipped tests, weakened assertions,
  arbitrary waits, or fake success states.