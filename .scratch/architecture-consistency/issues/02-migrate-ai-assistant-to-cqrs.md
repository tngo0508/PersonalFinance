# 02: Migrate AI Assistant to CQRS and feature folder

**What to build:** The AI Assistant feature uses CQRS with MediatR (Commands, Queries, Handlers) instead of direct service calls from the controller. All AI Assistant code is co-located in a dedicated feature folder. API endpoints for chat and article summarization continue to work as before.

**Blocked by:** 01: Create AuditableEntity base class and configure soft deletes.

**Status:** ready-for-agent

- [ ] AI Assistant feature folder created with Commands/, Queries/, and Services/ subfolders
- [ ] ChatCommand and SummarizeArticleCommand created with handlers in the Commands folder
- [ ] Handlers are thin coordinators that delegate to IAiAssistantService (business logic stays in the service)
- [ ] AiAssistantController moved into the AiAssistant feature folder and updated to inject IMediator
- [ ] AiAssistantService and IAiAssistantService moved into AiAssistant/Services/
- [ ] Namespaces updated to follow PersonalFinance.ApiService.AiAssistant convention
- [ ] Controller registered in Program.cs DI container
- [ ] Solution builds successfully with no compilation errors
- [ ] Service-layer unit tests verify business logic still works after CQRS migration
- [ ] API integration tests verify chat and summarize endpoints return correct responses