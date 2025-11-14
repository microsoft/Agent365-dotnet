# GitHub Actions Workflows

This directory contains GitHub Actions workflows for the Agent 365 .NET SDK repository.

## CI Workflow (ci.yml)

The main CI workflow builds, tests, and prepares SDK packages for publishing:

### Jobs

#### .NET SDK (`dotnet-sdk`)
- **Matrix**: .NET 8.0.x
- **Steps**:
  - Restore NuGet dependencies
  - Build solution in Release configuration
  - Run unit tests
  - Pack NuGet packages
  - Upload packages as artifacts
  - *Publishing to NuGet (commented out for now)*

### Triggers

- **Push**: Triggers on pushes to `main` or `master` branches
- **Pull Request**: Triggers on pull requests targeting `main` or `master` branches
