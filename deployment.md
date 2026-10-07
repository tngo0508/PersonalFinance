# Personal Finance Application - Deployment Guide

This guide provides deterministic, step-by-step instructions for deploying the **Personal Finance** application across multiple environments. The architecture is engineered for **zero operating cost** using Microsoft Azure's lifetime free tiers (Azure Container Apps Consumption, GitHub Container Registry, Log Analytics free 5GB/mo, and optional Azure SQL Database Serverless Free Tier) alongside local containerized options.

---

## 1. Architecture & Deployment Overview

This section is the "big picture": what gets built, where it runs, how a commit becomes a running container, and how a request flows through the system at runtime. Every diagram below is derived from the actual pipeline (`.github/workflows/deploy-azure.yml`) and infrastructure code (`infra/main.bicep` + `infra/modules/*.bicep`), so it doubles as a map for reading the source.

> **How to read the diagrams**: they are [Mermaid](https://mermaid.js.org/) diagrams and render automatically on GitHub and in most Markdown previewers (VS Code, Rider). Solid arrows are runtime traffic; dashed arrows are build/deploy-time actions.

### 1.1 The Big Picture (System Context)

```mermaid
flowchart LR
    dev(["👩‍💻 Developer"])
    user(["🧑 End user<br/>(browser)"])

    subgraph GH["GitHub"]
        repo[("Repository<br/>tngo0508/PersonalFinanceApp")]
        actions["GitHub Actions<br/>deploy-azure.yml"]
        secrets[["Repository secrets<br/>&amp; variables"]]
        ghcr[("GitHub Container Registry<br/>ghcr.io/&lt;owner&gt;/personalfinance-api<br/>ghcr.io/&lt;owner&gt;/personalfinance-web")]
    end

    entra["Microsoft Entra ID<br/>App registration +<br/>federated credential (OIDC)"]

    subgraph AZ["Azure subscription"]
        subgraph RG["Resource group: rg-personalfinance-prod"]
            law[("Log Analytics workspace<br/>law-pf-prod-*")]
            subgraph CAE["Container Apps environment: cae-pf-prod-* (Consumption)"]
                web["Container App: web-pf-prod-*<br/>PersonalFinance.Web<br/>ASP.NET Core MVC + Identity<br/>external HTTPS ingress"]
                api["Container App: api-pf-prod-*<br/>PersonalFinance.ApiService<br/>REST API (controllers)<br/>ingress: see 1.10"]
            end
            subgraph SQLS["Azure SQL logical server: sql-pf-prod-* (westus2)"]
                sqldb[("Database: PersonalFinance<br/>Serverless GP_S_Gen5_1<br/>free offer, auto-pause")]
            end
        end
    end

    subgraph EXT["External SaaS"]
        goauth["Google OAuth 2.0<br/>(sign-in)"]
        gdrive["Google Drive API v3<br/>(files, sheet export)"]
        brevo["Brevo REST API<br/>(transactional email)"]
    end

    dev -. "git push / PR / manual run" .-> repo
    repo -. "triggers" .-> actions
    secrets -. "injected at deploy" .-> actions
    actions -. "docker push (sha + latest tags)" .-> ghcr
    actions -. "OIDC token exchange" .-> entra
    actions -. "az deployment group create<br/>(Bicep)" .-> RG
    ghcr -. "image pull" .-> CAE

    user -- "HTTPS" --> web
    web -- "Refit HTTP client<br/>/api/googledrive/*" --> api
    web -- "Identity, data-protection keys<br/>(EF Core)" --> sqldb
    api -- "Drive cache, connections<br/>(EF Core)" --> sqldb
    api -- "HTTPS + API key" --> gdrive
    web -- "OAuth redirect flow" --> goauth
    web -- "confirmation / reset emails" --> brevo
    CAE -- "console logs" --> law
```

**In one sentence**: a push to `main` builds and tests the .NET 10 solution, packages two Docker images into GHCR, signs in to Azure *without a stored password* (OIDC), deploys the Bicep templates that describe the whole environment, and finally smoke-tests the public site.

### 1.2 Solution Projects and What Runs Where

```mermaid
flowchart TB
    subgraph deployed["Deployed as containers"]
        web["PersonalFinance.Web<br/>MVC views, Identity UI,<br/>Refit client"]
        api["PersonalFinance.ApiService<br/>API controllers, Google Drive service,<br/>OpenAPI + Scalar"]
    end
    subgraph libs["Class libraries (compiled into both images)"]
        data["PersonalFinance.Data<br/>AppDbContext, migrations,<br/>MigrateAndSeedDatabaseAsync"]
        shared["PersonalFinance.Shared<br/>DTOs, IGoogleDriveApi (Refit contract),<br/>GoogleDriveHelper"]
        sd["PersonalFinance.ServiceDefaults<br/>OpenTelemetry, /health, /alive,<br/>resilience, service discovery"]
    end
    subgraph localonly["Local development only"]
        apphost["PersonalFinance.AppHost<br/>.NET Aspire orchestrator"]
        tests["PersonalFinance.Tests<br/>xUnit (run in CI)"]
    end

    web --> data & shared & sd
    api --> data & shared & sd
    apphost -. "starts &amp; wires" .-> web & api
    tests -. "tests" .-> web & api & data & shared
```

| Project | Runs in Azure? | Role |
|---|---|---|
| `PersonalFinance.Web` | ✅ Container App `web-*` | The only public entry point. Server-rendered MVC + Razor Pages (Identity), cookie authentication, Google sign-in, Brevo email, and a typed **Refit** client (`IGoogleDriveApi`) that calls the API. |
| `PersonalFinance.ApiService` | ✅ Container App `api-*` | Business logic for Google Drive: explore folders, persist connections and cached file metadata, export spreadsheets and build budget reports. Serves OpenAPI/Scalar when `ApiDocs__Enabled=true`. |
| `PersonalFinance.Data` | Inside both images | EF Core `AppDbContext` (Identity tables + Drive tables), SQL Server migrations, and the startup migration/seed routine both apps call. |
| `PersonalFinance.Shared` | Inside both images | Contracts shared by both sides: DTOs and the Refit interface, so the client and server cannot drift apart silently. |
| `PersonalFinance.ServiceDefaults` | Inside both images | Aspire "service defaults": OpenTelemetry, `/health` + `/alive` endpoints used by the Container Apps probes, HTTP resilience. |
| `PersonalFinance.AppHost` | ❌ local only | `dotnet run` it to start both apps together with the Aspire dashboard. In Azure, Bicep replaces it (it wires the same `services__apiservice__*` settings). |
| `PersonalFinance.Tests` | ❌ CI only | Unit/integration tests executed by the `build-and-test` job. |

### 1.3 The CI/CD Pipeline

```mermaid
flowchart TD
    trigger{{"Trigger"}}
    trigger -->|"pull_request → main"| bt
    trigger -->|"push → main"| bt
    trigger -->|"workflow_dispatch<br/>(environment, deploy_sql, dry_run)"| bt

    subgraph job1["Job 1 · build-and-test (≤15 min)"]
        bt["checkout → setup .NET 10 →<br/>dotnet restore → build (Release) →<br/>dotnet test → az bicep build"]
    end

    subgraph job2["Job 2 · build-and-push-containers (≤20 min)"]
        img["Buildx multi-stage builds<br/>ApiService + Web Dockerfiles<br/>(GitHub Actions layer cache)"]
        push{"PR or dry_run?"}
        img --> push
        push -->|"yes"| nopush["Build only<br/>(validates Dockerfiles)"]
        push -->|"no"| pushed["Push to GHCR<br/>tags: &lt;short-sha&gt;, latest"]
    end

    subgraph job3["Job 3 · deploy-azure (≤40 min) · push / dispatch only, not dry_run"]
        login["azure/login (OIDC)"]
        rg["Ensure resource group exists<br/>(reuse if present)"]
        wait["Wait if old Container Apps<br/>environment is being deleted"]
        val["Validate DB secrets:<br/>SQL_ADMIN_PASSWORD or<br/>CUSTOM_CONNECTION_STRING"]
        bicep["azure/arm-deploy → infra/main.bicep<br/>images pinned to &lt;short-sha&gt;"]
        diag["On failure: dump deployment<br/>operations per module"]
        login --> rg --> wait --> val --> bicep
        bicep -. "failure()" .-> diag
    end

    subgraph job4["Job 4 · smoke-test (≤10 min)"]
        smoke["curl the Web URL from deploy outputs:<br/>/health (30 retries, cold start) →<br/>/alive → / (200/302) →<br/>/Identity/Account/Login"]
    end

    bt --> img
    pushed --> login
    bicep -->|"outputs: webUrl, webFqdn, apiFqdn"| smoke
```

| Trigger | Build & test | Build images | Push to GHCR | Deploy to Azure | Smoke test |
|---|:-:|:-:|:-:|:-:|:-:|
| Pull request to `main` | ✅ | ✅ | ❌ | ❌ | ❌ |
| Push / merge to `main` | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manual (`workflow_dispatch`) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manual with `dry_run = true` | ✅ | ✅ | ❌ | ❌ | ❌ |

> **Note**: `dry_run` skips pushing and deploying; it does not run an Azure What-If. For a What-If preview use `.\infra\deploy.ps1 -WhatIf` (Section 4).

**Why it is built this way (things worth learning)**:
- **Fail fast, cheapest first**: tests and `az bicep build` run before any image is built or anything touches Azure.
- **Immutable deployments**: the deploy step uses the `<short-sha>` image tag, never `latest`, so every Container App revision maps to exactly one commit and a rollback is "redeploy the previous SHA".
- **PRs validate everything except the deploy**: Dockerfiles are still built on PRs, so a broken image is caught before merge.
- **No long-lived cloud credentials**: GitHub requests a short-lived OIDC token, and Entra ID trusts it because of the federated credential configured in Step 1 below.

### 1.4 What Happens Inside `deploy-azure`

```mermaid
sequenceDiagram
    autonumber
    participant R as GitHub runner
    participant E as Entra ID
    participant ARM as Azure Resource Manager
    participant LAW as Log Analytics
    participant CAE as Container Apps env
    participant SQL as Azure SQL
    participant API as api-* app
    participant WEB as web-* app
    participant G as GHCR

    R->>E: OIDC token (repo + branch claims)
    E-->>R: Azure access token (Contributor role)
    R->>ARM: az group exists / create rg-personalfinance-prod
    R->>ARM: arm-deploy infra/main.bicep (params + @secure secrets)
    ARM->>LAW: module logAnalyticsDeployment
    ARM->>CAE: module containerEnvDeployment (needs workspace key)
    ARM->>SQL: module sqlDatabaseDeployment (if deploySqlDatabase)
    ARM->>API: module apiServiceDeployment (secrets: Drive key, connection string)
    API->>G: pull personalfinance-api:<sha>
    ARM->>WEB: module webServiceDeployment (needs API FQDN)
    WEB->>G: pull personalfinance-web:<sha>
    Note over API,WEB: New revision starts → startup probe /alive →<br/>EF Core migrations run → readiness probe /health
    ARM-->>R: outputs webUrl, webFqdn, apiFqdn, sqlServerFqdn
```

The Bicep modules and their dependencies (ARM works out the order from these references and runs independent modules in parallel):

```mermaid
flowchart LR
    main["infra/main.bicep"]
    la["log-analytics.bicep<br/>PerGB2018, 30-day retention"]
    env["container-env.bicep<br/>Consumption workload profile"]
    sql["sql-database.bicep<br/>server + firewall + DB<br/>(optional)"]
    apim["api-service.bicep<br/>0.25 vCPU / 0.5 Gi, 0–1 replicas"]
    webm["web-service.bicep<br/>0.25 vCPU / 0.5 Gi, 0–1 replicas"]

    main --> la & env & sql & apim & webm
    la -- "workspace id" --> env
    env -- "environment id" --> apim & webm
    sql -- "server FQDN → connection string" --> apim & webm
    apim -- "app name + FQDN" --> webm
```

| Resource | Bicep module | Name pattern | Cost model |
|---|---|---|---|
| Log Analytics workspace | `log-analytics.bicep` | `law-<env>-<hash>` | First 5 GB/month ingestion free |
| Container Apps environment | `container-env.bicep` | `cae-<env>-<hash>` | No charge itself (Consumption profile) |
| Container App (API) | `api-service.bicep` | `api-<env>-<hash>` | Consumption; scales to zero; monthly free grant |
| Container App (Web) | `web-service.bicep` | `web-<env>-<hash>` | Consumption; scales to zero; monthly free grant |
| Azure SQL server + database | `sql-database.bicep` | `sql-<env>-<hash>` / `PersonalFinance` | Serverless `GP_S_Gen5_1` with the free offer (auto-pause) |
| Container images | *(GitHub)* | `ghcr.io/<owner>/personalfinance-{api,web}` | Free for public packages |

`<hash>` is `uniqueString(resourceGroup().id, location)`, which gives names that are stable across redeploys and unique per resource group. The SQL server hashes `sqlLocation` instead, because SQL server names are tied to their region.

### 1.5 Runtime Request Flow (Example: Opening a Budget Report)

```mermaid
sequenceDiagram
    autonumber
    actor U as Browser
    participant W as Web (MVC)
    participant DB as Azure SQL
    participant A as ApiService
    participant D as Google Drive API

    U->>W: GET /GoogleDrive (auth cookie)
    W->>DB: Identity: validate user / security stamp
    W->>A: Refit GET /api/googledrive/connections?userId=…
    A->>DB: load connections + cached file metadata
    A-->>W: JSON (DTOs from PersonalFinance.Shared)
    W-->>U: Razor view + /js/google-drive/*.js
    U->>W: XHR GET /GoogleDrive/MonthlyBudgetReport?fileId=…
    W->>A: Refit GET /api/googledrive/spreadsheet-report
    A->>D: export sheet as .xlsx (fallback: CSV) with API key
    D-->>A: spreadsheet bytes
    A->>A: parse sheets, aggregate months & categories
    A-->>W: MonthlyBudgetReportDto
    W-->>U: JSON → budget-report.js renders tiles, charts, meters
```

Key points:
- **The browser only ever talks to Web.** Web is a backend-for-frontend: it owns authentication, then calls the API server-to-server.
- **The database is shared.** Both apps use the same `AppDbContext` and the same Azure SQL database (Identity tables plus Google Drive tables).
- **The serverless database auto-pauses when idle.** The first query after a pause can take up to about a minute; `EnableRetryOnFailure` in `PersonalFinance.Data` absorbs it.

### 1.6 Configuration & Secrets Flow

A secret is never written into an image or committed to Git. It travels like this:

```mermaid
flowchart LR
    s1[["GitHub secret<br/>e.g. BREVO_API_KEY"]]
    s2["arm-deploy parameter<br/>brevoApiKey=***"]
    s3["Bicep @secure() param<br/>(redacted in deployment history)"]
    s4["Container App secret<br/>brevo-api-key"]
    s5["Env var with secretRef<br/>Brevo__ApiKey"]
    s6["IConfiguration key<br/>Brevo:ApiKey → BrevoOptions"]
    s1 --> s2 --> s3 --> s4 --> s5 --> s6
```

`__` (double underscore) in an environment variable name becomes `:` in .NET configuration, so `ConnectionStrings__DefaultConnection` is read by `GetConnectionString("DefaultConnection")`.

| GitHub secret / variable | Container App secret | Env var in container | Used by |
|---|---|---|---|
| `SQL_ADMIN_PASSWORD` *(builds the connection string)* or `CUSTOM_CONNECTION_STRING` | `db-connection-string` | `ConnectionStrings__DefaultConnection` | Web + API |
| `GOOGLE_DRIVE_API_KEY` | `google-drive-api-key` | `GoogleDrive__ApiKey` | API |
| `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` | `google-client-id` / `google-client-secret` | `Authentication__Google__ClientId` / `__ClientSecret` | Web |
| `BREVO_API_KEY` | `brevo-api-key` | `Brevo__ApiKey` | Web |
| `vars.BREVO_SENDER_EMAIL` / `vars.BREVO_SENDER_NAME` | *(plain env)* | `Brevo__SenderEmail` / `Brevo__SenderName` | Web |
| *(computed by Bicep)* | *(plain env)* | `ApiSettings__BaseUrl=https://<api FQDN>`, `services__apiservice__http__0` | Web → API |
| `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` | — | — | Pipeline login only (OIDC) |

Locally the same keys come from **.NET User Secrets** (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" …`), so the code reads configuration the same way in every environment.

### 1.7 Startup, Health and Scale-to-Zero Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Zero: no traffic
    Zero --> Starting: first HTTP request arrives
    Starting --> Migrating: startup probe /alive passes
    Migrating --> Ready: EF Core migrations + seed complete,<br/>readiness probe /health passes
    Ready --> Ready: liveness /alive every 15s
    Ready --> Zero: idle → scaled to 0 replicas
    Ready --> Starting: new revision deployed
```

- **Scale rule**: HTTP, 100 concurrent requests per replica, `minReplicas: 0`, `maxReplicas: 1`. With zero replicas the app costs nothing, but the first request waits for a cold start; the smoke test retries `/health` up to 30 times for this reason.
- **Migrations at startup**: both `Program.cs` files call `MigrateAndSeedDatabaseAsync()`, so a deploy that adds a migration applies it when the new revision starts. `--migrate-only` / `MIGRATE_ONLY=true` runs migrations and exits, for use as a one-off job.
- **Logs**: container stdout (Serilog console output) goes to the Log Analytics workspace (`ContainerAppConsoleLogs_CL`). See Section 8.6 for live tailing.

### 1.8 Environments at a Glance

| Environment | How you start it | Orchestration | Database | Secrets source |
|---|---|---|---|---|
| **Local (Aspire)** | `dotnet run --project PersonalFinance/src/PersonalFinance.AppHost` | .NET Aspire AppHost + dashboard | Dev Azure SQL from `infra/deploy-local-sql.ps1` (`rg-personalfinance-dev`) or a local SQL Server | .NET User Secrets |
| **Local (Docker)** | Section 6 | `docker network` with two containers | Any SQL Server reachable from Docker, passed via `ConnectionStrings__DefaultConnection` | `-e` environment variables |
| **Azure (production)** | Push to `main` (Section 3), `deploy.ps1` (Section 4) or `azd up` (Section 5) | Azure Container Apps | Serverless Azure SQL, or `CUSTOM_CONNECTION_STRING` | GitHub secrets → Container App secrets |
| **Tests** | `dotnet test` | — | EF Core InMemory provider | — |

### 1.9 Deployment Modality Selection Matrix

| Deployment Method | Target Environment | Best For | Ingress & Networking | Database Tier |
|---|---|---|---|---|
| **Option 1: GitHub Actions CI/CD (Recommended)** | Azure Container Apps | Automated production deployments, continuous delivery on push to `main` or manual trigger. | Web: External<br/>API: see 1.10 | Azure SQL Serverless Free, or custom connection string |
| **Option 2: PowerShell / Azure CLI (`deploy.ps1`)** | Azure Container Apps | Direct terminal control, parameter tweaking, dry-run validation (`-ValidateOnly`, `-WhatIf`). | Web: External<br/>API: Internal | Azure SQL Serverless Free (`-DeploySqlDatabase`), or custom connection string |
| **Option 3: Azure Developer CLI (`azd`)** | Azure Container Apps | Standardized developer workflows and single-command provisioning (`azd up`). | Web: External<br/>API: Internal | Azure SQL Serverless Free, or custom connection string |
| **Option 4: Local Multi-Stage Docker** | Local machine (Docker) | Pre-commit validation and isolated local integration testing. | Localhost ports `8080` (Web) & `8081` (API) | Any reachable SQL Server (e.g. the dev Azure SQL database) |

### 1.10 Security Posture and Known Gaps

What the design already does well:
- **Passwordless CI/CD**: OIDC federation, so no Azure secret is stored in GitHub.
- **Secrets stay out of code and images**: secure Bicep params, then Container App secrets, then `secretRef` env vars.
- **HTTPS-only ingress** (`allowInsecure: false`), and the Web app trusts `X-Forwarded-*` from the Container Apps proxy.
- **Hardened images**: chiseled, non-root (`USER $APP_UID`) runtime images built in multiple stages.
- **Web app protections**: Identity with confirmed accounts and strong passwords, secure/HttpOnly cookies, global antiforgery validation, and data-protection keys stored in SQL so sign-ins survive restarts.

Gaps to understand (and fix before handling real financial data):

| Gap | Where | Why it matters | Typical fix |
|---|---|---|---|
| **API is public and unauthenticated in CI deployments** | `deploy-azure.yml` passes `exposeApiPublicly=true` (added for testing); the API trusts the `userId` it is given | Anyone who finds the `api-*` URL can call it and read or modify other users' Drive connections | Set `exposeApiPublicly=false` (internal ingress) and/or require a token from Web (managed identity or a JWT) |
| **SQL admin login in the connection string** | `main.bicep` builds the connection string with `sqladmin` + password | One shared admin credential is used by both apps | Microsoft Entra authentication with the Container Apps' managed identities (`Authentication=Active Directory Managed Identity`) |
| **SQL firewall allows all Azure IPs** | `sql-database.bicep` rule `AllowAllWindowsAzureIps` (0.0.0.0) | Any Azure-hosted service (any tenant) can reach the server's login endpoint | Private endpoint + VNet-integrated Container Apps environment, or narrow outbound-IP rules |
| **Broad pipeline permissions** | Step 1 allows assigning *Contributor* at subscription scope | A compromised workflow could change anything in the subscription | Scope the role to `rg-personalfinance-prod` (pre-create the group, since creating a group needs subscription-level rights) |

### 1.11 Concepts This Solution Teaches

- **Infrastructure as Code (Bicep)**: the whole environment can be recreated from `infra/` in minutes; modules, `@secure()` params, outputs and conditional modules (`if (deploySqlDatabase)`).
- **GitHub Actions**: job graphs (`needs`), conditional jobs (`if:`), job outputs passed between jobs, timeouts, and OIDC (`id-token: write`).
- **Containers**: multi-stage Dockerfiles, layer caching, non-root chiseled images, immutable SHA tags.
- **Azure Container Apps**: environments, revisions, ingress (external vs internal), probes, HTTP scale rules and scale-to-zero.
- **.NET Aspire**: one set of service defaults (telemetry, health, resilience) used by both the local AppHost and the cloud deployment.
- **Backend-for-frontend**: an MVC app that owns authentication and calls an internal API through a shared, typed Refit contract.
- **12-factor configuration**: the same code is configured per environment through environment variables and User Secrets.
- **Serverless data**: cost-optimized Azure SQL with auto-pause, and resilient connections that tolerate cold starts.

---

## 2. Prerequisites & Secrets Matrix

### Required Tools & Software

| Tool | Minimum Version | Installation Command / Link | Purpose |
|---|---|---|---|
| **.NET SDK** | `10.0.100`+ | [dot.net/download](https://dotnet.microsoft.com/download/dotnet/10.0) | Local compilation & testing |
| **Azure CLI (`az`)** | `2.60.0`+ | `winget install Microsoft.AzureCLI` or [aka.ms/installazurecliwindows](https://aka.ms/installazurecliwindows) | Azure management and Bicep execution |
| **Bicep CLI** | `0.30.0`+ | `az bicep install` / `az bicep upgrade` | Infrastructure-as-Code compilation |
| **Docker Desktop / Engine** | `24.0.0`+ | [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop) | Local container builds and execution |
| **Azure Developer CLI (`azd`)** *(Optional)* | `1.9.0`+ | `winget install Microsoft.Azd` or [aka.ms/install-azd](https://aka.ms/install-azd) | `azd up` deployments |

---

### External Services & Secrets Matrix

The application integrates with external identity and communication services. Configure these credentials before deploying:

| Secret Name | GitHub Secret Key | Bicep / Script Parameter | Required? | Source / Configuration Details |
|---|---|---|---|---|
| **Azure Client ID** | `AZURE_CLIENT_ID` | N/A (OIDC) | Yes (CI/CD) | Client ID of Azure App Registration / User-Assigned Managed Identity. |
| **Azure Tenant ID** | `AZURE_TENANT_ID` | N/A (OIDC) | Yes (CI/CD) | Directory / Tenant ID of Microsoft Entra ID. |
| **Azure Subscription ID** | `AZURE_SUBSCRIPTION_ID` | `-SubscriptionId` | Yes | Target Azure Subscription identifier. |
| **Google OAuth Client ID** | `GOOGLE_CLIENT_ID` | `-GoogleClientId` | Optional | Google Cloud Console OAuth 2.0 Web Client ID for social sign-in. |
| **Google OAuth Client Secret** | `GOOGLE_CLIENT_SECRET` | `-GoogleClientSecret` | Optional | Google Cloud Console OAuth 2.0 Web Client Secret. |
| **Brevo REST API Key** | `BREVO_API_KEY` | `-BrevoApiKey` | Optional | API Key from Brevo Dashboard (for account registration verification emails). |
| **Google Drive API Key** | `GOOGLE_DRIVE_API_KEY` | `-GoogleDriveApiKey` | Optional | Google Cloud Console API Key with Google Drive API v3 enabled. |
| **Custom Connection String** | `CUSTOM_CONNECTION_STRING` | `-CustomConnectionString` | Optional | Connection string to existing SQL Server or mounted storage database. |

---

## 3. Deployment Method 1: GitHub Actions CI/CD (Recommended)

The repository provides a complete automated CI/CD pipeline in `.github/workflows/deploy-azure.yml`. It handles:
1. Automated .NET 10 compilation and test suite execution.
2. Bicep template syntax and ARM schema validation.
3. Multi-stage Docker image builds published to GitHub Container Registry (`ghcr.io`).
4. Passwordless Azure authentication using OpenID Connect (OIDC).
5. Idempotent infrastructure rollout to Azure Container Apps.
6. Automated post-deployment HTTP smoke testing against `/health`, `/alive`, and the login UI.

---

### Step 1: Configure Azure OIDC Federated Credentials

Configure passwordless GitHub Actions authentication to eliminate the need for long-lived secrets:

#### 1. Create App Registration in Azure Portal
1.  Navigate to **Microsoft Entra ID** &rarr; **App registrations** &rarr; **New registration**.
2.  **Name**: `personalfinance-github-actions`
3.  **Supported account types**: **Accounts in this organizational directory only**.
4.  Click **Register**.
5.  On the Overview page, record:
    *   **Application (client) ID** (This becomes `AZURE_CLIENT_ID` in GitHub)
    *   **Directory (tenant) ID** (This becomes `AZURE_TENANT_ID` in GitHub)

#### 2. Assign Contributor Role
1.  Navigate to your Azure Subscription (or the target Resource Group `rg-personalfinance-prod`).
2.  Select **Access control (IAM)** &rarr; **Add** &rarr; **Add role assignment**.
3.  **Role**: **Contributor**.
4.  **Assign access to**: **User, group, or service principal**.
5.  Click **+ Select members**, search for `personalfinance-github-actions`, select it, and click **Select**.
6.  Click **Review + assign**.

#### 3. Add Federated Credential for GitHub Actions
1.  In your App Registration (`personalfinance-github-actions`), select **Certificates & secrets** &rarr; **Federated credentials** &rarr; **Add credential**.
2.  **Federated credential scenario**: **GitHub Actions deploying Azure resources**.
3.  **Organization**: Your GitHub username/organization (e.g., `tngo0508`).
4.  **Repository**: `PersonalFinanceApp`.
5.  **Entity type**: **Branch**.
6.  **GitHub branch**: `main`.
7.  **Name**: `main-branch-deploy`.
8.  Click **Add**.

*Note: For advanced users, equivalent CLI commands are available in `DEVELOPMENT.md`.*

You will also need your **Subscription ID** (found on the Azure Portal Subscriptions blade) as `AZURE_SUBSCRIPTION_ID` in GitHub.

---

### Step 2: Configure Google Cloud OAuth and Google Drive API Credentials

The application supports optional **Google OAuth 2.0 social sign-in** and **Google Drive API integration** for budget report analytics. Both are optional; if not configured, the application runs without these features.

This step creates two separate credentials in Google Cloud Console:
1. **OAuth 2.0 Web Client** (`GOOGLE_CLIENT_ID` & `GOOGLE_CLIENT_SECRET`) — for user sign-in via Google account.
2. **Google Drive API Key** (`GOOGLE_DRIVE_API_KEY`) — for querying Google Drive folders without user login.

#### 1. Create a Google Cloud Project

1. Navigate to [Google Cloud Console](https://console.cloud.google.com/) and sign in with your Google account.
2. At the top of the page, click the **Project dropdown** (next to the Google Cloud logo).
3. Click **New Project**.
4. Enter a **Project Name** (e.g., `PersonalFinance-Prod`) and click **Create**.
5. Wait for the project to be created, then ensure it is selected in the project dropdown.

#### 2. Configure the OAuth Consent Screen

Before creating OAuth credentials, you must configure the consent screen that users see during sign-in:

1. In the left sidebar, navigate to **APIs & Services** &rarr; **OAuth consent screen** (or visit [console.cloud.google.com/apis/credentials/consent](https://console.cloud.google.com/apis/credentials/consent)).
2. Select **External** as the user type and click **Create**.
3. Fill in the required application information:
   - **App name**: `Personal Finance`
   - **User support email**: Your email address (e.g., `your-email@gmail.com`)
   - **Developer contact information**: Your email address
4. Click **Save and Continue**.
5. On the **Scopes** page, click **Add or Remove Scopes**.
6. Search for and select the following scopes:
   - `openid`
   - `.../auth/userinfo.email`
   - `.../auth/userinfo.profile`
7. Click **Update** and then **Save and Continue**.
8. On the **Test users** page, click **Add users** and add your Gmail account (and any test accounts you plan to use).
9. Click **Save and Continue**, then **Back to Dashboard**.

#### 3. Create OAuth 2.0 Web Client Credentials

1. In the left sidebar, navigate to **APIs & Services** &rarr; **Credentials** (or visit [console.cloud.google.com/apis/credentials](https://console.cloud.google.com/apis/credentials)).
2. Click **+ CREATE CREDENTIALS** at the top and select **OAuth client ID**.
3. Set **Application type** to **Web application**.
4. Set **Name** to `PersonalFinance Web App`.
5. Under **Authorized JavaScript origins**, add:
   - `https://localhost:7200` (local development HTTPS)
   - `http://localhost:5200` (local development HTTP)
   - `https://<your-deployed-web-fqdn>` (example placeholder: `https://web.yellowmeadow-123.eastus.azurecontainerapps.io`; replace it with your actual hostname after the first deployment)

   > ⚠️ **Important**: Enter only the **base origin** (scheme + host + port). Do NOT include `/signin-google` or any path.

6. Under **Authorized redirect URIs**, add:
   - `https://localhost:7200/signin-google`
   - `http://localhost:5200/signin-google`
   - `https://<your-deployed-web-fqdn>/signin-google` (example placeholder: `https://web.yellowmeadow-123.eastus.azurecontainerapps.io/signin-google`; replace it with your actual hostname after the first deployment)

   > ⚠️ **Important**: The redirect URI **must** end with `/signin-google` (the exact path where ASP.NET Core Identity handles the OAuth callback).

7. Click **CREATE**.
8. A dialog will display your credentials. **Copy and save**:
   - **Client ID** (e.g., `485689840313-abc123def456.apps.googleusercontent.com`)
   - **Client Secret** (e.g., `GOCSPX-abc123def456xyz`)

   These become `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` in GitHub Secrets.

#### 4. Enable Google Drive API and Create an API Key

1. In the left sidebar, navigate to **APIs & Services** &rarr; **Library** (or visit [console.cloud.google.com/apis/library](https://console.cloud.google.com/apis/library)).
2. Search for **Google Drive API** and click on it.
3. Click the blue **Enable** button.
4. Return to **APIs & Services** &rarr; **Credentials**.
5. Click **+ CREATE CREDENTIALS** and select **API key**.
6. A dialog will display your new API key (e.g., `AIzaSyDaGmWKa4JsXZ-HjGw7ISLn_3namBGewQe`).
7. **Copy and save** this key as `GOOGLE_DRIVE_API_KEY` in GitHub Secrets.

   > **Optional**: Click the **Edit** (pencil) icon next to your API key to restrict it to Google Drive API only and limit HTTP referrers to your deployed domain for security.

---

### Step 3: Generate Brevo Transactional Email API Key

Transactional emails (e.g., account confirmation) depend on an external email service. The application supports [Brevo](https://www.brevo.com/) as its transactional email provider. This is optional; if not configured, the application will not be able to send automated emails.

#### 1. Generate the Brevo v3 REST API Key

1.  Log in to your [Brevo Dashboard](https://app.brevo.com/).
2.  Click on your **Profile icon** (top right) &rarr; **SMTP & API**.
3.  Click on the **API Keys** tab.
4.  Click **Generate a new API key**.
5.  **Name**: Enter a name, such as `PersonalFinance-Prod`.
6.  Click **Generate**.
7.  **Important**: Copy the key immediately, as it will not be shown again.
8.  Save this as `BREVO_API_KEY` in GitHub Secrets (see the next step).

#### 2. Verify Sender Email Address

1.  In the Brevo Dashboard, navigate to **Senders & Domains** &rarr; **Senders**.
2.  Click **Add a sender**.
3.  Enter your Name and Email address.
4.  Brevo will send a verification email to that address. Follow the instructions in the email to verify your sender identity.
5.  This verified email and name are configured in your GitHub repository variables (`BREVO_SENDER_EMAIL` and `BREVO_SENDER_NAME`) to ensure correct email delivery.

---

### Step 4: Configure GitHub Repository Secrets and Variables

All deployment credentials and configuration are stored in GitHub Repository Secrets and Variables. This step configures both.

#### 4.1 Add Repository Secrets

Navigate to your GitHub repository **Settings** &rarr; **Secrets and variables** &rarr; **Actions** &rarr; **New repository secret** and create the following secrets:

**Required Azure OIDC Secrets** (for CI/CD authentication):

| Secret Name | Value |
|---|---|
| `AZURE_CLIENT_ID` | Application (client) ID from Step 1 |
| `AZURE_TENANT_ID` | Directory (tenant) ID from Step 1 |
| `AZURE_SUBSCRIPTION_ID` | Azure Subscription ID |

**Optional Application Integration Secrets** (leave empty if not configured):

| Secret Name | Value |
|---|---|
| `GOOGLE_CLIENT_ID` | Google OAuth Client ID from Step 2 |
| `GOOGLE_CLIENT_SECRET` | Google OAuth Client Secret from Step 2 |
| `GOOGLE_DRIVE_API_KEY` | Google Drive API Key from Step 2 |
| `BREVO_API_KEY` | Brevo v3 REST API Key from Step 3 |

**Conditional Database Secrets**:

| Secret Name | Value | When Required | Security & Secret Flow |
|---|---|---|---|
| `SQL_ADMIN_PASSWORD` | Strong password for Azure SQL admin account (`sqladmin`) | When `deploy_sql` input is `true` (Option A: Serverless Free Tier) | Passed to Bicep `@secure() param sqlAdministratorLoginPassword`. The connection string is generated dynamically by Bicep and stored securely in Container Apps secrets (`db-connection-string`). |
| `CUSTOM_CONNECTION_STRING` | Full connection string to existing/external SQL Server or mounted database | When `deploy_sql` input is `false` (Option B: Custom DB) | Passed to Bicep `@secure() param customConnectionString` and registered in Container Apps secrets (`db-connection-string`). |

##### Database Configuration Scenarios

- **Option A: Automated Azure SQL Serverless Free Tier ($0.00)**
  1. Generate a strong password meeting Azure SQL requirements (minimum 8 characters with uppercase, lowercase, numbers, and symbols).
  2. Add `SQL_ADMIN_PASSWORD` to GitHub Secrets.
  3. When running CI/CD, keep `deploy_sql: true`. Bicep provisions the SQL server, builds the encrypted connection string (`Encrypt=True;TrustServerCertificate=False;`), and mounts it via `secretRef: 'db-connection-string'` to environment variable `ConnectionStrings__DefaultConnection`.
- **Option B: Existing or External SQL Server / Managed Database**
  1. Construct the connection string with encryption enabled:
     ```text
     Server=tcp:<server-name>.database.windows.net,1433;Initial Catalog=PersonalFinance;Persist Security Info=False;User ID=<user>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
     ```
  2. Add `CUSTOM_CONNECTION_STRING` to GitHub Secrets.
  3. In workflow dispatch or Bicep params, set `deploy_sql: false`. The custom connection string is injected directly into Container Apps secrets.

#### 4.2 Add Repository Variables

Navigate to **Settings** &rarr; **Secrets and variables** &rarr; **Actions** &rarr; **New repository variable** and create the following variables:

| Variable Name | Default Value | Purpose |
|---|---|---|
| `BREVO_SENDER_EMAIL` | `tngo0508@gmail.com` | Verified sender email address for transactional emails (from Step 3). |
| `BREVO_SENDER_NAME` | `PersonalFinance` | Display name for transactional email sender. |
| `CUSTOM_DOMAIN_NAME` | *(empty)* | Optional custom domain name (e.g., `finance.mydomain.com`). Leave empty for auto-generated Azure Container Apps domain. |

> **Note**: If you do not create these variables, the workflow will use the default values shown above.

---

### Step 5: Trigger and Validate the CI/CD Deployment Pipeline

The repository provides an automated GitHub Actions workflow named **"Build, Test, and Deploy to Azure Container Apps"** (`.github/workflows/deploy-azure.yml`). This workflow:
1. Compiles and tests the .NET 10 application.
2. Builds and pushes Docker images to GitHub Container Registry (GHCR).
3. Deploys infrastructure and containers to Azure Container Apps using Bicep.
4. Runs automated smoke tests against the deployed endpoints.

#### 5.1 Understand Workflow Triggers and Behavior

The workflow is triggered in the following scenarios:

| Trigger | Behavior | Deployment? |
|---|---|---|
| **Push to `main` branch** | Automatically runs build, test suite, container publishing, and Azure deployment. | ✅ Yes (if tests pass) |
| **Pull Request to `main`** | Automatically runs build, test suite, container publishing, and Azure deployment. | ✅ Yes (if tests pass) |
| **Manual `workflow_dispatch`** | Triggered manually from the Actions tab with optional inputs. | ✅ Yes (unless `dry_run` is enabled) |

#### 5.2 Automatic Trigger (Push or Pull Request to `main`)

To trigger the workflow automatically, push a commit to the `main` branch or open a Pull Request targeting `main`:

```bash
# Example: Creating and pushing a PR branch
git checkout -b feature/my-update
git add .
git commit -m "Deploy update via PR"
git push origin feature/my-update
# Then create a Pull Request targeting main on GitHub
```

The workflow will start automatically on PR creation and subsequent commits. Monitor progress in the **Actions** tab.

#### 5.3 Manual Trigger via `workflow_dispatch`

For controlled deployments or testing, manually trigger the workflow:

1. In GitHub, open the **Actions** tab.
2. In the left sidebar, select **Build, Test, and Deploy to Azure Container Apps**.
3. Click the **Run workflow** button (top right).
4. Configure the optional inputs:
   - **`environment`** (string, default: `pf-prod`): Azure environment name prefix for resource naming.
   - **`deploy_sql`** (boolean, default: `true`): Set to `true` to provision Azure SQL Database Serverless Free Tier ($0.00). When enabled, you **must** provide `SQL_ADMIN_PASSWORD` secret.
   - **`dry_run`** (boolean, default: `false`): Set to `true` to validate infrastructure without deploying (skips container push and Azure deployment).
5. Click **Run workflow**.

#### 5.4 Monitor Workflow Execution

1. The workflow runs in the **Actions** tab. Click the workflow run to view detailed logs.
2. The workflow has four main jobs:
   - **Build & Test (.NET 10)**: Compiles the solution and runs the test suite.
   - **Build & Push Containers (GHCR)**: Builds Docker images and pushes to GitHub Container Registry (skipped only when `dry_run` is enabled).
   - **Deploy to Azure Container Apps**: Authenticates via OIDC and deploys Bicep infrastructure (skipped only when `dry_run` is enabled).
   - **End-to-End Smoke Tests**: Validates deployed endpoints (skipped only when `dry_run` is enabled).

#### 5.5 Validate Deployment Success

After the workflow completes, verify successful deployment:

**Check Workflow Status**:
- For a standard deployment run (a push to `main`, a pull request targeting `main`, or a manual run with `dry_run` set to `false`), Build & Test, Build & Push Containers, Deploy to Azure Container Apps, and End-to-End Smoke Tests should all complete successfully.
- For a dry run (`dry_run` set to `true`), Build & Test and Build & Push Containers (build-only) complete, while Deploy to Azure Container Apps and End-to-End Smoke Tests show as skipped.
- If any job fails, click it to view error logs.

**Smoke Test Results**:
The workflow automatically tests the following endpoints on the deployed web application:

| Endpoint | Expected Result | Purpose |
|---|---|---|
| `/health` | HTTP 200 OK (with up to 30 retry attempts, 5-second intervals) | Readiness probe; validates application startup and database connectivity. |
| `/alive` | HTTP 200 OK | Liveness probe; validates application is running. |
| `/` | HTTP 200 OK or 302 (redirect) | Home page; validates web UI is accessible. |
| `/Identity/Account/Login` | HTTP 200 OK | Login page; validates ASP.NET Core Identity UI is rendered. |

> **Note**: The smoke test only probes the Web app. The API Service (`PersonalFinance.ApiService`) is internal-only by default, but the CI workflow currently deploys it with `exposeApiPublicly=true` (public ingress + Scalar UI, no authentication) for testing. See [1.10 Security Posture and Known Gaps](#110-security-posture-and-known-gaps).

**Verify in Azure Portal**:
1. Navigate to the Azure Portal and open your Resource Group (`rg-personalfinance-prod`).
2. Verify that the following resources are created:
   - **Container Apps Environment** (`cae-<environmentName>-<uniqueSuffix>`, for example `cae-pf-prod-<uniqueSuffix>`)
   - **Web Container App** (`web`)
   - **API Container App** (`apiservice`)
   - **Log Analytics Workspace** (for monitoring)
   - *(Optional)* **Azure SQL Database** (if `deploy_sql` was enabled)

**Test Application Features**:
1. Navigate to the deployed web application URL (shown in the workflow output or Azure Portal).
2. Test the following:
   - **Home page loads**: Verify the dashboard and UI render correctly.
   - **Google OAuth sign-in** (if configured): Click the "Sign in with Google" button and verify the OAuth flow completes.
   - **Account registration**: Create a new account and verify the confirmation email is sent (check your email inbox or Brevo dashboard logs).
   - **Budget reports** (if Google Drive API is configured): Verify the Google Drive integration works.

#### 5.6 Troubleshooting Common Issues

**OIDC Authentication Failure** (`AADSTS70021` error):
- Verify that the Federated Credential in the App Registration (Step 1) is configured for the `main` branch.
- Ensure `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` secrets are correctly set.
- Verify the App Registration has the `Contributor` role assigned at the Subscription or Resource Group scope.

**Deployment Fails with `MANIFEST_UNKNOWN` (Container Image Pull Error)**:
- **Image Tag Prefix**: Ensure the image tag generated by Docker Buildx matches the parameter passed to Bicep. `docker/metadata-action` defaults to prefix `sha-` unless overridden with `prefix=`.
- **GHCR Package Visibility**: By default, newly pushed packages in GitHub Container Registry are set to **Private**. For Azure Container Apps to pull zero-cost container images anonymously without credentials:
  1. In GitHub, go to your profile &rarr; **Packages** (or repository &rarr; **Packages**).
  2. Click on `personalfinance-api` and `personalfinance-web`.
  3. Go to **Package settings** &rarr; scroll to **Danger Zone** &rarr; **Change visibility** &rarr; select **Public** &rarr; confirm.

**Database Connection Failure (`Cannot assign requested address [::1]:1433` or `Error Number: 10049`)**:
- **Root Cause**: The application container inside Azure Container Apps attempted to connect to `[::1]:1433` (`localhost:1433`) because the database connection string was missing or empty in the Container App environment secrets.
- **Resolution**:
  1. Ensure the secret is added under **Repository secrets** (not Environment secrets): Navigate to **Settings** &rarr; **Secrets and variables** &rarr; **Actions** &rarr; **New repository secret**.
  2. For Option A (Azure SQL Serverless Free Tier): Add `SQL_ADMIN_PASSWORD` with a strong password (minimum 8 characters with uppercase, lowercase, numbers, and symbols).
  3. For Option B (Existing or External Database): Add `CUSTOM_CONNECTION_STRING` with your full encrypted connection string.
  4. Trigger a new workflow run in GitHub Actions.

**Smoke Tests Fail**:
- Check the workflow logs for the specific endpoint that failed.
- Verify the web application is running: navigate to the Azure Portal and check the Container App status.
- If `/health` times out, the application may be experiencing a cold-start delay or database connectivity issue. Check application logs in Log Analytics.

**Email Not Received**:
- Verify `BREVO_API_KEY` is correctly set in GitHub Secrets.
- Verify the sender email address is verified in the Brevo Dashboard (**Senders & Domains** &rarr; **Senders**).
- Check Brevo Dashboard logs for delivery failures.

**Google OAuth Not Working**:
- Verify `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are correctly set.
- Ensure the deployed web application URL is added to the OAuth 2.0 Web Client's **Authorized JavaScript origins** and **Authorized redirect URIs** in Google Cloud Console.
- Check the application logs for OAuth-related errors.

---

## 4. Deployment Method 2: Local PowerShell / Azure CLI (`infra/deploy.ps1`)

For direct command-line provisioning or customized parameter testing, the repository provides `infra/deploy.ps1`. The script automates Azure authentication checks, Resource Group provisioning, Bicep compilation, ARM template validation, and deployment execution.

### Script Parameter Reference

| Parameter | Type | Default Value | Description |
|---|---|---|---|
| `-SubscriptionId` | `string` | Current context | Target Azure Subscription ID. |
| `-ResourceGroupName` | `string` | `rg-personalfinance-prod` | Name of the Azure Resource Group. |
| `-Location` | `string` | `eastus` | Azure region for resources. |
| `-EnvironmentName` | `string` | `pf-prod` | Prefix for resource naming. |
| `-ApiImage` | `string` | `ghcr.io/tngo0508/personalfinance-api:latest` | Container image for API backend. |
| `-WebImage` | `string` | `ghcr.io/tngo0508/personalfinance-web:latest` | Container image for Web frontend. |
| `-GoogleClientId` | `string` | `""` | Google OAuth 2.0 Client ID. |
| `-GoogleClientSecret` | `string` | `""` | Google OAuth 2.0 Client Secret. |
| `-BrevoApiKey` | `string` | `""` | Brevo REST API Key. |
| `-BrevoSenderEmail` | `string` | `tngo0508@gmail.com` | Verified Brevo sender email. |
| `-BrevoSenderName` | `string` | `PersonalFinance` | Sender display name. |
| `-GoogleDriveApiKey` | `string` | `""` | Google Drive v3 API Key. |
| `-DeploySqlDatabase` | `switch` | `false` | Provision Azure SQL Database Serverless Free Tier ($0.00). |
| `-SqlAdminPassword` | `string` | `""` | Admin password for Azure SQL (required if `-DeploySqlDatabase` is enabled). |
| `-CustomConnectionString`| `string` | `""` | Custom SQL Server or storage connection string. |
| `-CustomDomainName` | `string` | `""` | Custom domain name (e.g. `finance.mydomain.com`). |
| `-ValidateOnly` | `switch` | `false` | Validate Bicep syntax and ARM schema without deploying resources. |
| `-WhatIf` | `switch` | `false` | Preview Azure infrastructure changes without applying them. |

---

### Step-by-Step CLI Execution

#### Step 1: Authenticate with Azure CLI
```powershell
# Authenticate interactive session
az login

# List subscriptions and set active target
az account list --output table
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"
```

#### Step 2: Validate Infrastructure Syntax (Dry-Run)
Before creating cloud resources, execute syntax and schema validation:
```powershell
.\infra\deploy.ps1 `
  -ResourceGroupName "rg-personalfinance-prod" `
  -Location "eastus" `
  -ValidateOnly
```

#### Step 3: Run What-If Deployment Preview
Preview the resource graph additions and modifications:
```powershell
.\infra\deploy.ps1 `
  -ResourceGroupName "rg-personalfinance-prod" `
  -Location "eastus" `
  -WhatIf
```

#### Step 4: Provision & Deploy Full Stack

##### Option A: Bring Your Own Database (Custom Connection String)
```powershell
.\infra\deploy.ps1 `
  -ResourceGroupName "rg-personalfinance-prod" `
  -Location "eastus" `
  -EnvironmentName "pf-prod" `
  -GoogleClientId "your-google-client-id.apps.googleusercontent.com" `
  -GoogleClientSecret "your-google-client-secret" `
  -BrevoApiKey "your-brevo-api-key" `
  -GoogleDriveApiKey "your-google-drive-key" `
  -CustomConnectionString "Server=tcp:<server>,1433;Initial Catalog=PersonalFinance;..."
```

> **Important**: pass `-DeploySqlDatabase` explicitly when you want Azure SQL provisioned (Option B). The script sends `$DeploySqlDatabase.IsPresent`, so the `= $true` default in its `param()` block has no effect: omitting the switch deploys **without** a database, and the apps then need `-CustomConnectionString`.

##### Option B: Azure SQL Database Serverless Free Tier Stack ($0.00)
```powershell
.\infra\deploy.ps1 `
  -ResourceGroupName "rg-personalfinance-prod" `
  -Location "eastus" `
  -EnvironmentName "pf-prod" `
  -GoogleClientId "your-google-client-id.apps.googleusercontent.com" `
  -GoogleClientSecret "your-google-client-secret" `
  -BrevoApiKey "your-brevo-api-key" `
  -GoogleDriveApiKey "your-google-drive-key" `
  -DeploySqlDatabase `
  -SqlAdminPassword "StrongPassword123!"
```

---

## 5. Deployment Method 3: Azure Developer CLI (`azd`)

The repository includes an `azure.yaml` template schema that maps directly to the Bicep modules in `infra/`:

### Step 1: Install & Authenticate `azd`
```powershell
# Install Azure Developer CLI (if not already present)
winget install Microsoft.Azd

# Authenticate with Azure
azd auth login
```

### Step 2: Initialize & Configure Environment
```powershell
# Initialize azd environment
azd env new personalfinance-prod

# Set environment parameters
azd env set AZURE_LOCATION eastus
azd env set GOOGLE_CLIENT_ID "your-google-client-id.apps.googleusercontent.com"
azd env set GOOGLE_CLIENT_SECRET "your-google-client-secret"
azd env set BREVO_API_KEY "your-brevo-api-key"
azd env set GOOGLE_DRIVE_API_KEY "your-google-drive-key"
```

### Step 3: Provision and Deploy Stack
```powershell
# Package, provision Bicep infrastructure, build containers, and deploy
azd up
```

To tear down or deprovision all resources created by `azd`:
```powershell
azd down --purge
```

---

## 6. Deployment Method 4: Local Multi-Container Docker

For local testing, CI runner validation, or developer verification without deploying to Azure, build and run both services using Docker.

### Container Architecture & Security
- Both containers need `ConnectionStrings__DefaultConnection` pointing at a SQL Server they can reach (for example the dev Azure SQL database from `infra/deploy-local-sql.ps1`); without it they fall back to `localhost`, which inside a container is the container itself.
- Built on .NET 10 SDK and multi-stage distroless runtime images (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`).
- Hardened non-root user execution (`USER $APP_UID`, UID `1654`).
- Granular restore layer caching utilizing `Directory.Packages.props` Central Package Management (CPM).

---

### Step 1: Build Container Images

Run builds from the **repository root directory**:

```bash
# Build PersonalFinance.ApiService image
docker build -t personalfinance-apiservice:latest -f PersonalFinance/src/PersonalFinance.ApiService/Dockerfile .

# Build PersonalFinance.Web image
docker build -t personalfinance-web:latest -f PersonalFinance/src/PersonalFinance.Web/Dockerfile .
```

---

### Step 2: Create Docker Network & Run Containers

```bash
# 1. Create a dedicated bridge network for inter-container communication
docker network create personalfinance-net

# 2. Start ApiService backend on port 8081
docker run -d \
  --name personalfinance-api \
  --network personalfinance-net \
  -p 8081:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="<SQL_SERVER_CONNECTION_STRING>" \
  -e GoogleDrive__ApiKey="<OPTIONAL_GOOGLE_DRIVE_KEY>" \
  personalfinance-apiservice:latest

# 3. Start Web frontend on port 8080
docker run -d \
  --name personalfinance-web \
  --network personalfinance-net \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ApiSettings__BaseUrl="http://personalfinance-api:8080" \
  -e services__apiservice__http__0="http://personalfinance-api:8080" \
  -e ConnectionStrings__DefaultConnection="<SQL_SERVER_CONNECTION_STRING>" \
  -e Authentication__Google__ClientId="<OPTIONAL_GOOGLE_CLIENT_ID>" \
  -e Authentication__Google__ClientSecret="<OPTIONAL_GOOGLE_CLIENT_SECRET>" \
  -e Brevo__ApiKey="<OPTIONAL_BREVO_KEY>" \
  personalfinance-web:latest
```

---

### Step 3: Verify Local Container Status

```bash
# Inspect running containers
docker ps --filter "name=personalfinance"

# Tail logs for Web service
docker logs -f personalfinance-web

# Tail logs for ApiService
docker logs -f personalfinance-api
```

Access local endpoints:
- **Web Application**: [http://localhost:8080](http://localhost:8080)
- **Web Health Probes**: [http://localhost:8080/health](http://localhost:8080/health) and [http://localhost:8080/alive](http://localhost:8080/alive)
- **API Documentation (Scalar)**: [http://localhost:8081/scalar/v1](http://localhost:8081/scalar/v1)

---

## 7. Post-Deployment Smoke Testing & Health Checks

Once deployment completes, validate operational readiness, network routing, and health probes.

### Automated Smoke Testing (`infra/smoke-test.ps1`)

The repository includes an automated verification script that tests readiness, liveness, and routing against the live Web endpoint:

```powershell
# Run smoke tests against deployed Azure Container App URL
.\infra\smoke-test.ps1 -WebUrl "https://web.yellowmeadow-123.eastus.azurecontainerapps.io"

# Custom retry count and interval for cold-start environments
.\infra\smoke-test.ps1 `
  -WebUrl "https://web.yellowmeadow-123.eastus.azurecontainerapps.io" `
  -MaxRetries 45 `
  -RetryDelaySeconds 5
```

### Probed Endpoints & SLA Validation

| Endpoint Path | Type | Expected Status | Purpose & SLA |
|---|---|---|---|
| `/health` | Readiness Probe | `200 OK` | Verifies full application readiness, database connectivity, and dependent service health. |
| `/alive` | Liveness Probe | `200 OK` | Verifies process responsiveness and runtime availability. |
| `/` | Landing / UI | `200 OK` or `302 Found` | Verifies MVC pipeline routing, layout rendering, and static assets. |
| `/Identity/Account/Login` | Identity UI | `200 OK` | Verifies ASP.NET Core Identity Razor Pages and external provider button discovery. |
| `/scalar/v1` *(ApiService)* | OpenAPI / Docs | `200 OK` | API documentation; reachable only when the API is exposed (`exposeApiPublicly=true`) or locally. |

### Manual Terminal Verification (cURL / PowerShell)

```bash
# Test Readiness Probe
curl -i https://<YOUR_WEB_URL>/health

# Test Liveness Probe
curl -i https://<YOUR_WEB_URL>/alive

# Inspect HTTP Response Headers & Cookies
curl -I https://<YOUR_WEB_URL>/Identity/Account/Login
```

---

## 8. Operational Troubleshooting & Diagnostics Runbook

Use the following runbook to diagnose and remediate common operational and deployment issues:

### 1. Azure OIDC Authentication Failure in GitHub Actions
- **Symptom**: GitHub Action fails during `azure/login@v2` with `AADSTS70021: No matching federated identity record found for subject...`.
- **Root Cause**: The repository name, organization, or branch in Azure Entra ID's Federated Credential does not match the GitHub workflow context.
- **Remediation**:
  1. Open Azure Portal &rarr; **Microsoft Entra ID** &rarr; **App registrations** &rarr; select your GitHub deployment app.
  2. Navigate to **Certificates & secrets** &rarr; **Federated credentials**.
  3. Ensure the Subject identifier matches: `repo:<org>/<repo>:ref:refs/heads/main` (or environment `repo:<org>/<repo>:environment:<env>`).

---

### 2. Container Cold-Start & Readiness Timeout
- **Symptom**: `smoke-test.ps1` returns HTTP 503 or connection timeout on the first few attempts immediately after deployment.
- **Root Cause**: Azure Container Apps Consumption profile scales instances to zero when idle. Waking from zero or resuming Azure SQL Database Serverless Free Tier takes ~15–30 seconds.
- **Remediation**:
  - The application includes built-in retry backoff (5 retries with exponential backoff) during startup database migration.
  - Run `smoke-test.ps1` with `-MaxRetries 45` to allow the initial container revision to finish warming up and completing migrations.

---

### 3. Database Migration Lock or Schema Conflict
- **Symptom**: Application logs show `There is already an object named '...' in the database` or SQL Server lock timeouts during startup migrations.
- **Root Cause**: An interrupted previous deployment held an EF Core migration lock or legacy pre-scaffolded tables were present.
- **Remediation**:
  - Run a dedicated migration execution (applies pending migrations and exits):
    ```bash
    dotnet run --project PersonalFinance/src/PersonalFinance.ApiService -- --migrate-only
    ```

---

### 4. Brevo Email Delivery Failures (401 Unauthorized / Non-Delivery)
- **Symptom**: User registers an account but does not receive a confirmation email. Logs display `Brevo API returned error status Unauthorized`.
- **Root Cause**: `Brevo:ApiKey` is missing, revoked, or the `Brevo:SenderEmail` is not authorized in Brevo.
- **Remediation**:
  1. Verify the API key in the Azure Container App environment secrets (`BREVO_API_KEY`).
  2. Log in to [Brevo Dashboard](https://app.brevo.com/) &rarr; **Senders, Domains & Dedicated IPs** &rarr; confirm `tngo0508@gmail.com` (or your configured sender) is marked as a **Verified Sender**.

---

### 5. Google OAuth `Error 400: redirect_uri_mismatch`
- **Symptom**: Social sign-in fails with Google Error 400 stating `redirect_uri_mismatch`.
- **Root Cause**: The deployed Azure Container App FQDN is not listed under Authorized Redirect URIs in Google Cloud Console.
- **Remediation**:
  1. Copy your public Azure Web URL (e.g. `https://web.yellowmeadow-123.eastus.azurecontainerapps.io`).
  2. Open [Google Cloud Console](https://console.cloud.google.com/apis/credentials) &rarr; edit your OAuth 2.0 Web Client.
  3. Add `https://<YOUR_WEB_URL>` to **Authorized JavaScript origins**.
  4. Add `https://<YOUR_WEB_URL>/signin-google` to **Authorized redirect URIs**.
  5. Click **Save** (changes propagate within 2–5 minutes).

---

### 6. Inspecting Live Container Logs via Azure CLI
To view live container execution logs, errors, or ASP.NET Core stack traces in Azure Container Apps:

```powershell
# Tail live console logs from the Web container
az containerapp logs show `
  --name web `
  --resource-group rg-personalfinance-prod `
  --follow

# Tail live console logs from the ApiService container
az containerapp logs show `
  --name apiservice `
  --resource-group rg-personalfinance-prod `
  --follow
```

---
