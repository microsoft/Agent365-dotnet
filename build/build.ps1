#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Build script for Microsoft Agents A365 SDK using traversal build

.DESCRIPTION
    This script builds the Agents A365 SDK using dirs.proj traversal build and optionally creates NuGet packages.

.PARAMETER Configuration
    The build configuration (Debug or Release). Default is Release.

.PARAMETER Clean
    Whether to clean before building. Default is false.

.PARAMETER Test
    Whether to run tests after building. Default is false.

.PARAMETER Pack
    Whether to create NuGet packages. Default is false.

.PARAMETER Restore
    Whether to restore packages. Default is false.

.PARAMETER Verbosity
    Build verbosity level. Default is minimal.

.EXAMPLE
    .\build.ps1 -Pack -Test
    Builds the project in Release mode, runs tests, and creates NuGet packages.

.EXAMPLE
    .\build.ps1 -Configuration Debug -Clean -Restore -Test
    Cleans, restores, builds in Debug mode, and runs tests.
#>

param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [switch]$Clean,
    
    [Parameter()]
    [switch]$Test,
    
    [Parameter()]
    [switch]$Pack,
    
    [Parameter()]
    [switch]$Restore,
    
    [Parameter()]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'

# Traversal projects and SDK configuration live under src.
Set-Location (Join-Path $PSScriptRoot '..\src')

Write-Host "🔧 Building Microsoft Agents A365 SDK..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan

try {
    if ($Clean) {
        Write-Host "🧹 Cleaning solution..." -ForegroundColor Yellow
        dotnet clean dirs.proj --configuration $Configuration --verbosity $Verbosity
        if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
    }

    if ($Restore) {
        Write-Host "📦 Restoring packages..." -ForegroundColor Yellow
        dotnet restore dirs.proj --verbosity $Verbosity
        if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    }

    Write-Host "🔨 Building solution..." -ForegroundColor Yellow
    dotnet build dirs.proj --configuration $Configuration --verbosity $Verbosity --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    if ($Test) {
        Write-Host "🧪 Running tests..." -ForegroundColor Yellow
        dotnet test tests.proj --configuration $Configuration --verbosity $Verbosity --no-build --logger "console;verbosity=detailed"
        if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    }

    if ($Pack) {
        Write-Host "📦 Creating packages..." -ForegroundColor Yellow
        
        # Isolate this run from packages left by earlier builds or branches.
        $outputPath = Join-Path (Split-Path $PSScriptRoot -Parent) "NuGetPackages\$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $outputPath | Out-Null
        
        dotnet pack dirs.proj --configuration $Configuration --verbosity $Verbosity --no-build --output $outputPath
        if ($LASTEXITCODE -ne 0) { throw "Pack failed" }

        if (Get-ChildItem -LiteralPath $outputPath -Filter 'Microsoft.Agents.A365.Observability.*' -File) {
            throw "Retired Observability packages must not be released."
        }
        if (-not (Get-ChildItem -LiteralPath $outputPath -Filter '*.nupkg' -File)) {
            throw "Pack produced no NuGet packages."
        }
        
        # List created packages
        Write-Host "📦 Created packages:" -ForegroundColor Green
        Write-Host "Output: $outputPath" -ForegroundColor Cyan
        Get-ChildItem -Path $outputPath -Filter "*.nupkg" | ForEach-Object {
            Write-Host "  - $($_.Name)" -ForegroundColor Cyan
        }
    }

    Write-Host "✅ Build completed successfully!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Build failed: $_" -ForegroundColor Red
    exit 1
}