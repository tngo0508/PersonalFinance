# 01: Add Notification Entity and Database Migration

**What to build:** Users can have notifications stored in the database, with proper soft-delete support and audit
fields. This is the foundation for all notification functionality.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Notification entity exists in `PersonalFinance.Data/Entities/` inheriting from `AuditableEntity`
- [ ] NotificationType enum defined with values: BudgetAlert, SavingsMilestone, SyncStatus
- [ ] AppDbContext has `DbSet<Notification>` with soft-delete query filter
- [ ] EF Core migration created and applied successfully
- [ ] Unit test verifies entity configuration (soft delete filter, required fields)
