# How to Run Notifications Tests

This document provides step-by-step instructions for running the streamlined unit tests for the Microsoft.Agents.A365.Notifications library.

## 📋 Prerequisites

- .NET 8.0 SDK installed
- Visual Studio Code or Visual Studio (optional)
- Git (for cloning the repository)
- `reportgenerator` tool (for coverage reports): `dotnet tool install -g dotnet-reportgenerator-globaltool`

## 🚀 Quick Start

### 1. Clone and Navigate to Repository
```powershell
git clone https://github.com/microsoft/Agent365.git
cd Agent365\dotnet\sdk\Tests\Microsoft.Agents.A365.Notifications.Tests
```

### 2. Run All Notifications Tests
```powershell
dotnet test --logger "console;verbosity=normal"
```

### 3. Expected Output
```
Test Run Successful.
Total tests: 35
     Passed: 35
 Total time: ~3.5 seconds
```

## 🔧 Detailed Instructions

### PowerShell/Command Line

1. **Open PowerShell or Command Prompt**

2. **Navigate to the test directory:**
   ```powershell
   cd path\to\Agent365\dotnet\sdk\Tests\Microsoft.Agents.A365.Notifications.Tests
   ```

3. **Run tests with different options:**
   ```powershell
   # Basic test run
   dotnet test
   
   # With detailed output (recommended)
   dotnet test --logger "console;verbosity=normal"
   
   # With code coverage collection
   dotnet test --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"
   
   # Run specific test class
   dotnet test --filter "ClassName~AgentNotificationTests"
   
   # Run specific test method
   dotnet test --filter "MethodName~IsValidSubChannel"
   ```

## 📊 Code Coverage Analysis

### Generate Coverage Report
```powershell
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"

# Generate HTML report
reportgenerator -reports:"TestResults\**\coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"

# Open report
start coverage-report\index.html
```

### Expected Coverage Results
- **Line Coverage**: 81.2%+ ✅
- **Branch Coverage**: 79%+
- **Method Coverage**: 88%+

## 🧪 Test Categories

### Core Business Logic (Priority 1)
```powershell
# Test notification routing and validation
dotnet test --filter "ClassName~AgentNotificationTests"

# Test activity processing logic  
dotnet test --filter "ClassName~AgentNotificationActivityTests"
```

### Extension Methods (Priority 2)
```powershell
# Test entity extraction methods
dotnet test --filter "ClassName~ActivityExtensionTests"
```

### Data Layer (Priority 3)
```powershell
# Test serialization functionality
dotnet test --filter "ClassName~SerializationTests"
```

## 🐛 Troubleshooting

### Common Issues

#### Issue: Build Errors
```powershell
# Clean and restore
dotnet clean
dotnet restore
dotnet build
```

#### Issue: Test Discovery Problems
```powershell
# Rebuild test project
dotnet build --no-restore
dotnet test --no-build
```

#### Issue: Coverage Tool Missing
```powershell
# Install reportgenerator globally
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### Performance Notes

- **Test Execution Time**: ~3-4 seconds (optimized from previous 7+ seconds)
- **Memory Usage**: Low (streamlined test suite)
- **Parallel Execution**: Supported (tests are isolated)

## 📈 Continuous Integration

### CI/CD Pipeline Integration
```yaml
# Azure DevOps / GitHub Actions example
- name: Run Notifications Tests
  run: |
    dotnet test sdk/Tests/Microsoft.Agents.A365.Notifications.Tests/ 
      --collect:"XPlat Code Coverage" 
      --logger "trx" 
      --results-directory TestResults/

- name: Generate Coverage Report  
  run: |
    reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" 
      -targetdir:"coverage-report" 
      -reporttypes:"Cobertura;HtmlInline_AzurePipelines"
```

### Quality Gates
- ✅ All tests must pass
- ✅ Coverage must be ≥80%
- ✅ No build warnings in test project

## 🎯 Test Philosophy

This streamlined test suite focuses on:
- **Core business logic** rather than trivial code
- **Security-critical validation** 
- **Complex processing scenarios**
- **Error handling and edge cases**

**Goal**: Meaningful coverage that provides confidence in production reliability.