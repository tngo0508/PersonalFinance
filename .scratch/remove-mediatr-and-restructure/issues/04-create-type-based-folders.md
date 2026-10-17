# 04: Create type-based folder structure (expand phase)

**What to build:** New type-based folders created inside the ApiService project: `Controllers/`, `Services/`, `Validators/`. The old feature-based folders (NetWorth/, Budget/, Community/, etc.) remain untouched. This prepares the target structure without breaking anything, following the expand–migrate–contract pattern for wide refactors.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Create `Controllers/` folder in ApiService
- [ ] Create `Services/` folder in ApiService
- [ ] Create `Validators/` folder in ApiService
- [ ] Verify old feature-based folders are untouched
- [ ] Solution still builds with zero errors
