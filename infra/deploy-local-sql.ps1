<#
.SYNOPSIS
    Provisions an Azure SQL Database (Serverless Free Tier) for local development.

.DESCRIPTION
    Deploys infra/local-dev.bicep (SQL server + database only), opens the server firewall
    to this machine's public IP, and stores the resulting connection string in dotnet
    user-secrets (ConnectionStrings:DefaultConnection) for PersonalFinance.ApiService and
    PersonalFinance.Web. EF Core migrations are applied automatically on app startup.

.PARAMETER ResourceGroupName
    Target Azure Resource Group name. Defaults to rg-personalfinance-dev.

.PARAMETER Location
    Azure region for the resource group (if it needs to be created).

.PARAMETER SqlLocation
    Azure region for the SQL server and database.

.PARAMETER EnvironmentName
    Environment name prefix. Defaults to pf-dev.

.PARAMETER SqlAdminPassword
    Administrator password for the SQL server. Prompted for if omitted.

.PARAMETER ClientIpAddress
    Public IPv4 address to allow through the firewall. Auto-detected if omitted.

.PARAMETER SkipUserSecrets
    Do not write the connection string to dotnet user-secrets; just print it.

.EXAMPLE
    .\deploy-local-sql.ps1
    .\deploy-local-sql.ps1 -SqlLocation "westus2" -ClientIpAddress "203.0.113.10"
#>

[CmdletBinding()]
param (
    [string]$SubscriptionId,
    [string]$ResourceGroupName = "rg-personalfinance-dev",
    [string]$Location = "westus2",
    [string]$SqlLocation = "westus2",
    [string]$EnvironmentName = "pf-dev",
    [string]$SqlAdminLogin = "sqladmin",
    [string]$SqlAdminPassword = "",
    [string]$ClientIpAddress = "",
    [string]$TemplateFile = "$PSScriptRoot/local-dev.bicep",
    [switch]$SkipUserSecrets
)

$ErrorActionPreference = "Stop"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " PersonalFinance Local Development Azure SQL Provisioning        " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI ('az') is not installed or not in PATH. Please install Azure CLI from https://aka.ms/installazurecliwindows."
    exit 1
}

# 1. Azure login
Write-Host "`n[1/5] Checking Azure authentication..." -ForegroundColor Yellow
$accountJson = az account show --output json 2>$null
if (-not $accountJson) {
    az login --output none
    $accountJson = az account show --output json
}
$account = $accountJson | ConvertFrom-Json
Write-Host "  -> Logged in as: $($account.user.name) (Subscription: $($account.name) [$($account.id)])" -ForegroundColor Green

if ($SubscriptionId -and ($account.id -ne $SubscriptionId)) {
    az account set --subscription $SubscriptionId
}

# 2. Resource group
Write-Host "`n[2/5] Verifying Resource Group '$ResourceGroupName'..." -ForegroundColor Yellow
if ((az group exists --name $ResourceGroupName) -eq "false") {
    Write-Host "  -> Creating Resource Group '$ResourceGroupName' in '$Location'..." -ForegroundColor Green
    az group create --name $ResourceGroupName --location $Location --output none
} else {
    Write-Host "  -> Resource Group '$ResourceGroupName' exists." -ForegroundColor Green
}

# 3. Inputs: client IP + admin password
Write-Host "`n[3/5] Resolving inputs..." -ForegroundColor Yellow
if (-not $ClientIpAddress) {
    $ClientIpAddress = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 15).ToString().Trim()
}
Write-Host "  -> Allowing client IP: $ClientIpAddress" -ForegroundColor Green

if (-not $SqlAdminPassword) {
    $securePassword = Read-Host "Enter SQL administrator password (8+ chars, 3 of: upper, lower, digit, symbol)" -AsSecureString
    $SqlAdminPassword = [System.Net.NetworkCredential]::new("", $securePassword).Password
}

# 4. Deploy
Write-Host "`n[4/5] Deploying Azure SQL (this can take a few minutes)..." -ForegroundColor Yellow
$deploymentName = "localdev-sql-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$deploymentResult = az deployment group create `
    --name $deploymentName `
    --resource-group $ResourceGroupName `
    --template-file $TemplateFile `
    --parameters `
        "environmentName=$EnvironmentName" `
        "sqlLocation=$SqlLocation" `
        "sqlAdministratorLogin=$SqlAdminLogin" `
        "sqlAdministratorLoginPassword=$SqlAdminPassword" `
        "allowedClientIpAddresses=[`"$ClientIpAddress`"]" `
    --output json | ConvertFrom-Json

if ($deploymentResult.properties.provisioningState -ne "Succeeded") {
    Write-Error "Deployment finished with state: $($deploymentResult.properties.provisioningState)"
    exit 1
}

$outputs = $deploymentResult.properties.outputs
$serverFqdn = $outputs.sqlServerFqdn.value
$databaseName = $outputs.databaseName.value
$connectionString = "Server=tcp:$serverFqdn,1433;Initial Catalog=$databaseName;Persist Security Info=False;User ID=$SqlAdminLogin;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

# 5. User secrets
Write-Host "`n[5/5] Configuring local connection string..." -ForegroundColor Yellow
if ($SkipUserSecrets) {
    Write-Host "  -> Connection string (keep this secret):" -ForegroundColor Green
    Write-Host "     $connectionString"
} else {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $projects = @(
        "$repoRoot/PersonalFinance/src/PersonalFinance.ApiService/PersonalFinance.ApiService.csproj",
        "$repoRoot/PersonalFinance/src/PersonalFinance.Web/PersonalFinance.Web.csproj"
    )
    foreach ($project in $projects) {
        dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connectionString --project $project | Out-Null
        Write-Host "  -> Set user-secret for $(Split-Path $project -Leaf)" -ForegroundColor Green
    }
}

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host " Azure SQL Server FQDN : $serverFqdn" -ForegroundColor Green
Write-Host " Database              : $databaseName" -ForegroundColor Green
Write-Host " Firewall client IP    : $ClientIpAddress" -ForegroundColor Green
Write-Host " Run the AppHost; EF Core migrations apply on startup." -ForegroundColor Cyan
Write-Host " Note: the serverless DB auto-pauses; the first connection after a pause may take ~1 min." -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
