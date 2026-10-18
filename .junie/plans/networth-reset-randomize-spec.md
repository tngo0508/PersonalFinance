# Problem Statement

When a user clicks the **Reset** button on the Net Worth projection page, the system performs a soft delete of their saved parameters in the database. This is inconvenient and annoying — users expect Reset to simply undo their current exploration, not destroy their saved data. The **Randomize** button already works correctly as a frontend-only tool, but Reset destroys persisted records.

From the user's perspective:
- Reset and Randomize are **exploration tools** — they let you play with parameters to see different scenarios.
- Neither should affect the saved records already persisted in the database.
- The current behavior of soft-deleting on Reset is surprising and destructive.

# Solution

Make Reset a frontend-only exploration tool, identical in data-safety to Randomize. Reset will restore the user's last saved parameters (or fall back to defaults if none are saved). The soft-delete functionality will be preserved as a separate, explicit **"Clear saved parameters"** action with a confirmation dialog.

# User Stories

1. As a user exploring net worth scenarios, I want Reset to restore my last saved values so that I can quickly undo my exploration without losing my saved data.
2. As a user who has never saved parameters, I want Reset to fall back to default values with a brief message so that I understand what happened.
3. As a user who wants to clear my saved parameters, I want an explicit "Clear saved" action with a confirmation dialog so that I don't accidentally delete my data.
4. As a user randomizing scenarios, I want Randomize to remain frontend-only so that my saved data is never affected.
5. As a user, I want the Reset button to visually indicate it has a dropdown menu so that I can discover both "Reset to saved" and "Clear saved" actions.
6. As a user, I want the confirmation dialog for clearing saved parameters to clearly explain what will be deleted so that I make an informed decision.
7. As a user exploring presets, I want the Quick Presets (FIRE, Coast FI, Traditional, etc.) to work without affecting my saved data.
8. As a user, I want the Reset button icon to include a chevron indicator so that I know it opens a dropdown menu.
9. As a user, I want my saved parameters to be cached in JavaScript on page load so that Reset works instantly without a page reload.
10. As a user, I want the charts and projection table to update immediately after Reset so that I can see the restored scenario.

# Implementation Decisions

- **Reset restores saved values from JavaScript cache**: On page load, the initial saved/default values are cached in JavaScript. Reset restores from this cache without any API call or page reload.
- **Reset is a dropdown button**: The Reset button becomes a Bootstrap dropdown with two actions:
  - **"Reset to saved"** (primary) — restores cached saved values.
  - **"Clear saved parameters"** (secondary, destructive, red text) — triggers soft delete via existing `DeleteParameters` API.
- **Confirmation dialog for Clear**: A browser `confirm()` dialog warns: "This will delete your saved net worth parameters. Your current slider values will be reset to defaults. This action cannot be undone."
- **Fallback to defaults**: If no saved parameters exist, Reset falls back to hardcoded defaults and shows a brief message: "No saved parameters, using defaults."
- **Randomize remains unchanged**: Already frontend-only; no changes needed.
- **No new API endpoint**: The JavaScript cache eliminates the need for a new endpoint. The existing `DeleteParameters` endpoint is reused for the Clear action.
- **Icon change**: The Reset button icon will include a small chevron/caret to indicate the dropdown menu, alongside the existing undo arrow.
- **ADR documentation**: An Architecture Decision Record will be created in `docs/adr/` to document why Reset and Randomize are frontend-only exploration tools.

# Testing Decisions

- **Good test definition**: Test external behavior — the user's ability to reset, randomize, and clear — not internal implementation details like the JavaScript cache structure.
- **Modules to test**:
  - JavaScript: Verify that Reset restores cached values without API calls.
  - JavaScript: Verify that Clear triggers the DeleteParameters API and redirects.
  - Controller: Verify that DeleteParameters still works correctly for the Clear action.
  - View: Verify that the dropdown renders correctly with both actions.
- **Prior art**: The existing `DashboardServiceTests.cs` in the test project uses in-memory database tests. JavaScript tests would follow the existing pattern of testing external behavior.
- **Key test scenarios**:
  - Reset when saved parameters exist: values restore from cache, no API call.
  - Reset when no saved parameters exist: values restore to defaults, message shown.
  - Clear saved parameters: confirmation dialog appears, soft delete occurs, redirect to Index.
  - Randomize: no database impact, frontend-only.
  - Presets: no database impact, frontend-only.

# Out of Scope

- Adding a "Reset to hardcoded defaults" as a separate action — the primary Reset behavior is "restore saved" with fallback to defaults.
- Full modal dialog for Clear confirmation — a standard `confirm()` dialog is sufficient.
- New API endpoints — the existing `DeleteParameters` endpoint is reused.
- Changing the Save button behavior — Save continues to persist parameters to the database.

# Further Notes

- The existing `DeleteParameters` controller action and API endpoint will be preserved for the Clear action.
- The JavaScript cache approach is the simplest implementation — the values are already rendered into the page on initial load.
- This is a meaningful UX decision that future contributors should understand, hence the ADR documentation.
- The changelog will be updated under `[Unreleased]` with a Fixed entry describing the change.
- Version will be bumped (patch) after implementation.
