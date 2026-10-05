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

**Conditional Secrets** (only required if deploying Azure SQL Database):

| Secret Name | Value | When Required |
|---|---|---|
| `SQL_ADMIN_PASSWORD` | Strong password for Azure SQL admin account | Only when `deploy_sql` input is `true` in workflow |
| `CUSTOM_CONNECTION_STRING` | External SQL Server or database connection string | Only if using custom database instead of Azure SQL |

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
   - **`deploy_sql`** (boolean, default: `false`): Set to `true` to provision Azure SQL Database Serverless Free Tier. When enabled, you **must** provide `SQL_ADMIN_PASSWORD` secret.
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

> **Note**: The API Service (`PersonalFinance.ApiService`) has internal-only ingress and is not publicly smoke-tested. Its endpoints (`/scalar/v1`, `/openapi/v1.json`) are validated in the CI test suite.

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

**Deployment Fails with Permission Error**:
- Confirm the App Registration has `Contributor` role on the target Subscription or Resource Group.
- Check that the Resource Group `rg-personalfinance-prod` exists or will be created by the Bicep template.

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
