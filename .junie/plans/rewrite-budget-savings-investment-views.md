---
sessionId: session-261016-130231-1osf
---

# Requirements

### Overview & Goals

Rewrite the Razor views and CSS for three financial planning pages — Budget Planner, Savings Goals, and Investment
Calculator — to deliver a **professional, responsive UI/UX** that works flawlessly across all screen sizes. The current
layouts are cramped, lack visual hierarchy, and don't match the quality of the NetWorth projection page (the
best-designed page in the app). Every page must render perfectly on mobile phones (≤576px), tablets (≤768px), laptops
(≤992px), and desktops (≥1200px).

### User Stories

- As a user on **any device**, I want to see my budget categories organized in a clear, scannable layout so I can adjust
  allocations quickly without excessive scrolling.
- As a user on **mobile**, I want my savings goals displayed as full-width cards with large touch targets and clear
  progress indicators.
- As a user on **desktop**, I want the investment calculator to feel like a polished tool with grouped inputs, prominent
  results, and a large interactive chart.
- As a user, I expect all three pages to have **consistent visual quality** matching the NetWorth projection page.

### Scope

**In Scope:**

- Rewrite `Views/Budget/Index.cshtml` and `css/budget.css`
- Rewrite `Views/SavingsGoals/Index.cshtml` and `css/savings-goals.css`
- Rewrite `Views/InvestmentCalculator/Index.cshtml` and `css/investment-calculator.css`
- Keep all existing JavaScript behavior intact (`budget.js`, `calculator.js`)
- Maintain all existing form fields, model bindings, and controller integration
- Follow the "Indigo Ledger" design system tokens from `site.css` (`--app-*` variables)
- Match the visual quality and layout patterns of the NetWorth projection page
- **Responsive breakpoints**: mobile (≤576px), tablet (≤768px), laptop (≤992px), desktop (≥1200px)
- **Accessibility**: keyboard navigation, visible focus states, semantic headings, ARIA labels, sufficient contrast
  (WCAG AA)
- **Touch-friendly**: minimum 44px touch targets for interactive elements on mobile

**Out of Scope:**

- Changes to controllers, ViewModels, or business logic
- Changes to JavaScript calculation logic
- Changes to the NetWorth page or any other feature
- Database or API changes

### Functional Requirements

1. **Budget Planner**: Organize 10 categories into a 2-column grid on desktop/tablet, single column on mobile; keep the
   pie chart visible in a sidebar on desktop; improve the income input prominence; make the total/remaining indicator
   always visible.
2. **Savings Goals**: Redesign goal cards with better visual hierarchy (thicker progress bars at 12px, clearer amounts,
   cleaner update forms); improve the empty state; polish the add-goal modal; ensure cards are full-width on mobile.
3. **Investment Calculator**: Group input parameters into logical sections; make the three result metrics prominent with
   icons; improve the chart presentation area with more vertical space; ensure inputs are touch-friendly on mobile.

### Non-Functional Requirements

#### Responsive Design

- **Mobile (≤576px)**: Single-column layouts, full-width cards, larger touch targets (44px min), stacked inputs and
  results
- **Tablet (≤768px)**: Single or 2-column layouts depending on content, adjusted padding and font sizes
- **Laptop (≤992px)**: Two-column layouts with adjusted proportions, sidebar patterns
- **Desktop (≥1200px)**: Full two-column or split layouts, sticky sidebars where applicable
- All layouts use **Bootstrap grid** (`row g-3`, `col-*`, `col-sm-*`, `col-md-*`, `col-lg-*`) for consistent spacing
- No horizontal scrolling on any screen size
- Charts resize fluidly with container width

#### Accessibility

- All interactive elements have visible focus states (`:focus-visible`)
- Semantic HTML structure with proper heading hierarchy (h1 → h2 → h3)
- ARIA labels on all icon-only buttons and chart canvases
- Sufficient color contrast (WCAG AA: 4.5:1 for normal text, 3:1 for large text)
- Keyboard navigation for all form controls, sliders, and buttons
- `prefers-reduced-motion` media query support for animations

#### Visual Consistency

- All three pages use the same header pattern (icon + title + subtitle) as NetWorth
- Card components follow Bootstrap `card` / `card-body` structure
- Color palette uses Indigo Ledger tokens (`--app-brand`, `--app-accent-positive`, etc.)
- Typography uses Inter for UI text, IBM Plex Mono for financial data
- Shadows, borders, and border-radius follow design system tokens
- SVG icons throughout (no emoji or inconsistent icon libraries)

#### Performance

- No new heavy dependencies; keep existing Chart.js and canvas-based charting
- CSS scoped to feature-specific prefixes to avoid conflicts
- Charts use `ResizeObserver` for responsive resizing without layout thrashing
- Minimal reflows and repaints on input changes

