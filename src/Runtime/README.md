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

## 📋 **Telemetry**
 
Data Collection. The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
 
## Trademarks
 
*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
