# Personal Finance App

A modular personal finance management application built on .NET 10, featuring a clean architecture split across API services, a modern web frontend, and a dedicated data access layer.

---

## Architecture Overview

The solution is organized into focused, decoupled projects:

| Project | Role & Description |
|---|---|
| **`PersonalFinance.Data`** | Database schema, EF Core `AppDbContext`, domain entities, and migration definitions. |
| **`PersonalFinance.ApiService`** | Backend RESTful API offering CRUD operations, OpenAPI spec generation, and interactive Scalar UI (`/scalar/v1`). |
| **`PersonalFinance.Web`** | ASP.NET Core MVC and Razor Pages frontend consuming API endpoints via type-safe Refit client with resilience pipelines and ASP.NET Core Identity authentication. |
| **`PersonalFinance.Shared`** | Shared DTOs, API contracts (`IItemsApi`), exceptions, and application constants. |

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

### 1. Run ApiService
```bash
dotnet run --project PersonalFinance/src/PersonalFinance.ApiService
```
- **Scalar API UI**: `https://localhost:7100/scalar/v1`
- **OpenAPI Document**: `https://localhost:7100/openapi/v1.json`
- **Health Check**: `https://localhost:7100/health`

### 2. Run Web Frontend
```bash
dotnet run --project PersonalFinance/src/PersonalFinance.Web
```
- **Web App**: `https://localhost:7200`
- **Health Check**: `https://localhost:7200/health`

---

## Logging & Observability

- **Serilog**: Structured logging configured in both services with console and enricher support.
- **Health Checks**: Standard `/health` endpoints enabled for uptime monitoring and orchestrator probes.
- **Resilience Pipelines**: Refit HTTP client configured with standard exponential retry and circuit-breaker policies via `Microsoft.Extensions.Http.Resilience`.