# Technical Design

### Current Implementation

**Budget Planner** (`Views/Budget/Index.cshtml`, `css/budget.css`, `js/budget/budget.js`):

- Single-column layout with income card → category sliders card (10 categories stacked vertically) → chart/summary card
- On desktop (≥992px), the form and chart split 1.2fr/0.8fr
- Categories use `grid-template-columns: 140px 1fr 100px` (label | slider+input | amount)
- Pie chart rendered via Chart.js (doughnut type)
- Summary table populated dynamically by JS

**Savings Goals** (`Views/SavingsGoals/Index.cshtml`, `css/savings-goals.css`):

- Card grid: `repeat(auto-fill, minmax(340px, 1fr))`
- Each card: header (name + delete), amounts, progress bar, meta (percent + date), inline update form
- Empty state with icon, heading, and CTA button
- Bootstrap modal for adding goals

**Investment Calculator** (`Views/InvestmentCalculator/Index.cshtml`, `css/investment-calculator.css`,
`js/investment-calculator/calculator.js`):

- Two-column grid (1fr 1fr) → inputs left, results right
- 4 input groups (initial, monthly, rate, years) with range sliders + number inputs
- 3 result metric cards + custom canvas chart with tooltips

**Reference — NetWorth Page** (`Views/Networth/_NetWorthContent.cshtml`, `css/networth.css`):

- Split layout: sticky sidebar (inputs) + scrollable main area (results)
- Metric cards row (4 columns), chart section, scenario comparison cards, detailed table
- Uses Bootstrap grid (`row g-3`), card components, and custom `nw-*` CSS classes
- SVG icons throughout, Bootstrap modals for help

### Key Decisions

1. **Follow NetWorth page patterns**: All three views will adopt the same visual language — Bootstrap card components,
   `row g-3` grid spacing, SVG icons, section headers with icon + title, and the Indigo Ledger color palette.

2. **Budget — 2-column category grid**: Instead of stacking all 10 categories vertically, organize them into a 2-column
   grid on desktop (5 rows × 2 columns). Each category cell will show the icon, label, slider, percentage input, and
   dollar amount in a compact card-like block. On mobile, categories collapse to single column.

3. **Savings Goals — larger cards with better hierarchy**: Increase card size, make progress bars more prominent
   (thicker with percentage overlay), move the update form to a cleaner inline input at the bottom, and add a "remaining
   to goal" indicator. Cards are full-width on mobile.

4. **Investment Calculator — grouped inputs with prominent results**: Group the 4 parameters into two sections (
   "Investment Amount" and "Growth Parameters"). Make the result metrics larger and more prominent. Keep the canvas
   chart but give it more breathing room. Layout proportions: inputs `col-lg-4`, results `col-lg-8`.

5. **No JavaScript changes**: The existing JS files (`budget.js`, `calculator.js`) will continue to work. The CSS and
   HTML structure changes will be compatible with the existing DOM selectors and event bindings.

6. **Bootstrap grid for all layouts**: Use `row g-3` with `col-*` classes instead of custom CSS grid for main layout
   structure. This ensures consistent spacing and responsive behavior across all three pages, matching the NetWorth page
   pattern.

### Proposed Changes

#### Budget Planner

- **Layout**: Replace single-column category list with a 2-column grid on desktop
  (`grid-template-columns: repeat(2, 1fr)`), collapsing to 1 column on mobile (<768px). Use Bootstrap `row g-3` with
  `col-lg-8` (categories) + `col-lg-4` (chart sidebar)
- **Category blocks**: Each category becomes a compact card with icon, label, slider, percentage input, and dollar
  amount
- **Chart position**: Move pie chart to a sidebar on the right on desktop; on mobile, chart appears below categories
- **Income input**: Make it a prominent hero-style input at the top, full-width on mobile
- **Total/remaining**: Move to a sticky bottom bar at the bottom of the form area for constant visibility on all screen
  sizes
- **CSS**: Rewrite `budget.css` to use Bootstrap grid + custom category card styles
- **Responsive breakpoints**:
    - ≥992px: 2-column category grid, chart sidebar visible
    - 768px–991px: 2-column category grid, chart below categories
    - <768px: Single column, full-width cards, chart below

#### Savings Goals

- **Card redesign**: Larger cards with icon-based indicators, thicker progress bars (12px) with percentage overlay, and
  a "remaining to goal" amount
- **Update form**: Replace the inline form with a cleaner compact input + button at the bottom
- **Visual hierarchy**: Goal name → current/target amounts → progress bar → metadata → update action
- **Empty state**: More polished with larger icon and better spacing
- **Responsive breakpoints**:
    - ≥768px: `auto-fill, minmax(320px, 1fr)` grid
    - <768px: Single column, full-width cards
