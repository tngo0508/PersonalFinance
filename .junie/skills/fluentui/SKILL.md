---
name: fluentui
description: Use Fluent UI principles and components appropriately when building accessible, consistent interfaces for PersonalFinanceApp.
---

# Fluent UI

Use this skill when the task explicitly requests Fluent UI or when evaluating Fluent-inspired component patterns for the
web frontend.

## Integration guidance

- First inspect the existing Bootstrap, Razor, CSS, and JavaScript stack. Do not add Fluent UI packages or replace the
  current component system without explicit justification.
- Select components by user intent and preserve semantic HTML, accessible names, keyboard behavior, focus indicators,
  and responsive layout.
- Keep Fluent UI usage compatible with server-rendered MVC/Razor views; isolate component-specific assets and avoid
  coupling backend projects to UI libraries.
- Prefer established project patterns for dialogs, buttons, forms, tables, menus, notifications, and design tokens
  before creating custom equivalents.
- Do not mix competing visual languages on the same workflow without a deliberate migration plan.

## Accessibility and security

- Use visible labels, correct input types, validation summaries, and appropriate ARIA only where native semantics are
  insufficient.
- Preserve antiforgery tokens, server-side validation, authorization, output encoding, and safe DOM updates.
- Ensure status colors have text or icon alternatives and that interactive controls work without a mouse.

## Verification

- Check component behavior at narrow widths, with keyboard navigation, during loading and error states, and with long
  financial values or labels.
- Confirm the required Fluent UI dependency is compatible with the target framework and existing build; do not claim
  adoption unless it is actually wired and verified.