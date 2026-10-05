<#
.SYNOPSIS
    Deploys or validates the PersonalFinance zero-cost Azure Container Apps architecture.

.DESCRIPTION
    Provisions Azure Container Apps Managed Environment, Log Analytics Workspace (free 5GB/mo),
    internal API service, external Web service, and optional Azure SQL Database Serverless Free Tier.

.PARAMETER ResourceGroupName
    Target Azure Resource Group name (e.g. rg-personalfinance-prod).

.PARAMETER Location
    Azure region (e.g. eastus, westus2, centralus).

.PARAMETER EnvironmentName
    Environment name prefix (e.g. pf-prod, pf-dev).

.PARAMETER ParametersFile
    Path to the parameters JSON file. Defaults to main.parameters.json.

.PARAMETER GoogleClientId
    Google OAuth 2.0 Client ID for external sign-in.

.PARAMETER GoogleClientSecret
    Google OAuth 2.0 Client Secret for external sign-in.

.PARAMETER BrevoApiKey
    Brevo REST API key for transactional email delivery.

.PARAMETER GoogleDriveApiKey
    Google Drive API v3 key.

.PARAMETER DeploySqlDatabase
    Switch to provision Azure SQL Database Serverless Free Tier ($0.00).

.PARAMETER SqlAdminPassword
    Administrator password if deploying Azure SQL Database.

.PARAMETER ValidateOnly
    Validates Bicep syntax and Azure ARM template schema without provisioning resources.

.PARAMETER WhatIf
    Performs a deployment preview (what-if) showing planned changes.

.EXAMPLE
    .\deploy.ps1 -ResourceGroupName "rg-personalfinance-prod" -Location "eastus" -ValidateOnly
    .\deploy.ps1 -ResourceGroupName "rg-personalfinance-prod" -Location "eastus" -WhatIf
    .\deploy.ps1 -ResourceGroupName "rg-personalfinance-prod" -Location "eastus" -DeploySqlDatabase
#>

[CmdletBinding()]
param (
    [string]$SubscriptionId,
    [string]$ResourceGroupName = "rg-personalfinance-prod",
    [string]$Location = "eastus",
    [string]$EnvironmentName = "pf-prod",
    [string]$TemplateFile = "$PSScriptRoot/main.bicep",
    [string]$ParametersFile = "$PSScriptRoot/main.parameters.json",
    [string]$ApiImage = "ghcr.io/tngo0508/personalfinance-api:latest",
    [string]$WebImage = "ghcr.io/tngo0508/personalfinance-web:latest",
    [string]$GoogleClientId = "",
    [string]$GoogleClientSecret = "",
    [string]$BrevoApiKey = "",
    [string]$BrevoSenderEmail = "tngo0508@gmail.com",
    [string]$BrevoSenderName = "PersonalFinance",
    [string]$GoogleDriveApiKey = "",
    [switch]$DeploySqlDatabase,
    [string]$SqlAdminPassword = "",
    [string]$CustomConnectionString = "",
    [string]$CustomDomainName = "",
    [switch]$ValidateOnly,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " PersonalFinance Azure Deployment (Zero-Cost ACA Architecture)  " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

# 1. Check Azure CLI availability
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI ('az') is not installed or not in PATH. Please install Azure CLI from https://aka.ms/installazurecliwindows."
    exit 1
}

# 2. Check Azure Login Context
Write-Host "`n[1/5] Checking Azure authentication..." -ForegroundColor Yellow
$accountJson = az account show --output json 2>$null
if (-not $accountJson) {
    Write-Host "Not logged in to Azure. Running 'az login'..." -ForegroundColor Yellow
    az login --output none
    $accountJson = az account show --output json
}

$account = $accountJson | ConvertFrom-Json
Write-Host "  -> Logged in as: $($account.user.name) (Subscription: $($account.name) [$($account.id)])" -ForegroundColor Green

if ($SubscriptionId -and ($account.id -ne $SubscriptionId)) {
    Write-Host "  -> Switching to Subscription ID: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
}

# 3. Create Resource Group if not exists
Write-Host "`n[2/5] Verifying Resource Group '$ResourceGroupName' in '$Location'..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroupName
if ($rgExists -eq "false") {
    Write-Host "  -> Creating Resource Group '$ResourceGroupName' in '$Location'..." -ForegroundColor Green
    az group create --name $ResourceGroupName --location $Location --output none
} else {
    Write-Host "  -> Resource Group '$ResourceGroupName' exists." -ForegroundColor Green
}

