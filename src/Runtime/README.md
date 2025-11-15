# Microsoft Agent 365 Runtime SDK for .NET

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Runtime.svg?label=Core)](https://www.nuget.org/packages/Microsoft.Agents.A365.Runtime/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Runtime.Extensions.OpenAI.svg?label=OpenAI)](https://www.nuget.org/packages/Microsoft.Agents.A365.Runtime.Extensions.OpenAI/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel.svg?label=Semantic%20Kernel)](https://www.nuget.org/packages/Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.Runtime.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Runtime/)

The Microsoft Agent 365 Runtime SDK provides essential runtime utilities and services for building multi-tenant agent applications. This SDK includes ASP.NET Core integration helpers, authorization services, and extensions for popular AI frameworks.

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

The Runtime SDK provides multi-tenant utilities and framework extensions:

### Core Package

- **[Microsoft.Agents.A365.Runtime](Core/README.md)** - Core runtime utilities including TenantContextHelper, AgenticAuthorizationService, and Utility methods

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information
 
## Trademarks
 
*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
