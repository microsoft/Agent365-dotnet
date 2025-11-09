# Microsoft.Agents.A365.Observability - Core Package

The core observability package provides fundamental tracing, monitoring, and instrumentation capabilities for AI agent applications. This package includes the essential building blocks for implementing comprehensive observability in your agents.

## Overview

This package contains the core abstractions and implementations for:

- Agent execution tracing
- Tool and function call monitoring
- OpenTelemetry integration
- Activity and span management
- Performance metrics collection

## Features

- **Agent Monitoring**: Specialized tracing for AI agent invocations with detailed telemetry
- **Tool Execution Tracking**: Monitor tool executions and function calls with comprehensive metrics
- **OpenTelemetry Integration**: Built-in OpenTelemetry tracing for standardized observability
- **Azure Monitor Support**: Seamless integration with Azure Monitor for cloud-based monitoring
- **Caching Instrumentation**: Monitor caching operations and effectiveness
- **Scope Management**: Hierarchical tracing scopes for complex agent operations

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability
```

## Quick Start

### Basic Configuration

1. **Install the package**:

   ```bash
   dotnet add package Microsoft.Agents.A365.Observability
   ```

2. **Configure in your application**:

   ```csharp
   using Microsoft.Agents.A365.Observability;
   
   var builder = WebApplication.CreateBuilder(args);
   
   // Configure Microsoft Agents A365 with tracing
   builder.Services.AddTracing();
   
   var app = builder.Build();
   ```

3. **Add agent tracing**:

   ```csharp
   using Microsoft.Agents.A365.Observability.Tracing;
   
   using var agentScope = ExecuteAgentScope.Start(agentId);
   // Your agent logic here
   ```

### Advanced Usage

#### Custom Tracing Scopes

```csharp
using Microsoft.Agents.A365.Observability.Tracing;

// Create a custom scope for specific operations
using var scope = CustomScope.Start("CustomOperation");
scope.AddTag("operation", "data-processing");
scope.AddMetric("recordsProcessed", 1000);

try
{
    // Your operation logic
    scope.SetSuccess();
}
catch (Exception ex)
{
    scope.SetError(ex);
    throw;
}
```

#### Nested Scopes

```csharp
using var agentScope = ExecuteAgentScope.Start(agentId);

// Nested tool execution scope
using var toolScope = ExecuteToolScope.Start(toolName);
// Tool execution logic

// Another nested operation
using var dataScope = ExecuteDataScope.Start("database-query");
// Database operation
```

## Package Structure

### Tracing

- **Scopes**: Hierarchical tracing scope implementations
  - `ExecuteAgentScope`: Top-level agent execution tracing
  - `ExecuteToolScope`: Tool and function call tracing
  - `ExecuteDataScope`: Data operation tracing
  - `CustomScope`: User-defined operation tracing

### Middleware

- **ASP.NET Core Integration**: Request/response tracing middleware
- **Activity Enrichment**: Automatic context propagation

### Caching

See [Caching Documentation](Caching/README.md) for detailed caching instrumentation capabilities.

## Configuration Options

### Service Registration

```csharp
builder.Services.AddTracing(options =>
{
    options.EnableDetailedTracing = true;
    options.SampleRate = 1.0; // 100% sampling
    options.EnableMetrics = true;
    options.EnableLogs = true;
});
```

### Sampling Configuration

```csharp
// Configure sampling for high-volume scenarios
builder.Services.AddTracing(options =>
{
    options.SampleRate = 0.1; // 10% sampling
    options.AdaptiveSampling = true;
});
```

## Best Practices

1. **Use Appropriate Scopes**: Choose the right scope type for your operation (Agent, Tool, Data, Custom)
2. **Add Contextual Tags**: Include relevant metadata in scopes for better filtering and analysis
3. **Handle Errors Properly**: Always set error states on scopes when exceptions occur
4. **Mind Performance**: Be aware of the overhead in high-frequency operations
5. **Structured Logging**: Combine tracing with structured logging for comprehensive observability

## Integration with Other Packages

This package works seamlessly with:

- **Microsoft.Agents.A365.Observability.Runtime**: Runtime services and exporters
- **Microsoft.Agents.A365.Observability.Hosting**: ASP.NET Core hosting integration
- **Microsoft.Agents.A365.Observability.Extensions.***: Framework-specific extensions

## Performance Considerations

- Tracing adds minimal overhead (~1-2% in most scenarios)
- Use sampling in high-throughput applications
- Batch exports reduce network overhead
- Async operations don't block the main thread

## Related Documentation

- [Observability Module Overview](../README.md)
- [Caching Documentation](Caching/README.md)
- [Runtime Package](../Runtime/README.md)
- [Hosting Package](../Hosting/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.

