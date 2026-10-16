# 04: Migrate GoogleDrive to CQRS and feature folder

**What to build:** The GoogleDrive feature uses CQRS with MediatR (Commands, Queries, Handlers) instead of direct service calls from the controller. All GoogleDrive code is co-located in a dedicated feature folder. All Google Drive endpoints (explore, connections, sync, reauth, delete connection) continue to work as before.

**Blocked by:** 01: Create AuditableEntity base class and configure soft deletes.

**Status:** ready-for-agent

- [ ] GoogleDrive feature folder created with Commands/, Queries/, and Services/ subfolders
- [ ] Commands and Queries created for all GoogleDrive endpoints (Explore, GetConnections, Connect, DeleteConnection, Sync, Reauth, etc.)
- [ ] Handlers are thin coordinators that delegate to IGoogleDriveService (business logic stays in the service)
- [ ] GoogleDriveController moved into the GoogleDrive feature folder and updated to inject IMediator
- [ ] GoogleDriveService and IGoogleDriveService moved into GoogleDrive/Services/
- [ ] Namespaces updated to follow PersonalFinance.ApiService.GoogleDrive convention
- [ ] Controller registered in Program.cs DI container
- [ ] Solution builds successfully with no compilation errors
- [ ] Service-layer unit tests verify business logic still works after CQRS migration
- [ ] API integration tests verify all GoogleDrive endpoints return correct responses