# Microsoft.Agents.A365.Runtime

ASP.NET Core integration helpers for Microsoft Agents 365 Runtime, providing HttpContext-based tenant and worker ID extraction for multi-tenant agent applications.

## Features

- **Tenant Context Extraction**: Extract tenant IDs from HttpContext using standardized patterns
- **Worker Context Extraction**: Extract worker IDs from HttpContext for multi-worker scenarios
- **Multiple Source Support**: Checks user claims, request headers, and request items
- **Null Safety**: Proper null handling and validation
- **Performance Optimized**: Minimal overhead for context extraction

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Runtime
```

## Usage

### Basic Tenant/Worker ID Extraction

```csharp
using Microsoft.Agents.A365.Runtime;

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

This package is separate from framework-specific integrations to:

- **Avoid Unnecessary Dependencies**: Core runtime utilities don't need framework-specific dependencies
- **Enable Flexible Deployment**: Console apps, background services don't need web dependencies
- **Follow Single Responsibility**: Each package has a focused purpose
- **Reduce Package Size**: Consumers only get what they need

## Related Documentation

- [Runtime Module Overview](../README.md)
- [Microsoft Agents 365 Observability](../../Observability/README.md)
- [Microsoft Agents 365 DevTools](../../DevTools/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
