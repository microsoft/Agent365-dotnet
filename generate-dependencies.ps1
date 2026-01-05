# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Generates a dependency diagram in Mermaid format for Microsoft Agent 365 SDK packages.

.DESCRIPTION
    This script traverses the src directory starting from dirs.proj and analyzes
    project files to determine dependencies between packages. It generates a
    DEPENDENCIES.md file with a Mermaid diagram showing the relationships.
#>

# Define package types and their styling
$packageTypes = @{
    'Microsoft.Agents.A365.Notifications' = @{ Type = 'Notifications'; Fill = '#ffcdd2'; Stroke = '#c62828'; Color = '#280505' }
    'Microsoft.Agents.A365.Observability.Runtime' = @{ Type = 'Observability'; Fill = '#c8e6c9'; Stroke = '#2e7d32'; Color = '#142a14' }
    'Microsoft.Agents.A365.Observability.Hosting' = @{ Type = 'Observability Extensions'; Fill = '#e8f5e9'; Stroke = '#66bb6a'; Color = '#1f3d1f' }
    'Microsoft.Agents.A365.Observability.Extensions.AgentFramework' = @{ Type = 'Observability Extensions'; Fill = '#e8f5e9'; Stroke = '#66bb6a'; Color = '#1f3d1f' }
    'Microsoft.Agents.A365.Observability.Extensions.OpenAI' = @{ Type = 'Observability Extensions'; Fill = '#e8f5e9'; Stroke = '#66bb6a'; Color = '#1f3d1f' }
    'Microsoft.Agents.A365.Observability.Extensions.SemanticKernel' = @{ Type = 'Observability Extensions'; Fill = '#e8f5e9'; Stroke = '#66bb6a'; Color = '#1f3d1f' }
    'Microsoft.Agents.A365.Runtime' = @{ Type = 'Runtime'; Fill = '#bbdefb'; Stroke = '#1565c0'; Color = '#0d1a26' }
    'Microsoft.Agents.A365.Tooling' = @{ Type = 'Tooling'; Fill = '#ffe0b2'; Stroke = '#e65100'; Color = '#331a00' }
    'Microsoft.Agents.A365.Tooling.Extensions.AgentFramework' = @{ Type = 'Tooling Extensions'; Fill = '#fff3e0'; Stroke = '#fb8c00'; Color = '#4d2600' }
    'Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry' = @{ Type = 'Tooling Extensions'; Fill = '#fff3e0'; Stroke = '#fb8c00'; Color = '#4d2600' }
    'Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel' = @{ Type = 'Tooling Extensions'; Fill = '#fff3e0'; Stroke = '#fb8c00'; Color = '#4d2600' }
}

# Function to extract package name from csproj file
function Get-PackageName {
    param (
        [string]$ProjectPath
    )
    
    [xml]$project = Get-Content $ProjectPath
    $packageId = $project.Project.PropertyGroup.PackageId | Where-Object { $_ } | Select-Object -First 1
    
    if ($packageId) {
        return $packageId
    }
    
    # Fallback to AssemblyName if PackageId not found
    $assemblyName = $project.Project.PropertyGroup.AssemblyName | Where-Object { $_ } | Select-Object -First 1
    return $assemblyName
}

# Function to extract project references from csproj file
function Get-ProjectReferences {
    param (
        [string]$ProjectPath
    )
    
    [xml]$project = Get-Content $ProjectPath
    $references = @()
    
    foreach ($itemGroup in $project.Project.ItemGroup) {
        if ($itemGroup.ProjectReference) {
            foreach ($projRef in $itemGroup.ProjectReference) {
                $refPath = $projRef.Include
                if ($refPath) {
                    # Resolve relative path
                    $projectDir = Split-Path -Parent $ProjectPath
                    $fullPath = Join-Path $projectDir $refPath
                    $fullPath = [System.IO.Path]::GetFullPath($fullPath)
                    
                    if (Test-Path $fullPath) {
                        $refPackageName = Get-PackageName -ProjectPath $fullPath
                        if ($refPackageName -and $packageTypes.ContainsKey($refPackageName)) {
                            $references += $refPackageName
                        }
                    }
                }
            }
        }
    }
    
    return $references
}

