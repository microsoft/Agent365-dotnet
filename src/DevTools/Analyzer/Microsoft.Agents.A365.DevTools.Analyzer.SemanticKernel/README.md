# Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel

A Roslyn analyzer package that enforces Microsoft Agents A365 SDK compliance and governance rules for Semantic Kernel-based agent projects.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
```

The analyzer is automatically activated once the package is installed.

## Key Rules

### A365SK0001: Use IKernelProvider

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

### A365SK0002: Configure Before Build

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

### A365SK0004: Use TenantContextHelper

```csharp
// ❌ Incorrect
var tenantId = "hardcoded-tenant";

// ✅ Correct
var tenantId = TenantContextHelper.GetTenantId(httpContext);
```

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.