# 4. Assemble Deployment Parameters
Write-Host "`n[3/5] Assembling parameters..." -ForegroundColor Yellow

$paramArgs = @()
if (Test-Path $ParametersFile) {
    $paramArgs += "--parameters"
    $paramArgs += "@$ParametersFile"
}

$dynamicParams = @(
    "environmentName=$EnvironmentName",
    "location=$Location",
    "apiImage=$ApiImage",
    "webImage=$WebImage",
    "deploySqlDatabase=$($DeploySqlDatabase.IsPresent.ToString().ToLower())"
)

if ($GoogleClientId) { $dynamicParams += "googleClientId=$GoogleClientId" }
if ($GoogleClientSecret) { $dynamicParams += "googleClientSecret=$GoogleClientSecret" }
if ($BrevoApiKey) { $dynamicParams += "brevoApiKey=$BrevoApiKey" }
if ($BrevoSenderEmail) { $dynamicParams += "brevoSenderEmail=$BrevoSenderEmail" }
if ($BrevoSenderName) { $dynamicParams += "brevoSenderName=$BrevoSenderName" }
if ($GoogleDriveApiKey) { $dynamicParams += "googleDriveApiKey=$GoogleDriveApiKey" }
if ($SqlAdminPassword) { $dynamicParams += "sqlAdministratorLoginPassword=$SqlAdminPassword" }
if ($CustomConnectionString) { $dynamicParams += "customConnectionString=$CustomConnectionString" }
if ($CustomDomainName) { $dynamicParams += "customDomainName=$CustomDomainName" }

if ($dynamicParams.Count -gt 0) {
    $paramArgs += "--parameters"
    foreach ($param in $dynamicParams) {
        $paramArgs += $param
    }
}

# 5. Validation / What-If / Deployment
if ($ValidateOnly) {
    Write-Host "`n[4/5] Validating Bicep deployment (Dry-Run)..." -ForegroundColor Yellow
    $validateResult = az deployment group validate `
        --resource-group $ResourceGroupName `
        --template-file $TemplateFile `
        @paramArgs `
        --output json | ConvertFrom-Json

    if ($validateResult.error) {
        Write-Error "Bicep validation failed: $($validateResult.error.message)"
        exit 1
    }
    Write-Host "  -> Bicep template validation succeeded with 0 errors!" -ForegroundColor Green
    exit 0
}

if ($WhatIf) {
    Write-Host "`n[4/5] Generating What-If deployment preview..." -ForegroundColor Yellow
    az deployment group what-if `
        --resource-group $ResourceGroupName `
        --template-file $TemplateFile `
        @paramArgs
    exit 0
}

Write-Host "`n[4/5] Executing Azure Container Apps deployment..." -ForegroundColor Yellow
$deploymentName = "deploy-$EnvironmentName-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

$deploymentResult = az deployment group create `
    --name $deploymentName `
    --resource-group $ResourceGroupName `
    --template-file $TemplateFile `
    @paramArgs `
    --output json | ConvertFrom-Json

if ($deploymentResult.properties.provisioningState -eq "Succeeded") {
    Write-Host "`n[5/5] Deployment Succeeded!" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host " Public Web Application URL : $($deploymentResult.properties.outputs.webUrl.value)" -ForegroundColor Green
    Write-Host " Internal API FQDN          : $($deploymentResult.properties.outputs.apiFqdn.value)" -ForegroundColor Green
    Write-Host " Container Apps Environment : $($deploymentResult.properties.outputs.containerAppEnvironmentName.value)" -ForegroundColor Green
    Write-Host " Log Analytics Workspace    : $($deploymentResult.properties.outputs.logAnalyticsWorkspaceName.value)" -ForegroundColor Green
    if ($deploymentResult.properties.outputs.sqlServerFqdn.value) {
        Write-Host " Azure SQL Server FQDN      : $($deploymentResult.properties.outputs.sqlServerFqdn.value)" -ForegroundColor Green
    }
    Write-Host " Operating Cost Tier        : Scale-to-Zero ($0.00 / month)" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
} else {
    Write-Error "Deployment finished with state: $($deploymentResult.properties.provisioningState)"
    exit 1
}
