# Microsoft Agents A365 Runtime SDK for .NET

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Runtime.svg?label=Core)](https://www.nuget.org/packages/Microsoft.Agents.A365.Runtime/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Runtime.Extensions.OpenAI.svg?label=OpenAI)](https://www.nuget.org/packages/Microsoft.Agents.A365.Runtime.Extensions.OpenAI/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel.svg?label=Semantic%20Kernel)](https://www.nuget.org/packages/Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.Runtime.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Runtime/)

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

## Package Structure

The Runtime SDK provides multi-tenant utilities and framework extensions:

### Core Package

- **[Microsoft.Agents.A365.Runtime](Core/README.md)** - Core runtime utilities including TenantContextHelper, AgenticAuthorizationService, and Utility methods

### Framework Extensions

- **Microsoft.Agents.A365.Runtime.Extensions.OpenAI** - Runtime extensions for OpenAI integration (coming soon)
- **Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel** - Runtime extensions for Semantic Kernel integration (coming soon)

## Getting Started

The Runtime SDK helps extract tenant and worker context from HTTP requests for multi-tenant agent applications.

### Core Runtime Utilities

See [Core Package](Core/README.md) for detailed examples on:

- Extracting tenant and worker IDs from HttpContext
- Using TenantContextHelper for multi-tenant isolation
- Integrating with authorization services
- Working with kernel providers

## Key Capabilities

### Multi-Tenant Context Management

- Extract tenant IDs from user claims, headers, or request items
- Extract worker IDs for multi-worker scenarios
- Null-safe extraction with proper validation
- Performance-optimized with minimal overhead

### Authorization Services

- AgenticAuthorizationService for agent-specific authorization
- Integration with Microsoft Agents A365 security model
- Support for tenant-level and worker-level permissions

### Utility Methods

- Common helper methods for runtime operations
- HTTP context manipulation
- Request/response processing utilities

## Package Documentation

For detailed code examples and usage patterns, see the [Core Package README](Core/README.md).

## Related Packages

- [Microsoft.Agents.A365.Observability](../Observability/README.md) - Monitoring and tracing for agent applications
- [Microsoft.Agents.A365.Notifications](../Notification/README.md) - Agent notification services
- [Microsoft.Agents.A365.Tooling](../Tooling/README.md) - Developer tools and utilities
- [Microsoft.Agents.A365.DevTools](../DevTools/README.md) - Roslyn analyzers for governance enforcement

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