- **Modal**: Full-width on mobile, centered on desktop

#### Investment Calculator

- **Input grouping**: Split into two cards — "Investment Amount" (initial + monthly) and "Growth Parameters" (rate +
  years)
- **Results**: Make the 3 metric cards larger with icons and better typography; on mobile, stack vertically
- **Chart**: Give the chart more vertical space (min 350px height on desktop, 250px on mobile) and a cleaner container
- **Layout**: Bootstrap `row g-3` with `col-lg-4` (inputs) + `col-lg-8` (results); on mobile, single column stacked
- **Responsive breakpoints**:
    - ≥992px: Two-column layout (inputs left, results right)
    - <992px: Single column, inputs above results
    - Results cards: 3 columns on desktop, 1 column on mobile

### File Structure

| File                                      | Change                                                                                           |
|-------------------------------------------|--------------------------------------------------------------------------------------------------|
| `Views/Budget/Index.cshtml`               | Rewrite layout: 2-column category grid, sidebar chart panel, hero income input, bottom total bar |
| `wwwroot/css/budget.css`                  | Rewrite: category grid layout, compact category cards, sidebar chart, responsive breakpoints     |
| `Views/SavingsGoals/Index.cshtml`         | Redesign: larger cards, thicker progress bars, remaining indicator, cleaner update forms         |
| `wwwroot/css/savings-goals.css`           | Rewrite: card sizing, progress bar styling, remaining indicator, responsive grid                 |
| `Views/InvestmentCalculator/Index.cshtml` | Redesign: grouped input cards, larger metric cards, better chart container                       |
| `wwwroot/css/investment-calculator.css`   | Rewrite: grouped sections, larger metrics, chart spacing, responsive layout                      |

### Risks & Mitigations

1. **JavaScript selector breakage**: Existing `budget.js` and `calculator.js` rely on specific DOM selectors.
   **Mitigation**: Preserve all existing IDs, class names, and data attributes. Only add new classes, never remove
   existing ones.

2. **CSS conflicts**: New styles might conflict with Bootstrap or `site.css`. **Mitigation**: Use existing `--app-*` CSS
   custom properties. Scope all classes with feature-specific prefixes.

3. **Responsive layout issues**: 2-column category grid might not work on all screens. **Mitigation**: Test at key
   breakpoints (576px, 768px, 992px) with media queries.

4. **Chart rendering**: Charts depend on container dimensions. **Mitigation**: Ensure chart containers have proper
   sizing and ResizeObserver patterns are maintained.

5. **Form binding**: Razor tag helpers must continue to bind correctly. **Mitigation**: Keep all `asp-for` attributes,
   input names, and form structure intact.

# Testing

### Validation Approach

Verify that all three pages render correctly, maintain existing functionality, and are responsive across breakpoints.

### Key Scenarios

1. **Budget Planner**:
    - Categories display in a 2-column grid on desktop (≥992px)
    - Categories collapse to single column on mobile (<768px)
    - Pie chart updates when sliders or inputs change
    - Summary table populates correctly
    - Total/remaining indicator updates in real time
    - Form submission (Save) works with model binding
    - **Responsive**: No horizontal scrolling on any screen size; chart sidebar visible on desktop, below categories on
      mobile
    - **Touch**: Sliders and inputs are touch-friendly (44px min touch targets)

2. **Savings Goals**:
    - Goal cards display in responsive grid
    - Progress bars render correctly with percentage
    - Update form submits correctly via POST
    - Delete form works with confirmation
    - Add goal modal opens/closes and submits
    - Empty state displays when no goals exist
    - **Responsive**: Cards are full-width on mobile, grid on desktop; modal is full-width on mobile
    - **Touch**: Update buttons and delete buttons have 44px min touch targets

3. **Investment Calculator**:
    - Input sliders and number inputs sync correctly
    - Result metrics update in real time
    - Canvas chart renders and responds to input changes
    - Chart tooltip works on hover
    - Layout stacks correctly on mobile
    - **Responsive**: Two-column layout on desktop (≥992px), single column on mobile; results cards stack vertically on
      mobile
    - **Touch**: Sliders and number inputs are touch-friendly; chart is scrollable if needed

### Edge Cases

- Budget: allocations over 100% show warning state
- Budget: zero income shows $0 for all amounts
- Savings Goals: goal with 0% progress, 100% progress, and over-100%
- Investment Calculator: zero values, maximum values, and edge cases
- **Responsive**: All layouts work at 320px (small phone), 768px (tablet), 992px (laptop), 1200px (desktop)
- **Accessibility**: All interactive elements are keyboard-navigable with visible focus states
- **Reduced motion**: Animations are disabled when `prefers-reduced-motion` is set

