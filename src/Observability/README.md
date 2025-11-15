# Microsoft Agent 365 Observability SDK

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.svg?label=Core)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Runtime.svg?label=Runtime)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Runtime/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Hosting.svg?label=Hosting)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Hosting/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Extensions.OpenAI.svg?label=OpenAI)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Extensions.OpenAI/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.svg?label=SemanticKernel)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Extensions.SemanticKernel/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.Extensions.AgentFramework.svg?label=AgentFramework)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability.Extensions.AgentFramework/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.Observability.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Observability/)

The Microsoft Agent 365 Observability SDK provides comprehensive monitoring, tracing, and diagnostics capabilities for AI agent applications. This module enables developers to gain deep insights into agent behavior, performance, and execution patterns through industry-standard observability tools.

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

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

## Trademarks

*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.