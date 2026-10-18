---
sessionId: session-261017-142018-hpf4
---

# Requirements

### Overview
The Reset button on the Net Worth page currently performs a soft delete of saved parameters in the database when clicked. This is dangerous — users can accidentally wipe their saved data. The Reset button should only reset the frontend UI sliders to default values so users can experiment with parameters freely, without affecting any saved data.

### Current Behavior
1. User clicks "Reset" button
2. JavaScript calls `resetToDefaults()` to reset slider values
3. JavaScript then submits a form that posts to `DeleteParameters` action
4. The Web controller calls the API to soft-delete saved parameters in the database
5. Page redirects to Index, loading default values

### Desired Behavior
1. User clicks "Reset" button
2. JavaScript calls `resetToDefaults()` to reset slider values and recalculate projections
3. No server request is made; saved data in the database remains intact
4. User can restore saved data by refreshing the page

### Scope
- **In Scope**: Change Reset button to client-side-only operation (reset sliders, recalculate, no server round-trip)
- **Out of Scope**: Removing the `DeleteParameters` endpoint and controller action (keep them for potential future use), adding a "Clear Saved Data" feature, ADR or glossary updates (this change is too minor)

# Technical Design

### Current Implementation
- **Razor View** (`_NetWorthContent.cshtml` lines 298-303): Reset button is a `<form>` element that posts to `DeleteParameters` action with `id="nwResetForm"`
- **JavaScript** (`networth.js` lines 44-51): Click handler prevents default, calls `NW.resetToDefaults()`, then submits `nwResetForm`
- **Web Controller** (`NetworthController.cs` lines 152-166): `DeleteParameters()` action calls `_netWorthApi.DeleteParametersAsync()` and redirects to Index
- **API Controller** (`NetWorthController.cs` lines 83-90): `DELETE /api/networth/parameters` endpoint calls `_parametersService.DeleteAsync()`

### Proposed Changes

**File: `networth.js`** (lines 44-51)
- Remove the line `document.getElementById('nwResetForm').submit()` from the reset button click handler
- The `NW.resetToDefaults()` call remains, which already resets all sliders and triggers recalculation via AJAX

**File: `_NetWorthContent.cshtml`** (lines 298-303)
- Replace the `<form>` wrapper around the Reset button with a simple `<button>` element
- Change the button to `type="button"` to prevent any form submission behavior
- Add an `id="nwResetBtn"` for consistent JavaScript targeting
- Update the JavaScript selector in `networth.js` from `#nwResetForm button[type="submit"]` to `#nwResetBtn`

### Key Decisions
- Keep `DeleteParameters` endpoint and controller action intact (don't delete them) — they may be useful for a future explicit "Clear Saved Data" feature
- No confirmation dialog needed — Reset is now a harmless client-side operation
- No ADR needed — this is a simple behavioral fix, not an architectural decision

# Delivery Steps

###   Step 1: Update JavaScript reset handler
Remove the form submission from the reset button click handler in `networth.js` so Reset becomes a client-side-only operation.
- Open `PersonalFinance.Web/wwwroot/js/networth/networth.js`
- In the reset button click handler (lines 44-51), remove the line `document.getElementById('nwResetForm').submit()`
- Update the selector from `#nwResetForm button[type="submit"]` to `#nwResetBtn`
- The `NW.resetToDefaults()` call remains, which already resets all sliders and triggers recalculation via AJAX

###   Step 2: Update Razor view and finalize
Replace the form-wrapped Reset button with a plain button element in `_NetWorthContent.cshtml`, then update version and changelog.
- Open `PersonalFinance.Web/Views/Networth/_NetWorthContent.cshtml` (lines 298-303)
- Replace the `<form asp-action="DeleteParameters" method="post" class="d-inline" id="nwResetForm">` wrapper with a simple `<button type="button" class="btn nw-btn-reset" id="nwResetBtn">`
- Update version in `PersonalFinance.Shared/Constants/AppVersion.cs` (patch bump)
- Update `CHANGELOG.md` under [Unreleased] > Fixed section