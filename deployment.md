# Personal Finance Application - Deployment Guide

This guide provides deterministic, step-by-step instructions for deploying the **Personal Finance** application across multiple environments. The architecture is engineered for **zero operating cost** using Microsoft Azure's lifetime free tiers (Azure Container Apps Consumption, GitHub Container Registry, Log Analytics free 5GB/mo, and optional Azure SQL Database Serverless Free Tier) alongside local containerized options.

---

## 1. Architecture & Deployment Strategy Overview

The application comprises two core containerized workloads running in .NET 10:
- **`PersonalFinance.Web` (Frontend)**: ASP.NET Core MVC with Razor Pages, Identity UI, Bootstrap 5, and Chart.js. Exposed via external HTTPS ingress.
- **`PersonalFinance.ApiService` (Backend)**: Minimal API REST backend handling Google Drive integration and OpenAPI documentation (`/scalar/v1`). Configured with internal ingress only within the Container Apps Environment for zero-trust security.
- **Data Persistence Tier**: Supports **SQLite** (local volume/file-backed) or **Azure SQL Database (Serverless Free Tier)** with automatic cold-start retry resilience.

```
                      ┌──────────────────────────────────────────────┐
                      │            PersonalFinance System            │
                      └──────────────────────┬───────────────────────┘
                                             │
             ┌───────────────────────────────┼──────────────────────────────┐
             │                               │                              │
             ▼                               ▼                              ▼
  [ GitHub Actions CI/CD ]       [ Local Azure CLI / azd ]        [ Local Docker Engine ]
   • .github/workflows/           • infra/deploy.ps1               • Dockerfile build/run
     deploy-azure.yml             • azd up (azure.yaml)            • Multi-container test
             │                               │                              │
             │                               │                              │
             └───────────────────────┬───────┴──────────────────────────────┘
                                     │
                                     ▼
                     ┌───────────────────────────────┐
                     │   Azure Container Apps (ACA)  │
                     │  • Web: External Ingress      │
                     │  • API: Internal Ingress      │
                     │  • Database: SQLite/Azure SQL │
                     └───────────────────────────────┘
```

### Deployment Modality Selection Matrix

| Deployment Method | Target Environment | Best For | Ingress & Networking | Database Tier |
|---|---|---|---|---|
| **Option 1: GitHub Actions CI/CD (Recommended)** | Azure Container Apps | Automated production deployments, continuous delivery on push to `main` or manual trigger. | Web: External<br/>API: Internal | Azure SQL Serverless Free or SQLite |
| **Option 2: PowerShell / Azure CLI (`deploy.ps1`)** | Azure Container Apps | Direct terminal control, parameter tweaking, dry-run validation (`-ValidateOnly`, `-WhatIf`). | Web: External<br/>API: Internal | Azure SQL Serverless Free or SQLite |
| **Option 3: Azure Developer CLI (`azd`)** | Azure Container Apps | Standardized developer workflows and single-command provisioning (`azd up`). | Web: External<br/>API: Internal | Azure SQL Serverless Free or SQLite |
| **Option 4: Local Multi-Stage Docker** | Local Machine (Docker) | Pre-commit validation, isolated local integration testing, zero Azure account dependency. | Localhost ports `8080` (Web) & `8081` (API) | Local SQLite |

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

Configure passwordless GitHub Actions authentication in your Microsoft Entra ID:

1. **Create an App Registration in Azure Portal**:
   - Navigate to **Microsoft Entra ID** &rarr; **App registrations** &rarr; **New registration**.
   - Name: `personalfinance-github-actions`.
   - Supported account types: **Accounts in this organizational directory only**.
   - Click **Register**. Copy the **Application (client) ID** and **Directory (tenant) ID**.

2. **Grant Azure Role Assignment**:
   - Navigate to your Azure Subscription (or target Resource Group `rg-personalfinance-prod`).
   - Select **Access control (IAM)** &rarr; **Add role assignment**.
   - Role: **Contributor** (and **User Access Administrator** if creating role assignments).
   - Assign access to: **User, group, or service principal** &rarr; select `personalfinance-github-actions`.

3. **Add Federated Credential for GitHub Actions**:
   - In the App Registration, select **Certificates & secrets** &rarr; **Federated credentials** &rarr; **Add credential**.
   - Federated credential scenario: **GitHub Actions deploying Azure resources**.
   - **Organization**: Your GitHub username or organization (e.g., `tngo0508`).
   - **Repository**: `PersonalFinanceApp` (or your repository name).
   - **Entity type**: **Branch** &rarr; Branch name: `main`.
   - Name: `personalfinance-main-branch`.
   - Click **Add**.

---

### Step 2: Configure GitHub Repository Secrets

In your GitHub repository, navigate to **Settings** &rarr; **Secrets and variables** &rarr; **Actions** &rarr; **New repository secret**, and create:

```
AZURE_CLIENT_ID          = <Application (client) ID from Step 1>
AZURE_TENANT_ID          = <Directory (tenant) ID from Step 1>
AZURE_SUBSCRIPTION_ID    = <Azure Subscription ID>
GOOGLE_CLIENT_ID         = <Google OAuth Client ID>
GOOGLE_CLIENT_SECRET     = <Google OAuth Client Secret>
BREVO_API_KEY            = <Brevo v3 API Key>
GOOGLE_DRIVE_API_KEY     = <Google Drive API v3 Key>
```

---

### Step 3: Trigger CI/CD Pipeline

#### Automatic Trigger
Push any commit to the `main` branch:
```bash
git add .
git commit -m "Deploy production update"
git push origin main
```

#### Manual Trigger via `workflow_dispatch`
1. In GitHub, open the **Actions** tab.
2. Select **Build, Test, and Deploy to Azure Container Apps** in the left sidebar.
3. Click **Run workflow**:
   - **Azure Environment Name prefix**: `pf-prod` (default).
   - **Provision Azure SQL Database Serverless Free Tier ($0.00)**: `true` or `false` (default: `false` for SQLite).
   - **Dry-run validation / What-If only**: `false` (set to `true` to test without deploying).
4. Click **Run workflow**.

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

##### Option A: Default Zero-Cost SQLite Stack
```powershell
.\infra\deploy.ps1 `
  -ResourceGroupName "rg-personalfinance-prod" `
  -Location "eastus" `
  -EnvironmentName "pf-prod" `
  -GoogleClientId "your-google-client-id.apps.googleusercontent.com" `
  -GoogleClientSecret "your-google-client-secret" `
  -BrevoApiKey "your-brevo-api-key" `
  -GoogleDriveApiKey "your-google-drive-key"
```

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
| `/scalar/v1` *(ApiService)* | OpenAPI / Docs | `200 OK` | Internal-only API documentation (accessible internally via Container Apps Environment or locally). |

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
- **Symptom**: Application logs show `SQLite Error 1: 'table "..." already exists'` or SQL Server lock timeouts.
- **Root Cause**: An interrupted previous deployment held an EF Core migration lock or legacy pre-scaffolded tables were present.
- **Remediation**:
  - For SQLite: `DatabaseMigrationExtensions` automatically detects un-tracked legacy schemas and resets the SQLite database to cleanly apply current provider migrations.
  - For Azure SQL: Run a dedicated migration execution:
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
