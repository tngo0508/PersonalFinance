---
name: design
description: Plan and implement cohesive, accessible visual design changes for PersonalFinanceApp's MVC and Razor UI.
---

# Product and Visual Design

Use this skill when shaping user-facing pages, layouts, forms, dashboards, navigation, or other visual experiences in
`PersonalFinance.Web`.

## Design principles

- Inspect existing layouts, views, partials, CSS, and JavaScript before introducing a new visual pattern.
- Preserve the application's financial-product context: clarity, trust, readable numbers, and calm visual hierarchy take
  priority over decoration.
- Define the page purpose, primary user action, information hierarchy, responsive behavior, loading state, empty state,
  error state, and success state before implementation.
- Reuse existing Bootstrap components and shared partials where they provide the right behavior; avoid one-off patterns
  that fragment the UI.
- Keep financial values, dates, percentages, and explanatory assumptions legible and unambiguous.

## Accessibility and safety

- Design for keyboard navigation, visible focus, semantic headings, accessible labels, sufficient contrast, and
  reduced-motion preferences.
- Do not rely on color alone to communicate financial status, validation, or chart meaning.
- Preserve server-side validation, antiforgery protection, output encoding, and authorization while changing
  presentation.
- Never use fake records, misleading success states, or fabricated financial results to make a design appear complete.

## Implementation workflow

1. Inspect the relevant Razor view, layout, stylesheet, scripts, view model, and neighboring feature patterns.
2. Make the smallest coherent change in `PersonalFinance.Web` and keep markup, styles, and behavior understandable.
3. Verify desktop and narrow-screen layouts, keyboard interaction, validation/error states, and consistency with
   existing navigation.
4. Run focused tests or build checks when behavior or Razor compilation changes; documentation-only design guidance
   needs no application test.