### Regression Checks

- NetWorth page still renders correctly (no CSS conflicts)
- Navigation links still work
- Authentication/authorization still enforced
- Existing JavaScript files load without errors
- No horizontal scrolling on any page at any screen size

# Delivery Steps

### ✓ Step 1: Rewrite Budget Planner view and CSS

### ✓ Step 2: Redesign Savings Goals view and CSS

### ✓ Step 3: Redesign Investment Calculator view and CSS

Redesign the Budget Planner page with a 2-column category grid, sidebar chart panel, and improved visual hierarchy.

- Rewrite `Views/Budget/Index.cshtml`: Replace the single-column category list with a Bootstrap `row g-3` layout —
  `col-lg-8` for categories (2-column grid) and `col-lg-4` for the chart/summary sidebar. Add a hero-style income input
  card at the top. Move the total/remaining indicator to a sticky bottom bar with the Save button.
- Rewrite `wwwroot/css/budget.css`: Style the 2-column category grid (`grid-template-columns: repeat(2, 1fr)`), compact
  category cards with icon + label + slider + input + amount, sticky chart sidebar, and responsive breakpoints (single
  column on mobile <768px). Use Indigo Ledger design tokens (`--app-*` variables).
- Preserve all existing DOM selectors (IDs, classes, `data-*` attributes) that `budget.js` depends on: `.budget-slider`,
  `.budget-percent-input`, `.budget-category__amount`, `#budgetPieChart`, `#budgetSummaryBody`, etc.
- Keep all `asp-for` bindings, form structure, and anti-forgery tokens intact.
- **Responsive**: Categories in 2-column grid on desktop (≥992px), single column on mobile (<768px). Chart sidebar on
  desktop, below categories on mobile. No horizontal scrolling at any screen size.
- **Accessibility**: Visible focus states on all inputs, ARIA labels on chart canvas, semantic heading hierarchy.

### Step 2: Redesign Savings Goals view and CSS

Redesign the Savings Goals page with larger, more polished cards, thicker progress bars, and cleaner update forms.

- Rewrite `Views/SavingsGoals/Index.cshtml`: Redesign goal cards with better visual hierarchy — goal name with icon →
  current/target amounts → thick progress bar (12px) with percentage overlay → "remaining to goal" indicator → metadata
  (target date) → compact update form. Improve the empty state with larger icon and better spacing. Polish the add-goal
  modal with section dividers.
- Rewrite `wwwroot/css/savings-goals.css`: Larger cards (`minmax(320px, 1fr)`), thicker progress bars with percentage
  text overlay, remaining indicator styling, compact update form layout, and improved empty state. Use Bootstrap card
  components and Indigo Ledger tokens.
- Preserve all existing form actions, `asp-action` attributes, anti-forgery tokens, and modal structure.
- **Responsive**: Cards in `auto-fill, minmax(320px, 1fr)` grid on desktop, single column on mobile (<768px). Modal is
  full-width on mobile, centered on desktop. All touch targets ≥44px.
- **Accessibility**: Focus-visible states on buttons and inputs, ARIA labels on delete buttons, keyboard-navigable
  forms.

### Step 3: Redesign Investment Calculator view and CSS

Redesign the Investment Calculator page with grouped input sections, larger metric cards, and a better chart
presentation.

- Rewrite `Views/InvestmentCalculator/Index.cshtml`: Group the 4 input parameters into two cards — "Investment Amount"
  (initial + monthly) and "Growth Parameters" (rate + years). Make the 3 result metric cards larger with icons and
  better typography. Give the chart more vertical space (min 350px on desktop, 250px on mobile). Use Bootstrap `row g-3`
  with `col-lg-4` (inputs) and `col-lg-8` (results).
- Rewrite `wwwroot/css/investment-calculator.css`: Style the grouped input sections with card headers, larger metric
  cards with icons, improved chart container with more height, and responsive layout (single column on mobile <992px).
  Use Indigo Ledger design tokens.
- Preserve all existing DOM selectors that `calculator.js` depends on: `#calcInitial`, `#calcMonthly`, `#calcRate`,
  `#calcYears`, `#calcInitialRange`, etc., and the chart canvas `#calcChart`.
- Keep the page as a pure client-side calculator (no controller changes needed).
- **Responsive**: Two-column layout on desktop (≥992px), single column on mobile. Results cards in 3-column grid on
  desktop, single column on mobile. Chart height adjusts for screen size.
- **Accessibility**: Focus-visible states, ARIA labels on chart, keyboard-navigable sliders and inputs.