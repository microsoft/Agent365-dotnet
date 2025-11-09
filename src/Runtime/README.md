# Microsoft Agents A365 Runtime SDK for .NET

The Microsoft Agents A365 Runtime SDK provides essential runtime utilities and services for building multi-tenant agent applications. This SDK includes ASP.NET Core integration helpers, authorization services, and extensions for popular AI frameworks.

## Overview

The Runtime SDK simplifies building production-ready agent applications by providing:

- Multi-tenant context extraction from HTTP requests
- Authorization services for agentic operations
- Framework extensions for OpenAI and Semantic Kernel
- Tenant and worker isolation utilities
- Performance-optimized helper methods

## Features

- **Tenant Context Management**: Extract and manage tenant IDs across multi-tenant applications
- **Worker Context Support**: Handle worker-specific operations in distributed agent systems
- **Authorization Services**: Agentic authorization and permission management
- **Framework Extensions**: Ready-to-use extensions for OpenAI and Semantic Kernel
- **ASP.NET Core Integration**: Seamless integration with ASP.NET Core applications
- **Performance Optimized**: Minimal overhead for context extraction and management

## Installation

```bash
# Core runtime utilities
dotnet add package Microsoft.Agents.A365.Runtime

# For OpenAI integration
dotnet add package Microsoft.Agents.A365.Runtime.Extensions.OpenAI

# For Semantic Kernel integration
dotnet add package Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel
```

## Package Structure

### Core Package

- **Microsoft.Agents.A365.Runtime** (`Core/`): Core runtime utilities including:
  - `AgenticAuthorizationService`: Authorization services for agent operations
  - `TenantContextHelper`: Tenant and worker ID extraction from HttpContext
  - `Utility`: Common utility methods for runtime operations

### Extensions

- **Microsoft.Agents.A365.Runtime.Extensions.OpenAI** (`Extensions/OpenAI/`): Runtime extensions for OpenAI integration
- **Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel** (`Extensions/SemanticKernel/`): Runtime extensions for Semantic Kernel integration

## Quick Start

### Basic Tenant/Worker ID Extraction

```csharp
using Microsoft.Agents.A365.Runtime;

app.MapPost("/api/agent", async (HttpContext context) =>
{
    // Extract tenant and worker context
    var tenantId = TenantContextHelper.GetTenantId(context);
    var workerId = TenantContextHelper.GetWorkerId(context);
    
    // Use with KernelProvider or other multi-tenant services
    var kernel = kernelProvider.GetKernel(tenantId ?? "default", workerId ?? "default");
    
    // Process request with tenant isolation...
});
```

### Integration with Authorization

```csharp
using Microsoft.Agents.A365.Runtime;

app.MapPost("/api/process", async (
    HttpContext context, 
    IAgenticAuthorizationService authService) =>
{
    // Extract context
    var tenantId = TenantContextHelper.GetTenantId(context);
    var workerId = TenantContextHelper.GetWorkerId(context);
    
    // Apply authorization
    var isAuthorized = await authService.AuthorizeAsync(tenantId, workerId);
    
    if (!isAuthorized)
    {
        return Results.Unauthorized();
    }
    
    // Process authorized request...
});
```

## Context Sources

The helper checks for tenant/worker IDs in this priority order:

1. **User Claims**: `tenant_id`, `worker_id`
2. **Request Headers**: `X-Tenant-Id`, `X-Worker-Id`
3. **Request Items**: `TenantId`, `WorkerId`

## Advanced Usage

### Custom Tenant Resolution

```csharp
public class CustomTenantResolver
{
    public string? ResolveTenant(HttpContext context)
    {
        // Try standard extraction first
        var tenantId = TenantContextHelper.GetTenantId(context);
        
        if (tenantId != null)
            return tenantId;
            
        // Add custom resolution logic
        // e.g., from subdomain, custom header, database lookup, etc.
        
        return null;
    }
}
```

### Multi-Tenant Kernel Provider

```csharp
public class MultiTenantService
{
    private readonly IKernelProvider _kernelProvider;
    
    public async Task ProcessRequestAsync(HttpContext context)
    {
        var tenantId = TenantContextHelper.GetTenantId(context);
        var workerId = TenantContextHelper.GetWorkerId(context);
        
        // Get tenant-specific kernel with proper isolation
        var kernel = _kernelProvider.GetKernel(
            tenantId ?? "default", 
            workerId ?? "default");
            
        // Process with tenant-isolated resources
        await kernel.InvokeAsync("ProcessData");
    }
}
```

## Why Separate Packages?

The Runtime SDK is organized into separate packages to:

- **Avoid Unnecessary Dependencies**: Core runtime doesn't need web framework dependencies
- **Enable Flexible Deployment**: Console apps and background services don't need ASP.NET Core
- **Follow Single Responsibility**: Each package has a focused purpose
- **Reduce Package Size**: Consumers only get what they need
- **Improve Build Performance**: Smaller dependency graphs

## Related Packages

- [Microsoft.Agents.A365.Observability](../Observability/README.md) - Monitoring and tracing for agent applications
- [Microsoft.Agents.A365.Notifications](../Notification/README.md) - Agent notification services
- [Microsoft.Agents.A365.Tooling](../Tooling/README.md) - Developer tools and utilities
- [Microsoft.Agents.A365.DevTools.Analyzer](../DevTools/README.md) - Roslyn analyzers for governance enforcement

## Documentation

- [Core Runtime Documentation](Core/README.md)
- [OpenAI Extensions](Extensions/OpenAI/README.md)
- [Semantic Kernel Extensions](Extensions/SemanticKernel/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
