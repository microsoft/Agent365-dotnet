#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Validation script for Microsoft Kairo SDK NuGet package

.DESCRIPTION
    This script validates the created NuGet package to ensure it contains all expected files and metadata.

.PARAMETER PackagePath
    The path to the NuGet package file. Default is ../NuGetPackages/Microsoft.Kairo.Sdk.*.nupkg

.EXAMPLE
    .\validate.ps1
    Validates the package with default settings.

.EXAMPLE
    .\validate.ps1 -PackagePath "../NuGetPackages/Microsoft.Kairo.Sdk.1.0.0.nupkg"
    Validates a specific package file.
#>

param(
    [string]$PackagePath = "../NuGetPackages/Microsoft.Kairo.Sdk.*.nupkg"
)

# Ensure we're in the correct directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
Push-Location $scriptPath

try {
    Write-Host "Validating Microsoft Kairo SDK NuGet package..." -ForegroundColor Green
    
    # Find the package file
    $packageFiles = Get-ChildItem -Path $PackagePath -ErrorAction SilentlyContinue
    if ($packageFiles.Count -eq 0) {
        throw "No package files found at path: $PackagePath"
    }
    
    if ($packageFiles.Count -gt 1) {
        Write-Host "Multiple package files found. Using the most recent one." -ForegroundColor Yellow
        $packageFile = $packageFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    } else {
        $packageFile = $packageFiles[0]
    }
    
    Write-Host "Validating package: $($packageFile.Name)" -ForegroundColor Cyan
    Write-Host "Package size: $([math]::Round($packageFile.Length / 1KB, 2)) KB" -ForegroundColor Cyan
    
    # Extract package contents to temp directory
    $tempDir = Join-Path $env:TEMP "KairoSdk-Validation-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    try {
        # Extract the package (it's a zip file)
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($packageFile.FullName, $tempDir)
        
        Write-Host "`nPackage Contents:" -ForegroundColor Yellow
        Get-ChildItem -Path $tempDir -Recurse | ForEach-Object {
            $relativePath = $_.FullName.Substring($tempDir.Length + 1)
            Write-Host "  $relativePath" -ForegroundColor Gray
        }
        
        # Check for essential files
        $essentialFiles = @(
            "lib\net8.0\Microsoft.Kairo.Sdk.dll",
            "lib\net8.0\Microsoft.Kairo.Sdk.xml",
            "Microsoft.Kairo.Sdk.nuspec",
            "README.md",
            "CHANGELOG.md"
        )
        
        Write-Host "`nValidation Results:" -ForegroundColor Yellow
        $allFilesPresent = $true
        
        foreach ($file in $essentialFiles) {
            $fullPath = Join-Path $tempDir $file
            if (Test-Path $fullPath) {
                Write-Host "  ✓ $file" -ForegroundColor Green
            } else {
                Write-Host "  ✗ $file (MISSING)" -ForegroundColor Red
                $allFilesPresent = $false
            }
        }
        
        # Check nuspec metadata
        $nuspecPath = Join-Path $tempDir "Microsoft.Kairo.Sdk.nuspec"
        if (Test-Path $nuspecPath) {
            Write-Host "`nNuSpec Metadata:" -ForegroundColor Yellow
            [xml]$nuspec = Get-Content $nuspecPath
            $metadata = $nuspec.package.metadata
            
            Write-Host "  ID: $($metadata.id)" -ForegroundColor Gray
            Write-Host "  Version: $($metadata.version)" -ForegroundColor Gray
            Write-Host "  Title: $($metadata.title)" -ForegroundColor Gray
            Write-Host "  Authors: $($metadata.authors)" -ForegroundColor Gray
            Write-Host "  Description: $($metadata.description)" -ForegroundColor Gray
            Write-Host "  Tags: $($metadata.tags)" -ForegroundColor Gray
            Write-Host "  License: $($metadata.license.type)" -ForegroundColor Gray
            Write-Host "  Repository: $($metadata.repository.url)" -ForegroundColor Gray
            
            # Check dependencies
            if ($metadata.dependencies.group.dependency) {
                Write-Host "  Dependencies:" -ForegroundColor Gray
                foreach ($dep in $metadata.dependencies.group.dependency) {
                    Write-Host "    - $($dep.id) $($dep.version)" -ForegroundColor Gray
                }
            }
        }
        
        # Check symbol package
        $symbolPackagePath = $packageFile.FullName -replace "\.nupkg$", ".snupkg"
        if (Test-Path $symbolPackagePath) {
            Write-Host "`nSymbol Package:" -ForegroundColor Yellow
            $symbolPackageFile = Get-Item $symbolPackagePath
            Write-Host "  ✓ Symbol package found: $($symbolPackageFile.Name)" -ForegroundColor Green
            Write-Host "  Size: $([math]::Round($symbolPackageFile.Length / 1KB, 2)) KB" -ForegroundColor Gray
        } else {
            Write-Host "`nSymbol Package:" -ForegroundColor Yellow
            Write-Host "  ✗ Symbol package not found" -ForegroundColor Red
        }
        
        # Final result
        Write-Host "`nValidation Summary:" -ForegroundColor Yellow
        if ($allFilesPresent) {
            Write-Host "  ✓ Package validation PASSED" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Package validation FAILED" -ForegroundColor Red
            exit 1
        }
        
    } finally {
        # Clean up temp directory
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    Write-Host "`nDone!" -ForegroundColor Green
    
} catch {
    Write-Error "Validation failed: $($_.Exception.Message)"
    exit 1
} finally {
    Pop-Location
}