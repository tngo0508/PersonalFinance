# 06: Sidebar Layout, Top Bar, and Mobile Bottom Nav

**What to build:** The layout is replaced with a left sidebar (240px/60px collapsible), minimal top bar, and mobile
bottom navigation bar. All existing pages render correctly with the new layout.

**Blocked by:** None (can start immediately, purely frontend)

**Status:** ready-for-agent

- [ ] `_Layout.cshtml` rewritten: sidebar with 3 groups (Overview, My Finances, Resources), minimal top bar, mobile
  bottom nav
- [ ] Lucide Icons CDN added to layout
- [ ] `sidebar.css` with sidebar styles (expanded/collapsed, groups, active states, responsive)
- [ ] `sidebar.js` with toggle logic and mobile behavior
- [ ] Brand logo links to Dashboard instead of Google Drive
- [ ] Default route changed from Networth to Dashboard in `Program.cs`
- [ ] All 8 existing pages render correctly with new layout (verified manually)
- [ ] E2E tests updated: `nav.navbar` selector replaced with sidebar selectors
