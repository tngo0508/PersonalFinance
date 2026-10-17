# 06: Migrate services and update namespaces (migrate batch 2)

**What to build:** All service interfaces and implementations moved from feature-based folders to `Services/` with updated namespaces. This includes ~22 files: service interfaces (IAiAssistantService, IBudgetService, etc.) and implementations (AiAssistantService, BudgetService, etc.). All `using` statements updated, including references from controllers migrated in ticket 05. The solution builds with zero errors.

**Blocked by:** 05: Migrate controllers and update namespaces (migrate batch 1).

**Status:** ready-for-agent

- [ ] Move all service interface files to `Services/`
- [ ] Move all service implementation files to `Services/`
- [ ] Update namespaces in all moved service files
- [ ] Update `using` statements in controllers and other files that reference services
- [ ] Update Program.cs service registrations if namespaces changed
- [ ] Solution builds with zero errors
