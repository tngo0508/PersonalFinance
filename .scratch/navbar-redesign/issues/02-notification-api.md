# 02: Notification Backend API and Refit Contract

**What to build:** The API can create, list, and mark notifications as read. The Web layer can call these endpoints via
a Refit client.

**Blocked by:** 01: Add Notification Entity and Database Migration

**Status:** ready-for-agent

- [ ] NotificationService in ApiService with CRUD operations (create, get by user, get unread count, mark as read)
- [ ] Notification API Controller with endpoints: GET `/api/Notification`, GET `/api/Notification/unread-count`, POST
  `/api/Notification/{id}/read`
- [ ] NotificationDto and INotificationApi contract in Shared
- [ ] Refit client registered in Web `Program.cs` with UserIdForwardingHandler
- [ ] Unit tests for NotificationService CRUD operations
- [ ] Integration test for API endpoints
