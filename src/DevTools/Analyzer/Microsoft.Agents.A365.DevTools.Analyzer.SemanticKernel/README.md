# Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel

A Roslyn analyzer package that enforces Microsoft Agent 365 SDK compliance and governance rules for Semantic Kernel-based agent projects.

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

## 📋 **Telemetry**
 
Data Collection. The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
 
## Trademarks
 
*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.

