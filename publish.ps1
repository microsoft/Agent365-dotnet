#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Publish script for Microsoft Kairo SDK NuGet package

.DESCRIPTION
    This script publishes the Kairo SDK NuGet package to Azure DevOps Artifacts.

.PARAMETER PackagePath
    The path to the NuGet package file. Default is ../NuGetPackages/Microsoft.Kairo.Sdk.*.nupkg

.PARAMETER ApiKey
    The NuGet API key for authentication. Can also be provided via NUGET_API_KEY environment variable.
    For Azure DevOps, this should be a Personal Access Token (PAT) with packaging read/write permissions.

.PARAMETER Source
    The NuGet source URL. Default is the Azure DevOps Agent365 feed.

.PARAMETER SkipDuplicate
    Whether to skip duplicate packages. Default is true.

.EXAMPLE
    .\publish.ps1 -ApiKey "your-pat-token"
    Publishes the package to Azure DevOps Artifacts with the provided PAT token.

.EXAMPLE
    $env:NUGET_API_KEY="your-pat-token"; .\publish.ps1
    Publishes the package using the PAT token from environment variable.
#>

param(
    [string]$PackagePath = "../NuGetPackages/Microsoft.Kairo.Sdk.*.nupkg",
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Source = "https://pkgs.dev.azure.com/msazure/OneAgile/_packaging/Agent365/nuget/v3/index.json",
    [switch]$SkipDuplicate = $true
)

# Ensure we're in the correct directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
Push-Location $scriptPath

try {
    Write-Host "Publishing Microsoft Kairo SDK NuGet package..." -ForegroundColor Green
    
    # Check if API key is provided
    if ([string]::IsNullOrEmpty($ApiKey)) {
        throw "API key is required. Provide it via -ApiKey parameter or NUGET_API_KEY environment variable."
    }
    
    # Find the package file
    $packageFiles = Get-ChildItem -Path $PackagePath -ErrorAction SilentlyContinue
    if ($packageFiles.Count -eq 0) {
        throw "No package files found at path: $PackagePath"
    }
    
    foreach ($packageFile in $packageFiles) {    
        Write-Host "Publishing package: $($packageFile.Name)" -ForegroundColor Cyan
        
        # Publish the package
        $publishArgs = @(
            "nuget"
            "push"
            $packageFile.FullName
            "--source", $Source
            "--api-key", $ApiKey
        )
        
        if ($SkipDuplicate) {
            $publishArgs += "--skip-duplicate"
        }
        
        Write-Host "Running: dotnet $($publishArgs[0..2] -join ' ') [package] --source $Source --api-key [REDACTED]" -ForegroundColor Yellow
        & dotnet $publishArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "Package published successfully!" -ForegroundColor Green
        Write-Host "Package: $($packageFile.Name)" -ForegroundColor Cyan
    }
    
    Write-Host "Source: $Source" -ForegroundColor Cyan
    
} catch {
    Write-Error "Publish failed: $($_.Exception.Message)"
    exit 1
} finally {
    Pop-Location
}