# Function to find all project files
function Get-AllProjects {
    param (
        [string]$StartPath
    )
    
    $projects = @()
    
    # Define the specific projects we want to include
    $projectPaths = @(
        'src\Notification\Microsoft.Agents.A365.Notifications\Microsoft.Agents.A365.Notifications.csproj',
        'src\Observability\Runtime\Microsoft.Agents.A365.Observability.Runtime.csproj',
        'src\Observability\Hosting\Microsoft.Agents.A365.Observability.Hosting.csproj',
        'src\Observability\Extensions\AgentFramework\Microsoft.Agents.A365.Observability.Extensions.AgentFramework.csproj',
        'src\Observability\Extensions\OpenAI\Microsoft.Agents.A365.Observability.Extensions.OpenAI.csproj',
        'src\Observability\Extensions\SemanticKernel\Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.csproj',
        'src\Runtime\Core\Microsoft.Agents.A365.Runtime.csproj',
        'src\Tooling\Core\Microsoft.Agents.A365.Tooling.csproj',
        'src\Tooling\Extensions\AgentFramework\Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.csproj',
        'src\Tooling\Extensions\AzureAIFoundry\Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry.csproj',
        'src\Tooling\Extensions\SemanticKernel\Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.csproj'
    )
    
    foreach ($projPath in $projectPaths) {
        $fullPath = Join-Path $StartPath $projPath
        if (Test-Path $fullPath) {
            $projects += $fullPath
        }
    }
    
    return $projects
}

# Main script execution
Write-Host "Analyzing project dependencies..." -ForegroundColor Cyan

$repoRoot = $PSScriptRoot
$allProjects = Get-AllProjects -StartPath $repoRoot

# Build dependency graph
$dependencies = @{}

foreach ($project in $allProjects) {
    $packageName = Get-PackageName -ProjectPath $project
    
    if ($packageName -and $packageTypes.ContainsKey($packageName)) {
        $refs = Get-ProjectReferences -ProjectPath $project
        $dependencies[$packageName] = $refs
        Write-Host "  Found: $packageName with $($refs.Count) dependencies" -ForegroundColor Gray
    }
}

# Generate Mermaid diagram
$mermaidContent = @"
# Microsoft Agent 365 SDK .NET Package Dependencies

This diagram shows the internal dependencies between Microsoft Agent 365 SDK .NET packages.

``````mermaid
graph LR
"@

# Add all nodes with styling
$nodeMap = @{}

foreach ($package in $packageTypes.Keys | Sort-Object) {
    # Create meaningful node ID from package name
    # Remove prefix and special characters
    $id = $package -replace 'Microsoft\.Agents\.A365\.', '' -replace '\.', ''
    $nodeMap[$package] = $id
    
    $style = $packageTypes[$package]
    
    $mermaidContent += "`r`n    $id[""$package""]"
}

# Add dependencies (edges)
$mermaidContent += "`r`n"
foreach ($package in $dependencies.Keys | Sort-Object) {
    $fromId = $nodeMap[$package]
    foreach ($dep in $dependencies[$package] | Sort-Object) {
        if ($nodeMap.ContainsKey($dep)) {
            $toId = $nodeMap[$dep]
            $mermaidContent += "`r`n    $fromId --> $toId"
        }
    }
}

# Add styling - group by package type
$mermaidContent += "`r`n"

# Group packages by type and apply styling once per type
$typeStyles = @{}
foreach ($package in $packageTypes.Keys) {
    $type = $packageTypes[$package].Type
    if (-not $typeStyles.ContainsKey($type)) {
        $typeStyles[$type] = @{
            Fill = $packageTypes[$package].Fill
            Stroke = $packageTypes[$package].Stroke
            Color = $packageTypes[$package].Color
            Nodes = @()
        }
    }
    if ($nodeMap.ContainsKey($package)) {
        $typeStyles[$type].Nodes += $nodeMap[$package]
    }
}

# Apply styling once per package type
foreach ($type in $typeStyles.Keys | Sort-Object) {
    $style = $typeStyles[$type]
    $nodeIds = $style.Nodes -join ','
    $mermaidContent += "`r`n    classDef $($type -replace ' ', '') fill:$($style.Fill),stroke:$($style.Stroke),color:$($style.Color)"
}

# Assign classes to nodes
$mermaidContent += "`r`n"
foreach ($type in $typeStyles.Keys | Sort-Object) {
    $nodeIds = $typeStyles[$type].Nodes -join ','
    $className = $type -replace ' ', ''
    $mermaidContent += "`r`n    class $nodeIds $className"
}

$mermaidContent += "`r`n``````"
# Add legend
$mermaidContent += "`r`n`r`n## Package Types`r`n"
$mermaidContent += "`r`n- **Notifications** (Red): Notification and messaging extensions"
$mermaidContent += "`r`n- **Runtime** (Blue): Core runtime components"
$mermaidContent += "`r`n- **Observability** (Green): Telemetry and monitoring core"
$mermaidContent += "`r`n- **Observability Extensions** (Light Green): Framework-specific observability integrations"
$mermaidContent += "`r`n- **Tooling** (Orange): Agent tooling SDK core"
$mermaidContent += "`r`n- **Tooling Extensions** (Light Orange): Framework-specific tooling integrations"

# Write to file
$outputPath = Join-Path $repoRoot "DEPENDENCIES.md"
$mermaidContent | Out-File -FilePath $outputPath -Encoding UTF8

Write-Host "`nDependency diagram generated successfully!" -ForegroundColor Green
Write-Host "Output: $outputPath" -ForegroundColor Cyan
