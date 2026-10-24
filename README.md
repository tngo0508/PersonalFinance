# Personal Finance App

[![Build, Test, and Deploy to Azure Container Apps](https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml/badge.svg)](https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A modular, multi-tier personal finance management application built on **.NET 10** with a **layered architecture** and **.NET Aspire**. Connect Google Drive folders, analyze budget spreadsheets, and plan for financial independence — all from a responsive, self-hosted web interface.

> **New contributor?** Start with [CONTRIBUTING.md](CONTRIBUTING.md), then [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).  
> **Developer Guide**: Local setup, secrets management, EF Core migrations, OAuth configuration, and troubleshooting — see [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).  
> **Deployment Guide**: Azure Container Apps, Docker, CI/CD pipelines, and Infrastructure as Code — see [docs/deployment.md](docs/deployment.md).  
> **Changelog**: Version history and release notes — see [CHANGELOG.md](CHANGELOG.md).

---

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Tech Stack](#tech-stack)
- [Security & Resilience](#security--resilience)
- [Deployment](#deployment)
- [Testing](#testing)
- [Contributing](#contributing)
- [License](#license)

---

## Features

### Google Drive Folder Explorer
Connect and browse Google Drive folders with persistent caching, incremental sync, and interactive DataTables views. Supports multiple drives, AES-256 encrypted credentials, and universal URL parsing.

### Budget Report & Financial Analytics
Transform Google Sheets budget spreadsheets into structured financial insights: planned vs. actual income/expenses, savings rate, category variance, and interactive Chart.js visualizations.

### Net Worth Projection & Financial Independence Planning
Interactive projections for Financial Independence (FI) goals with year-by-year net worth growth, preset scenarios (Conservative, Moderate, Aggressive), Monte Carlo–style randomization, and a 4% rule calculator.

### Spreadsheet Transactions Viewer
View, search, and filter transaction data from Google Sheets with category chips, spending-over-time charts, top category rankings, and CSV export.

### Budget Tracker
In-app monthly budget tracker with Summary and Transactions tabs. Enter planned amounts, actual overrides, and dated transactions directly in the application, stored in Azure SQL Database.

### Dashboard & Navigation
Modern left sidebar navigation with Dashboard home page showing Net Worth, Progress to FI, key figures, budget breakdown, savings goals, recent activity, and quick actions. Includes global search and notification system.

### Landing Page
Public landing page for signed-out visitors showcasing features, how it works, and calls to action. Signed-in users are redirected to their Dashboard.

### Authentication & Identity
ASP.NET Core Identity with email verification (Brevo), Google OAuth 2.0 social sign-in, and secure account linking with pre-account-takeover defense.

---

## Architecture

The solution follows a **layered architecture** pattern with clear separation of concerns, orchestrated by .NET Aspire.

### Layers

1. **UI Layer** (`PersonalFinance.Web`) — MVC controllers, Razor views, Identity, and UI-specific services. Communicates with the API via typed Refit clients.
2. **API Service Layer** (`PersonalFinance.ApiService`) — REST API endpoints, business logic, and service implementations. Uses direct service injection via built-in .NET DI.
3. **Data Layer** (`PersonalFinance.Data`) — EF Core `AppDbContext`, entity definitions, and migrations.
4. **Shared Layer** (`PersonalFinance.Shared`) — DTOs, Refit API contracts, constants, and shared utilities referenced by all layers.

### Dependency Flow

```
PersonalFinance.Web  ──HTTP (Refit)──>  PersonalFinance.ApiService  ──references──>  PersonalFinance.Data
PersonalFinance.Shared  ── referenced by every project (DTOs, Refit contracts, constants)
```

Web references `PersonalFinance.Data` only for ASP.NET Core Identity and consent records. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the request flow and where new code goes.

### Key Principles

- **Direct Service Injection** — ApiService controllers inject service interfaces directly via .NET DI. No mediator libraries (MediatR) or CQRS indirection. See [ADR-0001](docs/adr/0001-standardize-cqrs-feature-organization.md).
- **Feature Folders** — ApiService and Web are organized by Feature in `Features/<Feature>/`, with shared code in `Infrastructure/`. See [ADR-0007](docs/adr/0007-feature-folders-with-interface-only-dependencies.md).
- **Strict Backend/Frontend Separation** — Web controllers call Refit APIs; they never inject ApiService services directly. Business logic lives in ApiService, not in Web controllers.
- **Soft Deletes** — All user-owned entities inherit from `AuditableEntity` with soft-delete support. EF Core global query filters exclude deleted records.
- **Permissive Open-Source Only** — Dependencies are restricted to MIT, BSD, Apache 2.0, or ISC licenses. MediatR and AutoMapper are banned due to commercial licensing.

### Projects

| Project | Role |
|---|---|
| `PersonalFinance.Web` | UI layer — MVC controllers, Razor views, Bootstrap/Chart.js frontend, Refit API clients |
| `PersonalFinance.ApiService` | API layer — REST endpoints, business logic services, validators, direct DI |
| `PersonalFinance.Data` | Data layer — EF Core `AppDbContext`, entities, Azure SQL migrations |
| `PersonalFinance.Shared` | Shared — DTOs, Refit contracts, constants, cross-cutting utilities |
| `PersonalFinance.AppHost` | .NET Aspire orchestrator — service discovery, telemetry, dashboard (local dev) |
| `PersonalFinance.ServiceDefaults` | Shared defaults — OpenTelemetry, health checks, HTTP resilience |
| `PersonalFinance.Tests` | Verification — xUnit unit and integration tests |

### Database

Production runs on **Azure SQL Database**, accessed through Entity Framework Core Code-First migrations. For development, run SQL Server 2022 locally in Docker (`npm run db:up`; see "Local SQL Server (Docker)" in [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)). Both use the same EF Core provider and the same migrations; only the connection string differs.

### Architecture Decision Records

For the rationale behind key design choices, see:

- [ADR-0001](docs/adr/0001-standardize-cqrs-feature-organization.md) — Direct service injection over MediatR/CQRS; permissive license policy
- [ADR-0002](docs/adr/0002-budget-tracker-separate-from-spreadsheet-data.md) — Budget Tracker data stored in separate entities
- [ADR-0003](docs/adr/0003-networth-reset-randomize-frontend-only.md) — Net Worth Reset and Randomize are frontend-only
- [ADR-0004](docs/adr/0004-rbac-roles-from-database-and-policy-checks.md) — RBAC: roles read from the database, code checks policies
- [ADR-0006](docs/adr/0006-one-release-per-merged-pr.md) — One GitHub Release per merged PR, tagged from `AppVersion`
- [ADR-0010](docs/adr/0010-research-never-stores-security-identifiers.md) — Research never stores or shows security identifiers (CUSIP, ISIN)
- [ADR-0011](docs/adr/0011-research-data-from-official-public-sources.md) — Research data comes from official public sources (no FRED), precomputed in a daily refresh

For detailed setup instructions, secrets management, and EF Core migrations, see [**docs/DEVELOPMENT.md**](docs/DEVELOPMENT.md).

---

## Quick Start

### Prerequisites
- **.NET 10 SDK**: the version pinned in [`global.json`](global.json) (10.0.402 or a later 10.0.4xx patch)
- **Git**
- Optional: **Docker Desktop** for containerized deployment

### Run Locally

```bash
# Clone the repository
git clone https://github.com/tngo0508/PersonalFinance.git
cd PersonalFinance

# Configure user secrets (required for database and API access)
# See docs/DEVELOPMENT.md for detailed setup instructions
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<YOUR_CONNECTION_STRING>" --project PersonalFinance/src/PersonalFinance.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<YOUR_CONNECTION_STRING>" --project PersonalFinance/src/PersonalFinance.ApiService

# Launch via .NET Aspire (recommended — starts all services + dashboard)
dotnet run --project PersonalFinance/src/PersonalFinance.AppHost
```

- **Web Frontend**: `https://localhost:7200`
- **API Service**: `https://localhost:7100`
- **Scalar API Docs**: `https://localhost:7100/scalar/v1`
- **Aspire Dashboard**: URL shown in terminal output

> **Important**: Before running the application, you must configure user secrets with your database connection string and API keys. See [**docs/DEVELOPMENT.md**](docs/DEVELOPMENT.md) for complete setup instructions including Brevo API key, Google OAuth credentials, Google Drive API key, and OpenAI API key.

---

## Tech Stack

| Category | Technology |
|---|---|
| **Framework** | .NET 10, ASP.NET Core MVC & Minimal APIs |
| **Orchestration** | .NET Aspire (service discovery, telemetry, dashboard) |
| **Database** | Azure SQL Database (production), SQL Server 2022 in Docker (local development) |
| **ORM** | Entity Framework Core 10 (Code-First, provider-specific migrations) |
| **Authentication** | ASP.NET Core Identity, Google OAuth 2.0, Brevo email verification |
| **API Client** | Refit (typed HTTP client) + Polly (resilience) |
| **Frontend** | Bootstrap 5, DataTables, Chart.js, jQuery |
| **Logging** | Serilog with `AnsiConsoleTheme.Code` |
| **Observability** | OpenTelemetry (metrics, tracing, health checks) |
| **Containerization** | Multi-stage Dockerfiles, chiseled non-root images |
| **CI/CD** | GitHub Actions, GHCR, Azure OIDC (passwordless) |
| **Infrastructure** | Azure Bicep IaC, Container Apps (Consumption, scale-to-zero) |
| **Testing** | xUnit with EF Core InMemory (500+ tests), Playwright e2e |

---

## Security & Resilience

- **Secrets Management**: Credentials managed via .NET User Secrets (local) and Azure Container Apps secret store (production). Nothing committed to Git.
- **Credential Encryption**: User-submitted API keys encrypted with AES-256 (`CredentialProtector`) before persistence.
- **Email Verification**: `RequireConfirmedAccount = true` prevents unverified account sign-in.
- **Antiforgery Protection**: Global antiforgery token validation on all unsafe HTTP methods.
- **HTML Escaping**: Spreadsheet content is HTML-escaped before rendering to prevent injection.
- **Resilience Pipelines**: Automatic exponential backoff, retry, and circuit breakers for all outgoing HTTP calls.
- **Health Checks**: `/health` (readiness) and `/alive` (liveness) endpoints integrated with Aspire and Azure Container Apps probes.
- **Hardened Containers**: Multi-stage builds, chiseled runtime images, non-root user execution.

---

## Deployment

The application supports multiple deployment targets, all engineered for **zero or near-zero operating cost**:

| Method | Target | Description |
|---|---|---|
| **GitHub Actions CI/CD** | Azure Container Apps | Automated build, test, deploy on push to `main`. OIDC passwordless auth, GHCR images, Bicep IaC. |
| **PowerShell / Azure CLI** | Azure Container Apps | Direct terminal control with `infra/deploy.ps1`. Supports dry-run and What-If previews. |
| **Azure Developer CLI** | Azure Container Apps | Single-command provisioning with `azd up`. |
| **Local Docker** | Local machine | Multi-container setup for pre-commit validation and integration testing. |

For complete deployment instructions, see [**docs/deployment.md**](docs/deployment.md).

---

## Testing

```bash
# Run all tests
dotnet test PersonalFinance.sln

# Run with detailed output
dotnet test PersonalFinance.sln --logger "console;verbosity=normal"
```

The test suite covers URL parsing, MIME type resolution, budget CSV analytics, persistence caching, controller actions, external authentication, email templates, service defaults, and CI/CD pipeline validation.

---

## Contributing

Contributions are welcome. For significant changes, open an issue first to discuss the proposed approach.
Setup, verification commands and the pull request checklist are in [CONTRIBUTING.md](CONTRIBUTING.md).

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.