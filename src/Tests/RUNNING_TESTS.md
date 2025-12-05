# Running Unit Tests for Agent365-dotnet SDK

This guide covers setting up and running tests.

---

## Prerequisites

### 1. Install .NET SDK

Ensure you have .NET 8 SDK or later installed:

```powershell
# Verify installation
dotnet --version  # Should be 8.0.0 or later
```

### 2. Restore Dependencies

```powershell
# From repository root
dotnet restore

# Or from src directory
cd src
dotnet restore
```

---

## Test Structure

> **Note:** This structure will be updated as new tests are added.

```plaintext
Tests/
├── Runtime.Tests/                              # Runtime core tests
├── Microsoft.Agents.A365.Observability.Runtime.Tests/          # Observability runtime tests
├── Microsoft.Agents.A365.Observability.Hosting.Tests/          # Observability hosting tests
├── Microsoft.Agents.A365.Observability.Extension.Tests/        # Observability extension tests
└── Microsoft.Agents.A365.Notifications.Tests/                  # Notifications tests
```

---

## Running Tests in VS Code (Optional)

### Test Explorer

1. Install **C# Dev Kit** extension
2. Click the beaker icon in the Activity Bar or press `Ctrl+Shift+P` → "Test: Focus on Test Explorer View"
3. Click the play button to run tests (all/folder/file/individual)
4. Right-click → "Debug Test" to debug with breakpoints

### Command Palette

- `Test: Run All Tests`
- `Test: Run Tests in Current File`
- `Test: Debug Tests in Current File`

---

## Running Tests from Command Line

```powershell
# Run all tests
dotnet test

# Run specific module/file
dotnet test Tests/Runtime.Tests/
dotnet test Tests/Runtime.Tests/Microsoft.Agents.A365.Runtime.Tests.csproj

# Run with options
dotnet test --verbosity detailed              # Verbose
dotnet test --filter "FullyQualifiedName~Utility"  # Pattern matching
dotnet test --logger "console;verbosity=detailed"  # Detailed logging
```

---

## Generating Reports

### HTML Reports

```powershell
# Install coverage tools (one-time)
dotnet tool install --global dotnet-reportgenerator-globaltool

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate HTML report
reportgenerator -reports:"./coverage/**/coverage.cobertura.xml" -targetdir:"./coverage-report" -reporttypes:Html

# View reports
start ./coverage-report/index.html
```

### CI/CD Reports

```powershell
# XML reports for CI/CD pipelines
dotnet test --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage"

# View reports
start TestResults/test-results.trx
```

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| **Test loading failed** | Clean and rebuild: `dotnet clean`, then `dotnet build` |
| **Tests not discovered** | Verify `[Fact]` or `[Theory]` attributes, refresh Test Explorer |
| **Build failures** | Run `dotnet restore`, check package references |
| **Coverage not generated** | Verify `coverlet.collector` package is referenced |

### Fix Steps

If tests fail to discover or build errors occur:

**1. Clean and Rebuild**

```powershell
dotnet clean
dotnet restore
dotnet build
```

**2. Clear Test Cache**

```powershell
Remove-Item -Recurse -Force bin, obj
dotnet restore
dotnet build
dotnet test
```

**3. Restart VS Code**

- Close completely and reopen
- Wait for C# extension to reload
- Refresh Test Explorer
