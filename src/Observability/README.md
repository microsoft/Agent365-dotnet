# Microsoft Agents A365 Observability

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.svg?label=Core)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Runtime.svg?label=Runtime)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Runtime/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Hosting.svg?label=Hosting)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Hosting/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Extensions.OpenAI.svg?label=OpenAI)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Extensions.OpenAI/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.svg?label=SemanticKernel)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Extensions.SemanticKernel/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Extensions.AgentFramework.svg?label=AgentFramework)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Extensions.AgentFramework/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.Observability.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability/)

The Microsoft Agents A365 Observability module provides comprehensive monitoring, tracing, and diagnostics capabilities for AI agent applications. This module enables developers to gain deep insights into agent behavior, performance, and execution patterns through industry-standard observability tools.

## Overview

Building production-ready AI agents requires robust observability to understand agent behavior, diagnose issues, and optimize performance. This module provides:

- Distributed tracing for agent invocations and tool executions
- Integration with OpenTelemetry and Azure Monitor
- Specialized telemetry for AI agent operations
- Performance metrics and diagnostics
- Caching instrumentation and monitoring

## Features

- **Agent Monitoring**: Specialized tracing for AI agent invocations with detailed telemetry
- **Tool Execution Tracking**: Monitor tool executions and function calls with comprehensive metrics
- **OpenTelemetry Integration**: Built-in OpenTelemetry tracing for standardized observability
- **Azure Monitor Support**: Seamless integration with Azure Monitor for cloud-based monitoring
- **Caching Instrumentation**: Monitor and optimize agent caching strategies
- **Middleware Support**: ASP.NET Core middleware for request/response tracing

## Installation

```bash
# Core packages
dotnet add package Microsoft.Agents.A365.Observability
dotnet add package Microsoft.Agents.A365.Observability.Runtime
dotnet add package Microsoft.Agents.A365.Observability.Hosting

# Framework-specific extensions
dotnet add package Microsoft.Agents.A365.Observability.Extensions.OpenAI
dotnet add package Microsoft.Agents.A365.Observability.Extensions.SemanticKernel
dotnet add package Microsoft.Agents.A365.Observability.Extensions.AgentFramework
```

## Getting Started

This module provides observability capabilities through multiple packages. Choose the packages that match your needs:

1. **Start with Runtime** - For basic tracing setup, see [Runtime Package](Runtime/README.md)
2. **Add Framework Extensions** - For OpenAI, Semantic Kernel, or Agent Framework, see the respective extension READMEs
3. **Configure ETW (Optional)** - For Windows-based production monitoring, see [Hosting Package](Hosting/README.md)
4. **Use Core Features** - For token caching and scope management, see [Core Package](Core/README.md)

## Package Structure

The Observability module is organized into several packages, each with detailed documentation:

### Core Packages

- **[Microsoft.Agents.A365.Observability](Core/README.md)** - Core observability functionality including token caching and scope abstractions
- **[Microsoft.Agents.A365.Observability.Runtime](Runtime/README.md)** - Runtime services with full tracing setup, exporters, and OpenTelemetry integration
- **[Microsoft.Agents.A365.Observability.Hosting](Hosting/README.md)** - ETW (Event Tracing for Windows) support for production monitoring

### Framework Extensions

- **[Microsoft.Agents.A365.Observability.Extensions.OpenAI](Extensions/OpenAI/README.md)** - OpenAI SDK tracing and ChatToolCall extensions
- **[Microsoft.Agents.A365.Observability.Extensions.SemanticKernel](Extensions/SemanticKernel/README.md)** - Semantic Kernel integration with function invocation filtering
- **[Microsoft.Agents.A365.Observability.Extensions.AgentFramework](Extensions/AgentFramework/README.md)** - Microsoft Agent Framework integration with multi-source activity tracking

## Useful Links

### Microsoft Agents A365 SDK

- [Microsoft Agents A365 Notifications](../Notification/README.md) - Agent notification services
- [Microsoft Agents A365 Runtime](../Runtime/README.md) - Core runtime utilities for agents
- [Microsoft Agents A365 Tooling](../Tooling/README.md) - Developer tools and utilities
- [Microsoft Agents A365 DevTools](../DevTools/README.md) - Code analyzers and development tools

### Documentation

- [Microsoft Agents A365 Developer Documentation](<https://learn.microsoft.com/en-us/microsoft-agent-365/developer/>)

### Related Repositories

- [Agent365-python](<https://github.com/microsoft/Agent365-python>) - Python SDK for Microsoft Agents A365
- [Agent365-nodejs](<https://github.com/microsoft/Agent365-nodejs>) - Node.js SDK for Microsoft Agents A365
- [Agent365-Samples](<https://github.com/microsoft/Agent365-Samples>) - Sample applications and code examples

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
