# 06: Organize DTOs by feature subfolders and clean up

**What to build:** All 16 DTOs in PersonalFinance.Shared are moved into feature subfolders (e.g., DTOs/NetWorth/, DTOs/Community/, DTOs/GoogleDrive/) for better navigation. Empty old folders (Controllers/, Services/, Features/, Validators/) are removed from ApiService. The full solution builds with no errors after all architecture changes.

**Blocked by:** 02: Migrate AI Assistant to CQRS and feature folder, 03: Migrate NetWorth to CQRS and feature folder, 04: Migrate GoogleDrive to CQRS and feature folder, 05: Reorganize existing CQRS features into feature folders.

**Status:** ready-for-agent

- [ ] DTOs moved into feature subfolders: DTOs/NetWorth/ (NetWorthParametersDto, NetWorthProjectionDto, NetWorthProjectionRequestDto), DTOs/Community/ (CommunityDto), DTOs/GoogleDrive/ (GoogleDriveConnectionDto, GoogleDriveFileDto, GoogleDriveFolderResponseDto, GoogleDriveRequestDto, ConnectGoogleDriveRequestDto, SyncGoogleDriveRequestDto, ReauthGoogleDriveRequestDto), DTOs/AiAssistant/ (AiChatDto), DTOs/Watchlist/ (ItemDto), DTOs/SpreadsheetAnalysis/ (SpreadsheetTransactionsDto, MonthlyBudgetReportDto), DTOs/News/ (NewsSummaryDto)
- [ ] All namespace references and using statements updated across the project to reflect new DTO locations
- [ ] Empty Controllers/ folder removed from ApiService
- [ ] Empty Services/ folder removed from ApiService
- [ ] Empty Features/ folder removed from ApiService
- [ ] Empty Validators/ folder removed from ApiService
- [ ] ValidationBehavior moved to an appropriate location (e.g., ApiService core folder)
- [ ] XUserIdAuthenticationHandler moved to an appropriate location (e.g., ApiService core folder)
- [ ] Full solution builds successfully with no compilation errors
- [ ] All existing tests pass after reorganization