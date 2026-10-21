---
name: design-system
description: Establish and extend reusable design tokens, components, and interaction patterns consistently across PersonalFinanceApp.
---

# Design System

Use this skill when creating or modifying shared UI patterns, theme values, component styles, layouts, or cross-feature
visual conventions.

## Before changing the system

- Inspect the shared layout, existing Bootstrap configuration, CSS organization, Razor partials, and feature-specific
  styles.
- Identify whether a token or component already exists before adding another color, spacing value, button style, card,
  table, alert, or form pattern.
- Keep the system compatible with the existing MVC/Razor rendering model and avoid introducing a framework or package
  without a clear need.

## System rules

- Prefer semantic tokens for color, spacing, typography, radii, elevation, and state instead of scattered magic values.
- Define interaction states consistently: default, hover, focus-visible, active, disabled, loading, validation, and
  error.
- Keep components composable and content-independent; do not couple reusable styles to a single controller or page.
- Maintain responsive behavior and ensure tables, forms, charts, and navigation remain usable on small screens.
- Preserve accessible contrast and provide non-color indicators for status and validation.
- Keep design-system changes backward-compatible with existing views unless a migration is intentional and complete.

## Verification

- Search for existing usages before changing shared tokens or selectors.
- Check affected views for regressions, specificity conflicts, focus visibility, and dark/light theme assumptions if
  supported.
- Remove obsolete styles and handlers introduced by a replacement; do not leave duplicate component implementations.