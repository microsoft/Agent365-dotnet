# Microsoft.Agents.A365.DevTools.Analyzer.OpenAI

This package contains custom Roslyn analyzers for enforcing Microsoft Agents A365 SDK compliance and governance in OpenAI-based agent projects.

## Included Rules
- A365OAI0001: Direct ChatClient access or storage is not allowed
- A365OAI0002: Direct OpenAIClient access or storage is not allowed
- A365OAI0004: Tenant/worker ID access enforcement
- A365OAI0005: ChatClient provider must be configured properly for multi-tenant scenarios
- A365OAI0006: Functions must be accessed via IOpenAIFunctionProvider for tenant isolation
- A365OAI0008: Providers must be registered with delegate-based configuration
- A365OAI0009: Tenant and Worker IDs must not be hardcoded
- A365OAI0010: Data storage must be tenant-isolated to prevent cross-tenant access
- A365OAI0011: Agent classes must use providers instead of direct OpenAI clients

See AnalyzerDocumentation.md for details on rule enforcement and usage.

## 📦 Creating a Local NuGet Package

To build and generate a local NuGet package for this project:

1. **Ensure prerequisites:**
   - .NET SDK 8.0 or later installed
   - This repository cloned locally

2. **Build and pack the project:**
   - Open a terminal in the project directory:
     ```pwsh
     cd ./Microsoft.Agents.A365.DevTools.Analyzer.OpenAI
     dotnet build -c Release
     ```
   - The build will automatically generate the NuGet package in `nupkgs/`.

3. **Verify the package:**
   - Check for the `.nupkg` file in:
     ```
     ./Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/nupkgs/
     ```

4. **Consume the package locally:**
   - In your agent project, add the following to your `.csproj`:
     ```xml
     <PropertyGroup>
       <RestoreSources>$(RestoreSources);../Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/nupkgs</RestoreSources>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.Agents.A365.DevTools.Analyzer.OpenAI" Version="1.0.0" />
     </ItemGroup>
     ```
   - Run `dotnet restore` in your agent project to use the local package.

## Analyzers

### A365OAI0001 - ChatClient Direct Access
Prevents direct usage of `ChatClient` in favor of `IChatClientProvider`.

**Problem**: Direct ChatClient usage breaks multi-tenant isolation
**Fix**: Use `IChatClientProvider.GetChatClient(tenantId, workerId)`

### A365OAI0002 - OpenAIClient Direct Access  
Prevents direct usage of `OpenAIClient` in favor of `IChatClientProvider`.

**Problem**: Direct OpenAIClient usage breaks multi-tenant isolation
**Fix**: Use `IChatClientProvider.GetChatClient(tenantId, workerId)`

### A365OAI0004 - Tenant/Worker ID Access
Ensures proper tenant/worker ID extraction via TenantContextHelper.

**Problem**: Direct header/claim access is inconsistent
**Fix**: Use `TenantContextHelper.GetTenantId(HttpContext)` and `TenantContextHelper.GetWorkerId(HttpContext)`

### A365OAI0005 - ChatClient Provider Usage
Ensures proper OpenAI provider registration and configuration.

**Problem**: Missing or improper provider setup
**Fix**: Register `IChatClientProvider` and `IOpenAIFunctionProvider` in DI

### A365OAI0006 - Function Provider Enforcement
Functions must be accessed via IOpenAIFunctionProvider for tenant isolation.

**Problem**: Direct function operations bypass multi-tenant governance
**Fix**: Use `IOpenAIFunctionProvider.GetAvailableTools()` and `ExecuteFunctionAsync()`

### A365OAI0008 - Provider Registration Validation
Providers must be registered with delegate-based configuration.

**Problem**: Direct client registration bypasses governance-approved factory patterns
**Fix**: Register providers using delegate-based factories with proper tenant isolation

### A365OAI0009 - Hardcoded Tenant/Worker Prevention
Tenant and Worker IDs must not be hardcoded.

**Problem**: Hardcoded tenant/worker IDs bypass multi-tenant isolation and create security risks
**Fix**: Extract tenant/worker IDs from HttpContext using TenantContextHelper methods

### A365OAI0010 - Cross-Tenant Data Access Prevention
Data storage must be tenant-isolated to prevent cross-tenant access.

**Problem**: Shared storage without tenant isolation creates data leakage risks
**Fix**: Use tenant-scoped storage patterns with tenantId/workerId in keys

### A365OAI0011 - Agent Construction Validation
Agent classes must use providers instead of direct OpenAI clients.

**Problem**: Agent classes with direct client dependencies cannot support multi-tenancy
**Fix**: Use provider-based dependency injection (IChatClientProvider, IOpenAIFunctionProvider)

## Local Development Note
If you want analyzer diagnostics to run immediately during development, add the analyzer DLL directly in your agent project's `.csproj`:

```xml
<ItemGroup>
  <Analyzer Include="..\Microsoft.Agents.A365.DevTools.Analyzer.OpenAI\bin\Debug\netstandard2.0\Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.dll" />
</ItemGroup>
```
This ensures analyzers run on every build, even if the NuGet package is not published or restored.

## Installation

```xml
<PackageReference Include="Microsoft.Agents.A365.DevTools.Analyzer.OpenAI" Version="1.0.0" />
```

## Usage

The analyzers automatically activate when you reference the package. No additional configuration required.

## Troubleshooting
- If the package is not generated, ensure `<IsPackable>true</IsPackable>` and `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` are set in the `.csproj`.
- If you change the version, rebuild to update the `.nupkg` file.

For more details on each rule, see the [AnalyzerDocumentation.md](./AnalyzerDocumentation.md).

## License
Copyright (c) Microsoft. All rights reserved.