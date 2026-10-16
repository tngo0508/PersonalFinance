# 01: Create AuditableEntity base class and configure soft deletes

**What to build:** All user-owned Entity records gain audit fields (CreatedAt, UpdatedAt, IsDeleted, DeletedAt) and soft delete support so that accidental data deletions can be recovered. EF Core global query filters automatically exclude soft-deleted records from normal queries. A database migration is created and inspected before application.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] AuditableEntity abstract base class created in PersonalFinance.Data with Id, UserId, CreatedAt, UpdatedAt, IsDeleted, and DeletedAt properties
- [ ] All 14 user-owned Entities (NetWorthParameters, SavingsGoal, Item, SavedArticle, UserBudgetAllocation, SpreadsheetBudgetSnapshot, SpreadsheetTransactionRecord, GoogleDriveConnection, GoogleDriveCachedFile, CommunityStory, CommunityComment, CommunityCommentLike, CommunityPollVote, CommunityStoryLike) inherit from AuditableEntity
- [ ] System-defined entities without UserId (CommunityPoll, CommunityPollOption) do not inherit from AuditableEntity
- [ ] EF Core global query filters configured in AppDbContext.OnModelCreating for all AuditableEntity types
- [ ] Existing delete methods (e.g., NetWorthParametersService.DeleteAsync) updated to use soft delete (set IsDeleted = true) instead of hard delete
- [ ] EF Core migration created and inspected for new columns
- [ ] Solution builds successfully with no compilation errors
- [ ] Unit tests verify soft delete behavior and global query filters exclude deleted records