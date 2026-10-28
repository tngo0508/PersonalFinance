# 0003 – NetWorth Reset and Randomize are Frontend-Only Exploration Tools

**Date:** 2026-10-18  
**Status:** Accepted

## Context

The Net Worth projection page allows users to adjust financial parameters (starting balance, annual savings, ROI assumptions, etc.) and see how their net worth grows over time under different scenarios. The page provides three action buttons: Save, Randomize, and Reset.

Prior to this change, the **Reset** button performed two actions:
1. Reset the frontend sliders to hardcoded default values.
2. Submitted a form that called `DeleteParameters`, soft-deleting the user's saved parameters in the database.

This behavior was surprising and destructive — users expected Reset to simply undo their current exploration, not destroy their saved data. The **Randomize** button already worked correctly as a frontend-only tool, generating random scenarios without affecting the database.

From the user's perspective, Reset and Randomize are **exploration tools** — they let you play with parameters to see different outcomes. Neither should affect persisted records.

## Decision

### 1. Reset is frontend-only

Reset restores the user's last saved parameters by reading from a JavaScript cache populated on page load. No API call or page reload is required. If no saved parameters exist (e.g., the user has never saved or previously cleared them), Reset falls back to hardcoded defaults with a brief message: "No saved parameters, using defaults."

### 2. Reset is a dropdown button with two actions

The Reset button is a Bootstrap dropdown with two options:
- **"Reset to saved"** (primary) — restores cached saved values instantly.
- **"Clear saved parameters"** (secondary, destructive, red text) — shows a confirmation dialog and, if confirmed, submits the existing `DeleteParameters` endpoint to soft-delete the saved record.

The button icon retains the undo arrow with a chevron indicator to signal the dropdown menu.

### 3. Randomize remains unchanged

Randomize was already frontend-only and continues to be. No changes were made to its behavior.

### 4. JavaScript cache eliminates the need for new API endpoints

On page load, the initial slider values (which reflect saved parameters from the database or defaults) are captured into a JavaScript object. Reset reads from this cache, avoiding any round-trip to the server. The existing `DeleteParameters` endpoint is reused for the explicit Clear action.

### 5. Confirmation dialog protects against accidental deletion

The Clear action shows a browser `confirm()` dialog with a detailed warning: "This will delete your saved net worth parameters. Your current slider values will be reset to defaults. This action cannot be undone."

## Consequences

**Positive:**
- Users can freely explore scenarios without fear of losing saved data.
- Reset is instant — no page reload or API call needed.
- Destructive actions require explicit intent through a separate, confirmed action.
- The change is minimal and localized to the view and JavaScript layer.

**Negative:**
- The JavaScript cache reflects the values at page load. If a user saves new parameters elsewhere (e.g., in another tab), the cache won't reflect the change until the page is reloaded.

## Update — 2026-10-27: Randomize moved into the More menu

Reduce Clutter (#407, slice #453) moved **Randomize** out of the action bar into a **More (⋯)** menu next to Reset, together with "Explore market research" and, once the user has saved parameters, the five presets. Save stays the only primary action. Randomize behaves exactly as described above: it is still frontend-only and never touches saved records. Reset and its dropdown are unchanged.

## Related Decisions

- [0001 – Standardize ApiService on Direct Service Injection](0001-standardize-cqrs-feature-organization.md) — the Web layer calls Refit APIs; this change keeps the Web layer simple with no new API contracts.
- GitHub Issue #39 — the spec that drove this decision.
