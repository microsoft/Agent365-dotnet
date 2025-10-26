# Microsoft.A365.Runtime.Common.AspNetCore

ASP.NET Core integration helpers for Microsoft Microsoft Agents A365 SDK, providing HttpContext-based tenant and worker ID extraction for multi-tenant agent applications.

## Features

- **Tenant Context Extraction**: Extract tenant IDs from HttpContext using standardized patterns
- **Worker Context Extraction**: Extract worker IDs from HttpContext for multi-worker scenarios  
- **Multiple Source Support**: Checks user claims, request headers, and request items
- **Null Safety**: Proper null handling and validation
- **Performance Optimized**: Minimal overhead for context extraction

## Installation

```bash
dotnet add package Microsoft.A365.Runtime.Common.AspNetCore
```

## Usage

### Basic Tenant/Worker ID Extraction

```csharp
using Microsoft.A365.Runtime.Common.AspNetCore;

app.MapPost("/api/agent", async (HttpContext context) =>
{
    // Extract tenant and worker context
    var tenantId = TenantContextHelper.GetTenantId(context);
    var workerId = TenantContextHelper.GetWorkerId(context);
    
    // Use with KernelProvider
    var kernel = kernelProvider.GetKernel(tenantId ?? "default", workerId ?? "default");
    
    // Process request...
});
```

### Integration with Governance

```csharp
app.MapPost("/api/process", async (HttpContext context, IKernelProvider kernelProvider) =>
{
    // Apply governance first
    await context.Services.ApplyGovernanceAsync(logger);
    
    // Extract context
    var tenantId = TenantContextHelper.GetTenantId(context);
    var workerId = TenantContextHelper.GetWorkerId(context);
    
    // Get governed kernel
    var kernel = kernelProvider.GetKernel(tenantId ?? "default", workerId ?? "default");
    
    // Process with multi-tenant isolation...
});
```

## Context Sources

The helper checks for tenant/worker IDs in this priority order:

1. **User Claims**: `tenant_id`, `worker_id`
2. **Request Headers**: `X-Tenant-Id`, `X-Worker-Id`  
3. **Request Items**: `TenantId`, `WorkerId`

## Why Separate Package?

This package is separate from the core SemanticKernel integration to:

- **Avoid Unnecessary Dependencies**: Core SK integration doesn't need ASP.NET Core
- **Enable Flexible Deployment**: Console apps, background services don't need web dependencies
- **Follow Single Responsibility**: Each package has a focused purpose
- **Reduce Package Size**: Consumers only get what they need

## Related Packages

- **Microsoft.A365.Runtime.SemanticKernel**: Core SemanticKernel integration and KernelProvider
- **Microsoft.A365.DevTools.Analyzer.SemanticKernel**: Roslyn analyzers for governance enforcement
- **Microsoft.A365.Observability**: Core observability and tracing infrastructure

## License

This project is licensed under the MIT License - see the LICENSE file for details.