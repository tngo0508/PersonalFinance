<#
.SYNOPSIS
    Executes end-to-end HTTP smoke tests against a deployed PersonalFinance Web application.

.DESCRIPTION
    Probes key application endpoints (/health, /alive, /, /Identity/Account/Login)
    with retry support to verify container readiness, liveness, and routing after deployment.
    Note: PersonalFinance.ApiService (/scalar/v1, /openapi/v1.json) is internal-only
    (ingress external=false in ACA) for microservice security and is validated during CI tests.

.PARAMETER WebUrl
    Base URL of the deployed Web application (e.g. https://web.yellowmeadow-123.eastus.azurecontainerapps.io).

.PARAMETER MaxRetries
    Maximum number of attempts to wait for container readiness (default: 30).

.PARAMETER RetryDelaySeconds
    Seconds to wait between retry attempts (default: 5).

.EXAMPLE
    .\infra\smoke-test.ps1 -WebUrl "https://web.yellowmeadow-123.eastus.azurecontainerapps.io"
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$WebUrl,

    [int]$MaxRetries = 30,
    [int]$RetryDelaySeconds = 5
)

$ErrorActionPreference = "Stop"

# Normalize base URL (strip trailing slash)
$WebUrl = $WebUrl.TrimEnd('/')
if (-not ($WebUrl.StartsWith("http://") -or $WebUrl.StartsWith("https://"))) {
    $WebUrl = "https://$WebUrl"
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " PersonalFinance Post-Deployment Smoke Test Suite               " -ForegroundColor Cyan
Write-Host " Target URL: $WebUrl" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

$endpoints = @(
    @{ Name = "Readiness Health Check"; Path = "/health"; ExpectedStatuses = @(200); Retryable = $true },
    @{ Name = "Liveness Health Check";  Path = "/alive";  ExpectedStatuses = @(200); Retryable = $false },
    @{ Name = "Home UI / Landing";      Path = "/";       ExpectedStatuses = @(200, 302); Retryable = $false },
    @{ Name = "Identity Login Page";    Path = "/Identity/Account/Login"; ExpectedStatuses = @(200); Retryable = $false }
)

$allPassed = $true
$testIndex = 1

foreach ($endpoint in $endpoints) {
    $targetUrl = "$WebUrl$($endpoint.Path)"
    Write-Host "`n[$testIndex/$($endpoints.Count)] Testing $($endpoint.Name) ($($endpoint.Path))..." -ForegroundColor Yellow

    $passed = $false
    $attempts = if ($endpoint.Retryable) { $MaxRetries } else { 1 }

    for ($i = 1; $i -le $attempts; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $targetUrl -Method Get -TimeoutSec 10 -UseBasicParsing -ErrorAction SilentlyContinue
            $statusCode = [int]$response.StatusCode
        }
        catch {
            if ($_.Exception.Response) {
                $statusCode = [int]$_.Exception.Response.StatusCode
            } else {
                $statusCode = 0
            }
        }

        if ($endpoint.ExpectedStatuses -contains $statusCode) {
            Write-Host "  -> Attempt $i/$attempts: HTTP $statusCode (Expected: $($endpoint.ExpectedStatuses -join ', ')) - PASSED" -ForegroundColor Green
            $passed = $true
            break
        } else {
            Write-Host "  -> Attempt $i/$attempts: HTTP $statusCode (Expected: $($endpoint.ExpectedStatuses -join ', ')) - Retrying..." -ForegroundColor DarkYellow
            if ($i -lt $attempts) {
                Start-Sleep -Seconds $RetryDelaySeconds
            }
        }
    }

    if (-not $passed) {
        Write-Host "  [FAIL] Endpoint '$($endpoint.Path)' failed after $attempts attempt(s)." -ForegroundColor Red
        $allPassed = $false
    }

    $testIndex++
}

Write-Host "`n================================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host " All smoke tests passed successfully! Application is ready." -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host " One or more smoke tests failed." -ForegroundColor Red
    Write-Host "================================================================" -ForegroundColor Cyan
    exit 1
}
