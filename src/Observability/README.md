# Microsoft Agents A365 Observability

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
dotnet add package Microsoft.Agents.A365.Observability
dotnet add package Microsoft.Agents.A365.Observability.Runtime
dotnet add package Microsoft.Agents.A365.Observability.Hosting
```

For framework-specific extensions:

```bash
# For OpenAI integration
dotnet add package Microsoft.Agents.A365.Observability.Extensions.OpenAI

# For Semantic Kernel integration
dotnet add package Microsoft.Agents.A365.Observability.Extensions.SemanticKernel

# For Agent Framework integration
dotnet add package Microsoft.Agents.A365.Observability.Extensions.AgentFramework
```

## Quick Start

### Basic Configuration

1. **Configure in your application**:

   ```csharp
   using Microsoft.Agents.A365;
   
   var builder = WebApplication.CreateBuilder(args);
   
   // Configure Microsoft Agents A365 with Azure Monitor
   builder.Services.AddTracing();
   
   var app = builder.Build();
   ```

2. **Add agent tracing**:

   ```csharp
   using Microsoft.Agents.A365.Tracing;
   
   using var agentScope = ExecuteAgentScope.Start(AgentId);
   // Your agent logic here
   ```

### Advanced Configuration with Azure Monitor

```csharp
using Microsoft.Agents.A365.Observability;
using Microsoft.Agents.A365.Observability.Runtime;

var builder = WebApplication.CreateBuilder(args);

// Add observability with Azure Monitor
builder.Services.AddObservability(options =>
{
    options.EnableAzureMonitor = true;
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableCaching = true;
    options.EnableTracing = true;
});

var app = builder.Build();
```

## Package Structure

The Observability module is organized into several packages:

### Core Packages

- **Microsoft.Agents.A365.Observability** (`Core/`): Core observability functionality including tracing abstractions and base instrumentation
- **Microsoft.Agents.A365.Observability.Runtime** (`Runtime/`): Runtime services for observability including DTOs and tracing utilities
- **Microsoft.Agents.A365.Observability.Hosting** (`Hosting/`): ASP.NET Core hosting integration with ETW support

### Extensions

- **Microsoft.Agents.A365.Observability.Extensions.AgentFramework** (`Extensions/AgentFramework/`): Integration with Microsoft Agent Framework
- **Microsoft.Agents.A365.Observability.Extensions.OpenAI** (`Extensions/OpenAI/`): OpenAI-specific tracing and instrumentation
- **Microsoft.Agents.A365.Observability.Extensions.SemanticKernel** (`Extensions/SemanticKernel/`): Semantic Kernel integration for enhanced observability

## Key Features

### Distributed Tracing

Track agent invocations across distributed systems with full context propagation:

```csharp
using var agentScope = ExecuteAgentScope.Start("MyAgent");
// Agent operations are automatically traced
```

### Caching Instrumentation

Monitor cache performance and efficiency:

```csharp
// Cache operations are automatically instrumented
// View cache hit rates, latency, and effectiveness in your monitoring dashboard
```

### Middleware Integration

Add request/response tracing to your ASP.NET Core application:

```csharp
app.UseObservabilityMiddleware();
```

## Sample Applications

- **Basic Sample**: Simple ASP.NET Core web application with Microsoft Agents A365 integration
- **Custom Engine**: Advanced agent implementation with custom engines and comprehensive tracing
- **Hello World Agent**: Simple getting started example demonstrating core observability features
- **Devin Agent**: Advanced AI agent implementation with full observability
- **Semantic Kernel Multiturn**: Semantic Kernel sample with distributed tracing

## Integration Guides

- [Caching Documentation](Core/Caching/README.md)
- [OpenAI Integration](Extensions/OpenAI/README.md)
- [Semantic Kernel Integration](Extensions/SemanticKernel/README.md)
- [Agent Framework Integration](Extensions/AgentFramework/README.md)

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
