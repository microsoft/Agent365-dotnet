# Microsoft Agent 365 SDK - Package Setup Guide

This document provides a complete guide for building, validating, and publishing the Microsoft Agent 365 SDK NuGet packages.

## Package Information

The Microsoft Agent 365 SDK is organized into multiple NuGet packages:

- **Microsoft.Agents A365.Observability.Common** - Core observability and tracing infrastructure
- **Microsoft.Agents.A365.Observability.SemanticKernel** - SemanticKernel observability integration
- **Microsoft.Agents.A365.Observability.OpenAI** - OpenAI observability integration
- **Microsoft.Agents.A365.Runtime.SemanticKernel** - Runtime components for SemanticKernel
- **Microsoft.Agents.A365.Runtime.Common.AspNetCore** - ASP.NET Core runtime components
- **Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel** - Roslyn analyzers for code governance
- **Microsoft.Agents.A365.Tooling.SemanticKernel** - Tooling and utilities for SemanticKernel
- **Version**: 1.0.0
- **Target Framework**: .NET 8.0
- **License**: MIT
- **Repository**: https://github.com/microsoft/Agent365-dotnet

## Building the Package

### Prerequisites

- .NET 8.0 SDK or later
- PowerShell 5.1 or later (for build scripts)

### Manual Build

```bash
# Clean build entire solution
dotnet clean Microsoft.Agents.A365.sln

# Build the entire solution
dotnet build Microsoft.Agents.A365.sln --configuration Release

# Create NuGet packages for all projects
dotnet pack Microsoft.Agents.A365.sln --configuration Release --output ../NuGetPackages
```

### Using Build Scripts

#### PowerShell (Recommended)
```bash
# Build only
.\build.ps1

# Build and create package
.\build.ps1 -Pack

# Build with custom version
.\build.ps1 -Pack -Version "1.1.0-preview"

# Build in Debug mode
.\build.ps1 -Configuration Debug -Pack
```

#### Batch Script (Windows)
```cmd
build.cmd -Pack
```

## Package Validation

Use the validation script to verify package contents:

```bash
.\validate.ps1
```

The validation script checks for:
- Required assemblies in each package
- XML documentation for each assembly
- Package metadata (.nuspec files)
- Documentation files (README.md, CHANGELOG.md)
- Symbol packages (.snupkg)

## Publishing the Package

### Using the Publish Script

```bash
# Publish with API key parameter
.\publish.ps1 -ApiKey "your-nuget-api-key"

# Publish using environment variable
$env:NUGET_API_KEY = "your-nuget-api-key"
.\publish.ps1

# Publish to custom source
.\publish.ps1 -ApiKey "your-key" -Source "https://custom-nuget-server.com/v3/index.json"
```

### Manual Publishing

```bash
# Publish all packages
dotnet nuget push "../NuGetPackages/*.nupkg" \
  --source https://api.nuget.org/v3/index.json \
  --api-key "your-nuget-api-key"
```

## Package Contents

The generated package includes:

### Assemblies
- `lib/net8.0/Microsoft.Agents.A365.dll` - Main assembly
- `lib/net8.0/Microsoft.Agents.A365.xml` - XML documentation

### Documentation
- `README.md` - Package documentation
- `CHANGELOG.md` - Release notes and version history

### Symbol Package
- `Microsoft.Agents.A365.1.0.0.snupkg` - Debug symbols for source link support

## Automated CI/CD

The repository includes a GitHub Actions workflow (`.github/workflows/build-and-package.yml`) that:

1. **Builds** the project on every push and pull request
2. **Tests** the project (if tests exist)
3. **Packages** the NuGet package as artifacts
4. **Publishes** to NuGet.org on release events

### Setting up CI/CD

1. **Add NuGet API Key**: Add your NuGet API key as a repository secret named `NUGET_API_KEY`
2. **Create Release**: Create a GitHub release to trigger automatic publishing
3. **Monitor**: Check the Actions tab for build and publish status

## Version Management

### Version Strategy
- **Major.Minor.Patch** for stable releases (e.g., 1.0.0)
- **Major.Minor.Patch-preview** for preview releases (e.g., 1.1.0-preview)
- **Major.Minor.Patch-alpha** for alpha releases (e.g., 1.1.0-alpha)

### Updating Version

Update the version in `Directory.Build.props` to apply to all projects:

```xml
<PropertyGroup>
  <PackageVersion>1.1.0</PackageVersion>
</PropertyGroup>
```

Or update individual project files as needed.

Or override at build time:

```bash
.\build.ps1 -Pack -Version "1.1.0-preview"
```

## Dependencies

The package depends on the following NuGet packages:

- **Azure.Monitor.OpenTelemetry.Exporter** (1.4.0)
- **OpenTelemetry.Exporter.Console** (1.12.0)
- **OpenTelemetry.Exporter.OpenTelemetryProtocol** (1.12.0)
- **OpenTelemetry.Extensions.Hosting** (1.12.0)
- **OpenTelemetry.Instrumentation.AspNetCore** (1.12.0)
- **OpenTelemetry.Instrumentation.Http** (1.12.0)
- **OpenTelemetry.Instrumentation.Runtime** (1.12.0)
- **Microsoft.SourceLink.GitHub** (8.0.0) - Build-time only

## Troubleshooting

### Common Issues

1. **MSB1011 Error**: Multiple project files in folder
   - Solution: Use `dotnet build Microsoft.Agents.A365.sln` instead of `dotnet build`

2. **NU5046 Error**: Missing icon file
   - Solution: Remove `<PackageIcon>` property or add the icon file

3. **Build Warnings**: Missing XML documentation
   - Solution: Add XML documentation comments to public APIs

4. **Missing Dependencies**: Package references not found
   - Solution: Run `dotnet restore` before building

### Debug Build Issues

Enable MSBuild verbosity for detailed output:

```bash
dotnet build Microsoft.Agents.A365.sln --verbosity detailed
```

## File Structure

```
/
├── Microsoft.Agents.A365.sln            # Main solution file
├── Directory.Build.props              # Common build properties
├── build.ps1                          # Build script
├── build.cmd                          # Build script (Windows batch)
├── publish.ps1                        # Publish script
├── validate.ps1                       # Validation script
├── PACKAGE-SETUP.md                   # This file
├── Observability/                     # Observability and tracing packages
│   ├── Common/                        # Core observability infrastructure
│   │   └── Microsoft.Agents.A365.Observability.Common
│   ├── SemanticKernel/                # SemanticKernel observability
│   │   └── Microsoft.Agents.A365.Observability.SemanticKernel  
│   └── OpenAI/                        # OpenAI observability
│       └── Microsoft.Agents.A365.Observability.OpenAI
├── Runtime/                           # Runtime components
│   ├── Common/                        # Common runtime functionality
│   │   └── Microsoft.Agents.A365.Runtime.Common.AspNetCore
│   └── SemanticKernel/                # SemanticKernel runtime
│       └── Microsoft.Agents.A365.Runtime.SemanticKernel
├── DevTools/                          # Development tools
│   └── Analyzer/                      # Roslyn analyzers
│       └── Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
├── Tooling/                           # Utilities and tooling
│   └── Microsoft.Agents.A365.Tooling.SemanticKernel
└── Notification/                      # Agent notifications
    └── AgentNotification
```

## Support

For issues with the package setup or build process:

1. Check the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues)
2. Review the build logs in GitHub Actions
3. Run validation script to verify package integrity
4. Check NuGet.org for package availability after publishing
