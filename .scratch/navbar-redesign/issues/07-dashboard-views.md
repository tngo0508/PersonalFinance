# 07: Dashboard Views, Charts, and ApexCharts

**What to build:** A working Dashboard home page with grid of cards showing Net Worth trend, Budget status, Savings
Goals progress, Recent Activity, and Quick Actions with interactive charts.

**Blocked by:**

- 05: Dashboard Backend API with Aggregation
- 06: Sidebar Layout, Top Bar, and Mobile Bottom Nav

**Status:** ready-for-agent

- [ ] DashboardController in Web layer calls IDashboardApi and returns view
- [ ] `Views/Dashboard/Index.cshtml` with grid of cards layout
- [ ] ApexCharts CDN added, `dashboard-charts.js` with chart rendering (Net Worth trend, Budget vs Actual, Savings
  progress)
- [ ] `dashboard.css` for Dashboard-specific styles
- [ ] Loading states and empty states for dashboard cards
- [ ] Charts render correctly with real data
- [ ] Dashboard accessible as default home page
