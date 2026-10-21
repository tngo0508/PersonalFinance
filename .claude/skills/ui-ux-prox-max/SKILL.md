---
name: ui-ux-prox-max
description: Improve UI and UX quality with practical interaction, accessibility, responsive, and visual-design checks for PersonalFinanceApp.
---

# UI/UX Quality

Use this skill when reviewing or implementing user journeys, forms, dashboards, data-heavy finance screens, and
interactive feedback. Favor useful product behavior over decorative polish.

## UX checklist

- Identify the user's goal and make the primary action obvious, understandable, and available at the right point in the
  flow.
- Provide clear labels, concise instructions, predictable navigation, and actionable validation or error messages.
- Preserve entered values when validation or recoverable server errors occur, and prevent accidental duplicate
  submissions.
- Handle loading, empty, partial-data, failure, and success states honestly; never silently invent missing financial
  data.
- Use progressive disclosure for advanced projection or financial parameters while keeping assumptions visible.
- Make dense tables, filters, charts, and summaries scannable and usable on mobile and desktop.

## Accessibility and resilience

- Support keyboard-only operation, semantic structure, focus management, accessible names, contrast, and reduced motion.
- Do not communicate important information by color alone, and provide text alternatives for charts and icons.
- Treat browser validation as a usability aid only; preserve server-side validation, authorization, and antiforgery
  protections.
- For asynchronous interactions, show progress, handle failures, and prevent stale responses from overwriting newer user
  choices.

## Delivery

Inspect the complete affected journey rather than only the happy-path markup. Reuse existing UI patterns, test boundary
and failure states, and verify responsive and keyboard behavior before declaring the experience complete.