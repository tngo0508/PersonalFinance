---
name: ui-styleing
description: Apply maintainable, responsive, and accessible UI styling to PersonalFinanceApp Razor views and frontend assets.
---

# UI Styling

Use this skill for CSS, Razor markup, responsive layout, typography, visual states, and frontend presentation changes.
The skill name intentionally follows the repository configuration requested by the project owner.

## Styling rules

- Follow the existing stylesheet organization, Bootstrap conventions, naming patterns, and four-space JavaScript style
  guidance.
- Prefer classes and reusable selectors over inline styles, excessive specificity, and element selectors that can leak
  across features.
- Use semantic HTML and Tag Helpers; keep presentation separate from controller and service logic.
- Use fluid layouts, sensible breakpoints, readable line lengths, and touch-friendly controls rather than fixed
  desktop-only dimensions.
- Preserve visible focus, keyboard access, contrast, form labels, validation summaries, and meaningful responsive table
  behavior.
- Treat user content as untrusted: do not add unsafe HTML insertion or bypass Razor encoding for styling convenience.

## Interaction states

- Style loading, empty, validation, error, success, disabled, and authorization states explicitly where a feature needs
  them.
- Ensure charts and color-coded financial indicators have text or visual alternatives.
- Avoid animation that blocks tasks or causes motion problems; respect reduced-motion preferences.

## Validation

Inspect neighboring pages after styling changes for cascade regressions, then verify narrow-screen rendering, keyboard
focus, and form error presentation. Run the relevant build or tests when markup or behavior is affected.