# Personal Finance App

[![Build, Test, and Deploy to Azure Container Apps](https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml/badge.svg)](https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml)

A modular, multi-tier personal finance management application built on **.NET 10**, designed for high maintainability, efficiency, and data privacy. It features a clean architecture separating the backend RESTful API, responsive web frontend, shared contract/parsing logic, and a persistent SQLite data layer.

> 📖 **Developer Documentation**: For detailed local setup, secrets management, Aspire dashboard debugging, EF Core migration workflows, and common troubleshooting solutions, see [**DEVELOPMENT.md**](DEVELOPMENT.md).  
> 🚀 **Deployment Guide**: For step-by-step instructions on deploying to Azure Container Apps (zero operating cost), local Docker, and CI/CD pipelines, see [**deployment.md**](deployment.md).

---

## Architecture Overview

The solution follows a modern, decoupled **Clean Architecture** pattern to maximize separation of concerns, testability, and operational simplicity.

```
                              ┌──────────────────────────────────────────────┐
                              │            PersonalFinance.Web               │
                              │  (ASP.NET Core MVC, Razor Pages, Identity,   │
                              │   DataTables, Chart.js, Bootstrap 5)         │
                              └───────────────────────┬──────────────────────┘
                                                      │
                                                      │ Typed Refit Client (IGoogleDriveApi)
                                                      │ + Polly HTTP Resilience Pipeline
                                                      ▼
                              ┌──────────────────────────────────────────────┐
                              │         PersonalFinance.ApiService           │
                              │  (REST API, GoogleDriveService, OpenAPI,     │
                              │   Scalar UI at /scalar/v1, Health Checks)    │
                              └───────────────┬───────────────┬──────────────┘
                                              │               │
                     Shared Contracts / DTOs  │               │ EF Core 10 / SQLite
                     & Parsing Algorithms     │               │ (Direct & Design-Time)
                                              ▼               ▼
                ┌──────────────────────────────────┐    ┌──────────────────────────────────┐
                │     PersonalFinance.Shared       │    │      PersonalFinance.Data        │
                │  - DTOs & API Contracts          │    │  - AppDbContext & SQLite Schema  │
                │  - GoogleDriveHelper Parser      │    │  - GoogleDriveConnection Entity  │
                │  - CredentialProtector (AES-256) │    │  - GoogleDriveCachedFile Entity  │
                │  - URL & Byte Format Helpers     │    │  - DatabasePathHelper            │
                └──────────────────────────────────┘    └─────────────────┬────────────────┘
                                                                          │
                                                                          ▼
                                                               ┌─────────────────────┐
                                                               │  PersonalFinance.db │
                                                               │  (Solution Root)    │
                                                               └─────────────────────┘
```

### Project Responsibilities

| Project | Architectural Role & Responsibilities |
|---|---|
| **`PersonalFinance.AppHost`** | **Orchestration Layer (.NET Aspire)**: Aspire application host that coordinates startup, dependencies, service discovery, and telemetry across `PersonalFinance.ApiService` and `PersonalFinance.Web` with the Aspire dashboard. |
| **`PersonalFinance.ServiceDefaults`** | **Cross-Cutting Service Defaults (.NET Aspire)**: Reusable Aspire defaults configuring OpenTelemetry metrics/tracing, liveness/readiness health checks (`/health`, `/alive`), resilient HTTP pipelines, and service discovery. |
| **`PersonalFinance.Web`** | **Presentation Layer**: ASP.NET Core MVC & Razor Pages application. Handles user interaction, ASP.NET Core Identity authentication, session state, DataTables file browsing, interactive Chart.js financial visualizations, and communicates with `ApiService` using strongly-typed Refit clients with Aspire service discovery and resilience policies. |
| **`PersonalFinance.ApiService`** | **Application & API Layer**: RESTful backend service. Exposes OpenAPI endpoints, integrates with the Google Drive API v3, coordinates background data fetching, executes spreadsheet export conversions, serves structured budget reports, and exposes interactive Scalar API documentation (`/scalar/v1`) and `/health` endpoints. |
| **`PersonalFinance.Data`** | **Persistence Layer**: EF Core database context (`AppDbContext`), ASP.NET Core Identity entities, domain models (`GoogleDriveConnection`, `GoogleDriveCachedFile`, `Item`), migrations, and design-time factory (`AppDbContextFactory`). Uses `DatabasePathHelper` to bind to a single shared SQLite database file (`PersonalFinance.db`). |
| **`PersonalFinance.Shared`** | **Domain & Cross-Cutting Layer**: Core data transfer objects (DTOs), Refit API interface contracts (`IGoogleDriveApi`, `IItemsApi`), cryptographic helpers (`CredentialProtector` for AES-256 encryption and key masking), and financial parsing engines (`GoogleDriveHelper`). |
| **`PersonalFinance.Tests`** | **Verification Layer**: Comprehensive xUnit unit and integration test suite verifying URL parsing, MIME type resolution, budget CSV analytics engines, persistence caching, controller actions, and Aspire service defaults. |

