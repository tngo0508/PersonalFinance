# Personal Finance App

[![Build, Test, and Deploy to Azure Container Apps](https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml/badge.svg)](https://github.com/tngo0508/PersonalFinance/actions/workflows/deploy-azure.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A modular, multi-tier personal finance management application built on **.NET 10** with **Clean Architecture** and **.NET Aspire**. Connect Google Drive folders, analyze budget spreadsheets, and plan for financial independence — all from a responsive, self-hosted web interface.

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

### Authentication & Identity
ASP.NET Core Identity with email verification (Brevo), Google OAuth 2.0 social sign-in, and secure account linking with pre-account-takeover defense.

---

## Architecture

The solution follows a **Clean Architecture** pattern with .NET Aspire orchestration:

```
┌─────────────────────────────────────────────────────────────────┐
│                    PersonalFinance.Web                          │
│         (MVC, Razor Pages, Identity, Chart.js, Bootstrap)       │
└─────────────────────────────┬───────────────────────────────────┘
                              │ Typed Refit Client + Polly Resilience
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  PersonalFinance.ApiService                     │
│           (REST API, Google Drive, OpenAPI / Scalar)            │
└───────────────┬───────────────────┬─────────────────────────────┘
                │                   │
    ┌───────────▼──────────┐  ┌─────▼─────────────────────────────┐
    │  PersonalFinance.    │  │  PersonalFinance.Data             │
    │  Shared              │  │  (EF Core, SQLite + SQL Server)   │
    │  (DTOs, Contracts,   │  │                                   │
    │   Parsers, Crypto)   │  │  ┌──────────────────────────┐    │
    └──────────────────────┘  │  │  PersonalFinance.db       │    │
                              │  │  / Azure SQL Database     │    │
                              │  └──────────────────────────┘    │
                              └──────────────────────────────────┘
```

| Project | Role |
|---|---|
| `PersonalFinance.Web` | Presentation layer — MVC, Identity, Refit client, Chart.js visualizations |
| `PersonalFinance.ApiService` | Application & API layer — REST endpoints, Google Drive integration, Scalar docs |
| `PersonalFinance.Data` | Persistence layer — EF Core `AppDbContext`, migrations, Identity stores |
| `PersonalFinance.Shared` | Domain & cross-cutting — DTOs, Refit contracts, `GoogleDriveHelper`, `CredentialProtector` |
| `PersonalFinance.AppHost` | .NET Aspire orchestrator — service discovery, telemetry, dashboard (local dev) |
| `PersonalFinance.ServiceDefaults` | Shared defaults — OpenTelemetry, health checks, HTTP resilience |
| `PersonalFinance.Tests` | Verification — xUnit unit and integration tests (104+ tests) |

For detailed data flow diagrams and system interaction sequences, see [**docs/DEVELOPMENT.md**](docs/DEVELOPMENT.md).

---

## Quick Start

### Prerequisites
- **.NET 10 SDK** (v10.0.100 or higher)
- **Git**
- Optional: **Docker Desktop** for containerized deployment

### Run Locally

```bash
# Clone the repository
git clone https://github.com/tngo0508/PersonalFinanceApp.git
cd PersonalFinanceApp

# Launch via .NET Aspire (recommended — starts all services + dashboard)
dotnet run --project PersonalFinance/src/PersonalFinance.AppHost
```

- **Web Frontend**: `https://localhost:7200`
- **API Service**: `https://localhost:7100`
- **Scalar API Docs**: `https://localhost:7100/scalar/v1`
- **Aspire Dashboard**: URL shown in terminal output

For first-time setup including secrets configuration and database initialization, see [**docs/DEVELOPMENT.md**](docs/DEVELOPMENT.md).

---

## Tech Stack

| Category | Technology |
|---|---|
| **Framework** | .NET 10, ASP.NET Core MVC & Minimal APIs |
| **Orchestration** | .NET Aspire (service discovery, telemetry, dashboard) |
| **Database** | SQLite (local dev), Azure SQL Serverless (production) |
| **ORM** | Entity Framework Core 10 (Code-First, provider-specific migrations) |
| **Authentication** | ASP.NET Core Identity, Google OAuth 2.0, Brevo email verification |
| **API Client** | Refit (typed HTTP client) + Polly (resilience) |
| **Frontend** | Bootstrap 5, DataTables, Chart.js, jQuery |
| **Logging** | Serilog with `AnsiConsoleTheme.Code` |
| **Observability** | OpenTelemetry (metrics, tracing, health checks) |
| **Containerization** | Multi-stage Dockerfiles, chiseled non-root images |
| **CI/CD** | GitHub Actions, GHCR, Azure OIDC (passwordless) |
| **Infrastructure** | Azure Bicep IaC, Container Apps (Consumption, scale-to-zero) |
| **Testing** | xUnit, Moq, FluentAssertions (104+ tests) |

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
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=normal"
```

The test suite covers URL parsing, MIME type resolution, budget CSV analytics, persistence caching, controller actions, external authentication, email templates, service defaults, and CI/CD pipeline validation.

---

## Contributing

Contributions are welcome. For significant changes, open an issue first to discuss the proposed approach.

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes: `git commit -m "Add my feature"`
4. Push to the branch: `git push origin feature/my-feature`
5. Open a Pull Request against the `main` branch.

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.