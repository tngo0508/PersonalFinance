# 03: Migrate NetWorth to CQRS and feature folder

**What to build:** The NetWorth feature uses CQRS with MediatR (Commands, Queries, Handlers) instead of direct service calls from the controller. All NetWorth code is co-located in a dedicated feature folder. All net worth endpoints (save parameters, load parameters, delete parameters, calculate projection) continue to work as before.

**Blocked by:** 01: Create AuditableEntity base class and configure soft deletes.

**Status:** ready-for-agent

- [ ] NetWorth feature folder created with Commands/, Queries/, Services/, and Validators/ subfolders
- [ ] SaveParametersCommand, DeleteParametersCommand, and LoadParametersQuery created with handlers
- [ ] Handlers are thin coordinators that delegate to INetWorthParametersService (business logic stays in the service)
- [ ] NetWorthController moved into the NetWorth feature folder and updated to inject IMediator
- [ ] NetWorthParametersService, INetWorthParametersService, and NetWorthProjectionValidator moved into the feature folder
- [ ] NetWorthProjectionService remains in the feature folder as a stateless calculation utility
- [ ] Namespaces updated to follow PersonalFinance.ApiService.NetWorth convention
- [ ] Controller registered in Program.cs DI container
- [ ] Solution builds successfully with no compilation errors
- [ ] Service-layer unit tests verify business logic still works after CQRS migration
- [ ] API integration tests verify all NetWorth endpoints return correct responses