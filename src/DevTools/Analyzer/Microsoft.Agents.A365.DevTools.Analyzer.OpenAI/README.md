# Microsoft.Agents.A365.DevTools.Analyzer.OpenAI

A Roslyn analyzer package that enforces Microsoft Agents A365 SDK compliance and governance rules for OpenAI-based agent projects. This analyzer helps developers follow best practices for multi-tenant scenarios, security, and proper resource isolation.

## Overview

This analyzer detects and prevents common issues in OpenAI-based agent applications, including:

- Direct client access that bypasses governance
- Hardcoded tenant or worker IDs
- Improper multi-tenant configuration
- Missing tenant isolation in data storage
- Incorrect provider registration patterns

## Features

- **Compile-Time Enforcement**: Catch governance violations during development
- **Multi-Tenant Support**: Ensures proper tenant isolation patterns
- **Security Best Practices**: Prevents hardcoded credentials and improper client access
- **Provider Pattern Enforcement**: Validates correct usage of provider-based architecture
- **IDE Integration**: Works seamlessly with Visual Studio and VS Code

## Installation

```bash
dotnet add package Microsoft.Agents.A365.DevTools.Analyzer.OpenAI
```

The analyzer is automatically activated once the package is installed.

## Analyzer Rules

### A365OAI0001: Direct ChatClient Access Not Allowed

**Description**: Direct ChatClient access or storage is not allowed. Use IChatClientProvider instead.

**Severity**: Error

**Example**:

```csharp
// ❌ Incorrect
private ChatClient _chatClient;

// ✅ Correct
private readonly IChatClientProvider _chatClientProvider;
```

### A365OAI0002: Direct OpenAIClient Access Not Allowed

**Description**: Direct OpenAIClient access or storage is not allowed. Use IOpenAIClientProvider instead.

**Severity**: Error

**Example**:

```csharp
// ❌ Incorrect
private OpenAIClient _openAIClient;

// ✅ Correct
private readonly IOpenAIClientProvider _openAIClientProvider;
```

### A365OAI0004: Tenant/Worker ID Access Enforcement

**Description**: Tenant and worker IDs must be accessed through proper context mechanisms.

**Severity**: Error

### A365OAI0005: ChatClient Provider Configuration

**Description**: ChatClient provider must be configured properly for multi-tenant scenarios.

**Severity**: Error

### A365OAI0006: Function Access via Provider

**Description**: Functions must be accessed via IOpenAIFunctionProvider for tenant isolation.

**Severity**: Error

**Example**:

```csharp
// ❌ Incorrect
var function = new MyFunction();

// ✅ Correct
var function = _functionProvider.GetFunction<MyFunction>(tenantId, workerId);
```

### A365OAI0008: Provider Registration Pattern

**Description**: Providers must be registered with delegate-based configuration.

**Severity**: Error

**Example**:

```csharp
// ✅ Correct
services.AddChatClientProvider((tenantId, workerId) => 
{
    return new ChatClient(endpoint, credential);
});
```

### A365OAI0009: No Hardcoded IDs

**Description**: Tenant and Worker IDs must not be hardcoded.

**Severity**: Error

**Example**:

```csharp
// ❌ Incorrect
var tenantId = "12345";
var workerId = "worker1";

// ✅ Correct
var tenantId = context.GetTenantId();
var workerId = context.GetWorkerId();
```

### A365OAI0010: Tenant-Isolated Data Storage

**Description**: Data storage must be tenant-isolated to prevent cross-tenant access.

**Severity**: Error

### A365OAI0011: Use Providers in Agent Classes

**Description**: Agent classes must use providers instead of direct OpenAI clients.

**Severity**: Error

## Configuration

### .editorconfig

Configure analyzer severity and behavior:

```ini
[*.cs]

# OpenAI Analyzer Rules
dotnet_diagnostic.A365OAI0001.severity = error
dotnet_diagnostic.A365OAI0002.severity = error
dotnet_diagnostic.A365OAI0004.severity = error
dotnet_diagnostic.A365OAI0005.severity = error
dotnet_diagnostic.A365OAI0006.severity = error
dotnet_diagnostic.A365OAI0008.severity = error
dotnet_diagnostic.A365OAI0009.severity = error
dotnet_diagnostic.A365OAI0010.severity = error
dotnet_diagnostic.A365OAI0011.severity = error
```

### Suppressing Warnings

For legitimate cases where analyzer warnings need to be suppressed:

```csharp
#pragma warning disable A365OAI0001 // Reason for suppression
var testClient = new ChatClient(endpoint, credential);
#pragma warning restore A365OAI0001
```

## Creating a Local NuGet Package

To build and generate a local NuGet package for this project:

### Prerequisites

- .NET SDK 8.0 or later installed
- This repository cloned locally

### Build and Pack

1. **Build the project:**

   ```bash
   cd ./src/DevTools/Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI
   dotnet build -c Release
   ```

2. **Verify the package:**

   The build will automatically generate the NuGet package in `nupkgs/`:

   ```
   ./Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/nupkgs/
   ```

### Consume Locally

In your agent project, add the following to your `.csproj`:

```xml
<PropertyGroup>
  <RestoreSources>$(RestoreSources);../Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/nupkgs</RestoreSources>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Agents.A365.DevTools.Analyzer.OpenAI" Version="1.0.0" />
</ItemGroup>
```

Run `dotnet restore` in your agent project to use the local package.

## Related Documentation

- [DevTools Overview](../../../README.md)
- [Analyzer Documentation](./AnalyzerDocumentation.md)
- [Semantic Kernel Analyzer](../Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