---

## Data Flow & System Interactions

### 1. Google Drive Folder Exploration & Caching Flow
1. **User Connection**: Authenticated user submits a Google Drive folder link or ID and API key on `/GoogleDrive`.
2. **Security & Persistence**: The connection configuration is encrypted via `CredentialProtector` (AES-256) and saved in SQLite (`GoogleDriveConnection`).
3. **Fetching & Caching**: `PersonalFinance.ApiService` queries Google Drive API v3, normalizes metadata (size, MIME category, timestamps), and persists records to `GoogleDriveCachedFile`.
4. **Subsequent Access**: On repeat visits, metadata is served immediately from the local SQLite cache without triggering external API calls, providing near-instant page loads.
5. **Incremental Sync**: The manual "Sync / Refresh" button detects new, updated, or removed files, updating only changed records.

### 2. Monthly Budget Report & Analytics Flow
1. **Spreadsheet Discovery**: User clicks "View Budget Report" on any Google Sheet or spreadsheet CSV in their Drive.
2. **Fetch & Conversion**: `ApiService` exports the spreadsheet via Google Drive API (or uses cached CSV data) and passes the raw content to `GoogleDriveHelper`.
3. **Parsing Engine**: `GoogleDriveHelper` automatically detects single-month and full-year multi-month budget formats, parses planned vs. actual income/expenses, extracts category allocations, and computes starting/ending balances.
4. **Analytics Delivery**: A structured `MonthlyBudgetReportDto` is returned through the typed Refit client to the frontend.
5. **Interactive Visualization**: The web client renders KPI summary cards (Total Income, Total Expenses, Net Surplus/Deficit, Savings Rate), Chart.js comparison charts, and category variance progress bars with print support.

---

## Features

### 1. Google Drive Folder Explorer & Persistent Caching
Allows users to connect, manage, and cache Google Drive folders with persistent SQLite storage so they only need to connect a folder once and avoid repeated API requests (`/GoogleDrive`).
- **Persistent SQLite Storage**: Google Drive folder connections (`GoogleDriveConnection`) and file metadata (`GoogleDriveCachedFile`) are saved in SQLite.
- **Subsequent Login Auto-Loading**: Saved configurations load automatically upon login, skipping setup flows and serving cached data with zero external API calls.
- **Multiple Drives Support**: Users can connect multiple Google Drive folders simultaneously, switch between them seamlessly, and add new drives without overwriting existing ones.
- **Incremental Synchronization & Manual Refresh**: Smart synchronization updates modified files, inserts new items, and removes deleted files without re-downloading unchanged metadata. Users can trigger manual sync via the "Sync / Refresh" button.
- **Credential Security & Re-authentication**: API credentials and tokens are AES-encrypted in SQLite, masked in UI outputs (`AIza...8xY2`), never logged in plaintext, and isolated by authenticated user. Expired credentials prompt an in-place re-authentication modal.
- **Universal URL Parsing**: Supports standard folder URLs, shortened/view URLs, and raw folder IDs via `GoogleDriveHelper`.
- **Interactive DataTables & Grid Views**: Real-time category filtering (Sheets, Docs, PDFs, Images, Slides, Folders), sorting by name, size (raw byte sorting), creation/modification dates, and pagination.
- **Direct Access**: Clickable links to open files/folders directly in Google Drive, plus quick clipboard copy actions.

