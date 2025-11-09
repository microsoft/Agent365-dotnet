# Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel

A Roslyn analyzer package that enforces Microsoft Agents A365 SDK compliance and governance rules for Semantic Kernel-based agent projects. This analyzer helps developers follow best practices for multi-tenant scenarios, proper kernel management, and secure service configuration.

## Overview

This analyzer detects and prevents common issues in Semantic Kernel-based agent applications, including:

- Direct kernel access that bypasses governance
- Improper kernel lifecycle management
- Missing or incorrect service registrations
- Hardcoded tenant or worker IDs
- Duplicate agent registrations

## Features

- **Compile-Time Enforcement**: Catch governance violations during development
- **Multi-Tenant Support**: Ensures proper tenant isolation patterns
- **Kernel Management**: Validates correct kernel provider usage
- **Service Configuration**: Enforces proper chat completion service registration
- **IDE Integration**: Works seamlessly with Visual Studio and VS Code

## Installation

```bash
dotnet add package Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
```

The analyzer is automatically activated once the package is installed.

## Analyzer Rules

### A365SK0001: Direct Kernel Access Not Allowed

**Description**: Direct Kernel access or storage is not allowed. Use IKernelProvider instead.

**Severity**: Error

**Rationale**: Direct kernel usage prevents multi-tenant isolation and proper governance enforcement.

**Example**:

```csharp
// ❌ Incorrect
private Kernel _kernel;
public MyAgent(Kernel kernel)
{
    _kernel = kernel;
}

// ✅ Correct
private readonly IKernelProvider _kernelProvider;
public MyAgent(IKernelProvider kernelProvider)
{
    _kernelProvider = kernelProvider;
}

// Usage
var kernel = _kernelProvider.GetKernel(tenantId, workerId);
```

### A365SK0002: Kernel Builder Usage Validation

**Description**: Kernel retrieval before builder.Build() and duplicate agent registration are not allowed.

**Severity**: Error

**Rationale**: Accessing the kernel before the builder is fully configured can lead to incomplete service registration. Duplicate agent registrations cause conflicts.

**Example**:

```csharp
// ❌ Incorrect
var builder = Kernel.CreateBuilder();
var kernel = builder.Build(); // Too early
builder.Services.AddSingleton<IService>(); // After Build()

// ✅ Correct
var builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IService>();
var kernel = builder.Build(); // After all configuration
```

### A365SK0003: Chat Completion Service Registration

**Description**: Chat completion service registration must follow proper patterns.

**Severity**: Error

**Rationale**: Proper service registration ensures consistent AI service configuration across tenants.

**Example**:

```csharp
// ✅ Correct
builder.Services.AddAzureOpenAIChatCompletion(
    deploymentName: "gpt-4",
    endpoint: endpoint,
    credentials: credentials
);
```

### A365SK0004: Tenant/Worker ID Access Enforcement

**Description**: Tenant and worker IDs must be accessed through proper context mechanisms.

**Severity**: Error

**Rationale**: Consistent tenant/worker ID extraction ensures proper multi-tenant isolation.

**Example**:

```csharp
// ❌ Incorrect
var tenantId = "hardcoded-tenant";
var workerId = Request.Headers["X-Worker-Id"];

// ✅ Correct
var tenantId = TenantContextHelper.GetTenantId(httpContext);
var workerId = TenantContextHelper.GetWorkerId(httpContext);
```

## Configuration

### .editorconfig

Configure analyzer severity and behavior:

```ini
[*.cs]

# Semantic Kernel Analyzer Rules
dotnet_diagnostic.A365SK0001.severity = error
dotnet_diagnostic.A365SK0002.severity = error
dotnet_diagnostic.A365SK0003.severity = error
dotnet_diagnostic.A365SK0004.severity = error
```

### Suppressing Warnings

For legitimate cases where analyzer warnings need to be suppressed:

```csharp
#pragma warning disable A365SK0001 // Reason for suppression
var testKernel = new Kernel();
#pragma warning restore A365SK0001
```

## Creating a Local NuGet Package

To build and generate a local NuGet package for this project:

### Prerequisites

- .NET SDK 8.0 or later installed
- This repository cloned locally

### Build and Pack

1. **Build the project:**

   ```bash
   cd ./src/DevTools/Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
   dotnet build -c Release
   ```

2. **Verify the package:**

   The build will automatically generate the NuGet package in `nupkgs/`:

   ```text
   ./Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/nupkgs/
   ```

### Consume Locally

In your agent project, add the following to your `.csproj`:

```xml
<PropertyGroup>
  <RestoreSources>$(RestoreSources);../Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/nupkgs</RestoreSources>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel" Version="1.0.0" />
</ItemGroup>
```

Run `dotnet restore` in your agent project to use the local package.

## Local Development

If you want analyzer diagnostics to run immediately during development, add the analyzer DLL directly in your agent project's `.csproj`:

```xml
<ItemGroup>
  <Analyzer Include="..\Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel\bin\Debug\netstandard2.0\Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.dll" />
</ItemGroup>
```

This ensures analyzers run on every build, even if the NuGet package is not published or restored.

## Best Practices

1. **Always Use IKernelProvider**: Never store or pass Kernel instances directly
2. **Complete Configuration Before Build**: Add all services before calling `builder.Build()`
3. **Use TenantContextHelper**: Always extract tenant/worker IDs through the helper
4. **Register Services Properly**: Follow the recommended patterns for service registration
5. **Test Multi-Tenant Scenarios**: Ensure your code works correctly with multiple tenants

## Troubleshooting

- **Package not generated**: Ensure `<IsPackable>true</IsPackable>` and `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` are set in the `.csproj`
- **Analyzer not running**: Verify the package reference is correctly added and restore packages
- **Version conflicts**: If you change the version, rebuild to update the `.nupkg` file

For more details on each rule, see the [AnalyzerDocumentation.md](./AnalyzerDocumentation.md).

## Related Documentation

- [DevTools Overview](../../../README.md)
- [Analyzer Documentation](./AnalyzerDocumentation.md)
- [OpenAI Analyzer](../Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.

