# 08: Search and Notification UI in Top Bar

**What to build:** The top bar has a working search bar with debounced input and results dropdown, plus a notification
bell with unread count badge and dropdown.

**Blocked by:**

- 02: Notification Backend API and Refit Contract
- 04: Unified Search Backend API
- 06: Sidebar Layout, Top Bar, and Mobile Bottom Nav

**Status:** ready-for-agent

- [ ] `search.js` with debounced search input, API call to ISearchApi, results dropdown with source labels
- [ ] `notifications.js` with notification bell logic (fetch on load, unread count badge, dropdown, mark as read)
- [ ] CSS for search results dropdown and notification dropdown in top bar
- [ ] Search returns results from transactions, budgets, and savings goals
- [ ] Notifications display correctly with unread count
- [ ] "No results" and "No notifications" empty states