### 2. Google Drive Monthly Budget Report & Financial Analytics
Transforms raw budget spreadsheets stored in Google Drive into structured, actionable financial insights directly from the web interface.
- **Automatic Layout Detection**: Seamlessly identifies and parses both single-month budget sheets and 12-month full-year overview spreadsheets.
- **Income & Expense Analytics**: Computes planned vs. actual totals, net surplus/deficit, net savings rate (%), and budget variance across all categories.
- **Starting & Ending Balances**: Tracks beginning and ending cash balances across months for cash flow visibility.
- **Visual Analytics**: Interactive Chart.js visualizations including income vs. expense bar/line charts and category spending doughnut charts.
- **Category Progress & Variance**: Visual progress bars highlighting under-budget and over-budget category allocations.
- **Export & Print**: Dedicated clean printable report view for physical archiving and financial reviews.

#### How to Get a Free Google Cloud API Key (Step-by-Step for Non-Technical Users)

To query live Google Drive folders, you can obtain a free API key from Google in about 2 minutes:

1. **Open Google Cloud Console**:
   - Go to [console.cloud.google.com](https://console.cloud.google.com/) and sign in with any standard Google/Gmail account.
2. **Create a Project**:
   - Click the **Project dropdown** at the top of the page (next to the Google Cloud logo) and click **"New Project"**.
   - Enter a name (e.g., `PersonalFinance-DriveExplorer`) and click **"Create"**.
3. **Enable the Google Drive API**:
   - In the left sidebar navigation, click **APIs & Services** &rarr; **Library** (or search for *"Google Drive API"* in the top search bar).
   - Select **Google Drive API** and click the blue **"Enable"** button.
4. **Generate Your API Key**:
   - In the left sidebar, click **APIs & Services** &rarr; **Credentials**.
   - Click **"+ CREATE CREDENTIALS"** at the top of the page and choose **"API key"**.
5. **Copy and Use Your Key**:
   - A dialog will show your generated API key (starts with `AIzaSy...`).
   - Copy the key and paste it into the **Google API Key** field on the `/GoogleDrive` web page, or configure it securely in local User Secrets:
     ```bash
     dotnet user-secrets set "GoogleDrive:ApiKey" "YOUR_API_KEY_HERE" --project PersonalFinance/src/PersonalFinance.ApiService
     ```

> **Note**:
> - **Cost**: The Google Drive API is **free** for personal use (Google provides thousands of free queries per day).
> - **Folder Sharing Requirement**: The Google Drive folder's General access must be set to **"Anyone with the link"** (Role: Viewer). API keys operate without user login credentials and can only query public/link-shared folders. If left as *"Restricted"*, Google Drive returns an HTTP 404 (Not Found) or HTTP 403 (Forbidden) access error.
>
> #### Troubleshooting File Listing Issues
> 1. **HTTP 404 / Cannot See Files**: Google masks private folders with HTTP 404. In Google Drive, right-click the folder &rarr; click **Share** &rarr; **Share** &rarr; change General access to **"Anyone with the link"** (Viewer).
> 2. **HTTP 403 Forbidden**: Ensure the **Google Drive API** is enabled in your Google Cloud Console under *APIs & Services > Library*, and that your API key has no restricting IP/HTTP referrers preventing requests.
> 3. **0 Files Returned**: If files exist in the folder but are not listed, confirm both the folder and nested items are accessible to "Anyone with the link".

---

## Configuration & User Secrets Management

To prevent accidental credential leaks into public Git repositories, private keys are managed via [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) during local development.

### 1. Brevo Transactional Email Sender
ASP.NET Core Identity is configured with `RequireConfirmedAccount = true`, requiring user verification upon registration. Transactional emails are dispatched via the **Brevo REST API** (`https://api.brevo.com/v3/smtp/email`) with responsive HTML templates.

Configure the Brevo API key in your local secret store:

```bash
# Set Brevo API Key for Web Frontend
dotnet user-secrets set "Brevo:ApiKey" "<YOUR_BREVO_API_KEY>" --project PersonalFinance/src/PersonalFinance.Web
```

Verify secrets stored for `PersonalFinance.Web`:
```bash
dotnet user-secrets list --project PersonalFinance/src/PersonalFinance.Web
```

---

## Database Configuration (SQLite)

The application uses **SQLite** as its lightweight, zero-configuration database engine.

- **Unified Solution Database**: During local development, all projects (`ApiService`, `Web`, and EF Core CLI tooling) dynamically resolve the database file to the **solution root** (`PersonalFinance.db`) via `DatabasePathHelper.ResolveConnectionString()`. This prevents fragmented, duplicate databases from forming inside individual project subfolders.
- **Connection String**: `"Data Source=PersonalFinance.db"` configured in `appsettings.json`.
- **Automatic Migration**: When running in `Development` mode, both `ApiService` and `Web` automatically apply pending migrations on startup via `db.Database.Migrate()`.

### Git & Database File Exclusions

SQLite database instances (`*.db`, `*.sqlite`, `*.db-wal`, `*.db-shm`) are excluded from Git version control via `.gitignore`. 
- **Reasoning**: SQLite database files are binary, causing repository bloat and merge conflicts. Additionally, committing database files risks exposing sensitive authentication credentials or user data.
- **Workflow**: Schema definitions and migrations are tracked in source code (`PersonalFinance.Data/Migrations/`), enabling reproducible database instances across environments.

---

## Entity Framework Core Code-First Workflow

The project follows the **Code-First** approach: database schemas are defined in C# models and evolved using EF Core Migrations.

### 1. Prerequisites (One-Time Setup)

Install or update the EF Core command-line global tool:

```bash
dotnet tool install --global dotnet-ef
# Or update if already installed:
dotnet tool update --global dotnet-ef
```

---

### 2. Design-Time DbContext Factory

To streamline CLI operations across multi-project architectures, `PersonalFinance.Data` includes an `IDesignTimeDbContextFactory<AppDbContext>` (`AppDbContextFactory.cs`). This allows running EF Core commands directly against the data project without having to specify `--startup-project`.

---

### 3. Step-by-Step Code-First Workflow

#### Step A: Define or Update Entity Models
Create or modify entity classes under `PersonalFinance/src/PersonalFinance.Data/Entities/`:

```csharp
namespace PersonalFinance.Data.Entities;

public class Transaction
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DateUtc { get; set; }
    public string Category { get; set; } = string.Empty;
}
```

#### Step B: Register Entities in `AppDbContext`
Add the corresponding `DbSet<T>` and configure constraints inside `PersonalFinance/src/PersonalFinance.Data/AppDbContext.cs`:

```csharp
public DbSet<Transaction> Transactions => Set<Transaction>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Transaction>(entity =>
    {
        entity.HasKey(t => t.Id);
        entity.Property(t => t.Description).IsRequired().HasMaxLength(250);
        entity.Property(t => t.Amount).HasPrecision(18, 2);
        entity.HasIndex(t => t.DateUtc);
    });
}
```

#### Step C: Add a Migration
Generate a new migration by running the following command from the repository root:

```bash
dotnet ef migrations add <DescriptiveMigrationName> --project PersonalFinance/src/PersonalFinance.Data
```

*Example:*
```bash
dotnet ef migrations add AddTransactionEntity --project PersonalFinance/src/PersonalFinance.Data
```

This generates C# migration files under `PersonalFinance/src/PersonalFinance.Data/Migrations/`.

#### Step D: Apply Migration to Database
Apply pending migrations to the SQLite database:

```bash
dotnet ef database update --project PersonalFinance/src/PersonalFinance.Data
```

Alternatively, launching `PersonalFinance.ApiService` or `PersonalFinance.Web` in `Development` mode will automatically apply migrations via `db.Database.Migrate()`.

---

### 4. Working with JetBrains Rider EF Core Tools

If using JetBrains Rider:
1. Open the **Entity Framework Core** tool window.
2. Set **Migrations project**: `PersonalFinance.Data`.
3. Set **Startup project**: `PersonalFinance.ApiService` (or `PersonalFinance.Web`).
4. Set **DbContext class**: `AppDbContext`.
5. Execute **Add Migration** or **Update Database** directly from the IDE UI.

---

### 5. Common EF Core CLI Commands Reference

| Action | Command |
|---|---|
| **Add New Migration** | `dotnet ef migrations add <Name> --project PersonalFinance/src/PersonalFinance.Data` |
| **Apply All Migrations** | `dotnet ef database update --project PersonalFinance/src/PersonalFinance.Data` |
| **Rollback to Specific Migration** | `dotnet ef database update <TargetMigrationName> --project PersonalFinance/src/PersonalFinance.Data` |
| **Remove Last Unapplied Migration** | `dotnet ef migrations remove --project PersonalFinance/src/PersonalFinance.Data` |
| **Generate SQL Script** | `dotnet ef migrations script --project PersonalFinance/src/PersonalFinance.Data -o migration.sql` |
| **List Migrations** | `dotnet ef migrations list --project PersonalFinance/src/PersonalFinance.Data` |

---

## Running the Application

### 1. Run via .NET Aspire AppHost (Recommended)
Launch the entire solution with the Aspire dashboard, service discovery, metrics, and trace observability:
```bash
dotnet run --project PersonalFinance/src/PersonalFinance.AppHost
```
- **Aspire Dashboard**: Accessible at the URL output in terminal (e.g., `https://localhost:17227`)
- **Web Frontend**: Automatically launched and configured to discover `ApiService`
- **API Service**: Automatically launched with health and OpenAPI/Scalar endpoints

### 2. Run Standalone Projects

#### ApiService
```bash
dotnet run --project PersonalFinance/src/PersonalFinance.ApiService
```
- **Scalar API UI**: `https://localhost:7100/scalar/v1`
- **OpenAPI Document**: `https://localhost:7100/openapi/v1.json`
- **Health Check**: `https://localhost:7100/health`

#### Web Frontend
```bash
dotnet run --project PersonalFinance/src/PersonalFinance.Web
```
- **Web App**: `https://localhost:7200`
- **Health Check**: `https://localhost:7200/health`

---

## Testing

Execute the automated test suite covering unit, parser, and controller integration tests:

```bash
dotnet test
```

---

## Security, Resilience & Observability

- **Secrets Management**: Sensitive credentials (such as Brevo email API keys and Google Drive API keys) are kept out of source control using `.NET User Secrets` during development.
- **Credential Encryption**: User-submitted Google API keys are encrypted using AES-256 (`CredentialProtector`) prior to SQLite persistence and masked (`AIza...8xY2`) in UI outputs.
- **Identity & Email Verification**: User authentication is enforced via ASP.NET Core Identity with mandatory email verification (`RequireConfirmedAccount = true`) delivered via Brevo REST API and responsive HTML templates.
- **Resilience Pipelines**: Outgoing HTTP calls to `ApiService` and external Google APIs leverage `Microsoft.Extensions.Http.Resilience` for automatic exponential backoff, retry handling, and circuit breakers.
- **Logging**: Structured, high-contrast logging configured across all services via Serilog and `AnsiConsoleTheme.Code`.
- **Health Checks & Telemetry**: Built-in `/health` and `/alive` uptime endpoints with full OpenTelemetry tracing and metrics integrated into the .NET Aspire dashboard.
- **API Documentation**: Interactive OpenAPI documentation served via Scalar at `/scalar/v1`.
- **Zero-Cost Azure Deployment & CI/CD**: Fully automated GitHub Actions workflow (`deploy-azure.yml`) using GitHub Container Registry (`ghcr.io`), Azure OIDC federated passwordless credentials, Bicep IaC, and Azure Container Apps Consumption (scale-to-zero) for a $0.00 to <$0.20/month operating cost.