# GitHub Actions Workflows

This directory contains GitHub Actions workflows for the Kairo repository.

## CI Workflow (ci.yml)

The main CI workflow builds, tests, and prepares all three SDK packages for publishing:

### Jobs

#### Python SDK (`python-sdk`)
- **Matrix**: Python 3.11 and 3.12
- **Steps**:
  - Install dependencies and build tools
  - Optional linting with ruff
  - Build Python package using `python -m build`
  - Run tests with pytest
  - *Publishing to PyPI (commented out for now)*

#### JavaScript/Node.js SDK (`javascript-sdk`)
- **Matrix**: Node.js 18 and 20
- **Steps**:
  - Install npm dependencies
  - Run ESLint for code quality
  - Build TypeScript to JavaScript
  - Run Jest tests
  - *Publishing to NPM (commented out for now)*

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

### Publishing

All publishing steps are currently commented out as requested. To enable publishing:

1. **Python**: Uncomment the PyPI publishing step and add `PYPI_API_TOKEN` secret
2. **JavaScript**: Uncomment the NPM publishing step and add `NPM_TOKEN` secret  
3. **.NET**: Uncomment the NuGet publishing step and add `NUGET_API_KEY` secret

### Caching

The workflow uses dependency caching to speed up builds:
- Python: pip cache
- JavaScript: npm cache
- .NET: NuGet packages are cached automatically by the dotnet CLI