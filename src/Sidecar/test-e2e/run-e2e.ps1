# Agent365 Sidecar E2E Test Script
# Usage: Fill in A365_AUTH__ClientSecret below, then run: .\run-e2e.ps1

# --- Configuration ---
$env:A365_AGENT_ID = "efc8b690-43e6-4750-901a-3d9a87ceda0c"
$env:A365_AUTH__ClientId = "037c994d-fc58-49e3-8b44-816dfe8e4a26"
$env:A365_AUTH__TenantId = "bc51ddf2-9c8b-45e7-8e08-c7ac238f6520"
$env:A365_AUTH__ClientSecret = ""  # <-- ADD YOUR SECRET HERE

$env:A365_AUTH_MODE = "client-credentials"
$env:A365_TOOLING_GATEWAY_ENDPOINT = "https://agent365.svc.cloud.microsoft"
$env:A365_TOOLING__GatewayScope = "api://ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default"
$env:A365_CUSTOMER_WEBHOOK = "http://127.0.0.1:8080/agent/turn"
$env:A365_SKIP_AGENT_REGISTRATION = "true"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5365"

# --- Validate ---
if ([string]::IsNullOrEmpty($env:A365_AUTH__ClientSecret)) {
    Write-Host "ERROR: Set A365_AUTH__ClientSecret in this script before running." -ForegroundColor Red
    exit 1
}

Write-Host "=== Agent365 Sidecar E2E Test ===" -ForegroundColor Cyan
Write-Host "Agent ID:  $env:A365_AGENT_ID"
Write-Host "Client ID: $env:A365_AUTH__ClientId"
Write-Host "Tenant ID: $env:A365_AUTH__TenantId"
Write-Host "Gateway:   $env:A365_TOOLING_GATEWAY_ENDPOINT"
Write-Host ""

# --- Build and start sidecar ---
$sidecarProject = Join-Path $PSScriptRoot "..\Microsoft.Agents.A365.Sidecar\Microsoft.Agents.A365.Sidecar.csproj"
Write-Host "Building sidecar..." -ForegroundColor Yellow
$buildOutput = dotnet build $sidecarProject -c Debug 2>&1
if ($LASTEXITCODE -ne 0) { 
    Write-Host "Build failed!" -ForegroundColor Red
    $buildOutput | Select-String "error" | Write-Host
    exit 1 
}
Write-Host "  Build succeeded." -ForegroundColor Green

Write-Host "Starting sidecar..." -ForegroundColor Yellow
$sidecarProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$sidecarProject`" --no-build --no-launch-profile" -PassThru -NoNewWindow

# Wait for sidecar to be ready
$maxWait = 20
for ($i = 0; $i -lt $maxWait; $i++) {
    Start-Sleep -Seconds 1
    try {
        $null = Invoke-RestMethod -Uri "http://127.0.0.1:5365/healthz" -Method Get -TimeoutSec 2
        Write-Host "  Sidecar ready after $($i+1)s" -ForegroundColor Green
        break
    } catch {
        if ($i -eq ($maxWait - 1)) {
            Write-Host "  Sidecar failed to start within ${maxWait}s" -ForegroundColor Red
            Stop-Process -Id $sidecarProcess.Id -Force -ErrorAction SilentlyContinue
            exit 1
        }
    }
}

# --- Health check ---
Write-Host "`n--- Health Check ---" -ForegroundColor Cyan
try {
    $health = Invoke-RestMethod -Uri "http://127.0.0.1:5365/healthz" -Method Get
    Write-Host "  /healthz: OK" -ForegroundColor Green
    $health | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "  /healthz: FAILED - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n--- Readiness Check ---" -ForegroundColor Cyan
try {
    $ready = Invoke-RestMethod -Uri "http://127.0.0.1:5365/readyz" -Method Get
    Write-Host "  /readyz: OK" -ForegroundColor Green
    $ready | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "  /readyz: FAILED - $($_.Exception.Message)" -ForegroundColor Red
}

# --- Tooling: List servers ---
Write-Host "`n--- Tooling: List Servers ---" -ForegroundColor Cyan
try {
    $servers = Invoke-RestMethod -Uri "http://127.0.0.1:5365/api/v1/tools/servers" -Method Get
    Write-Host "  Found $($servers.Count) server(s):" -ForegroundColor Green
    $servers | ForEach-Object { Write-Host "    - $($_.name) [$($_.id)]" }
} catch {
    Write-Host "  List servers: FAILED - $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        Write-Host "  Body: $($reader.ReadToEnd())" -ForegroundColor DarkRed
    }
}

# --- Tooling: Enumerate tools ---
Write-Host "`n--- Tooling: Enumerate Tools ---" -ForegroundColor Cyan
try {
    $tools = Invoke-RestMethod -Uri "http://127.0.0.1:5365/api/v1/tools/enumerate" -Method Post
    Write-Host "  Found $($tools.Count) tool(s):" -ForegroundColor Green
    $tools | Select-Object -First 10 | ForEach-Object { Write-Host "    - $($_.name): $($_.description)" }
    if ($tools.Count -gt 10) { Write-Host "    ... and $($tools.Count - 10) more" }
} catch {
    Write-Host "  Enumerate tools: FAILED - $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        Write-Host "  Body: $($reader.ReadToEnd())" -ForegroundColor DarkRed
    }
}

# --- Cleanup ---
Write-Host "`n--- Stopping sidecar ---" -ForegroundColor Yellow
Stop-Process -Id $sidecarProcess.Id -Force -ErrorAction SilentlyContinue
Write-Host "Done." -ForegroundColor Green
