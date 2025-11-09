# Microsoft.Agents.A365.Observability.Runtime

The Observability Runtime package provides runtime components for Microsoft Agents A365 Observability, including exporters, tracing utilities, DTOs, and scope management. This package bridges the core observability functionality with production telemetry systems.

## Overview

This package provides the runtime infrastructure for:

- Exporting telemetry data to Agent365 observability platform
- Managing tracing scopes with manual duration control
- Data transfer objects (DTOs) for telemetry
- Batch processing and queuing of telemetry data
- Integration with OpenTelemetry exporters

## Features

- **Agent365 Exporter**: Send telemetry to Agent365 observability platform
- **Batch Processing**: Efficient batching of telemetry data
- **Manual Scope Duration**: Support for custom start/end times in scopes
- **Flexible Configuration**: Comprehensive options for export behavior
- **Token-Based Authentication**: Secure authentication via token resolver
- **Multiple Cluster Support**: Support for different deployment environments

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Runtime
```

## Agent365 Exporter

The Agent365 exporter allows you to send telemetry data to the Agent365 observability platform using OpenTelemetry's `BatchActivityExportProcessor`.

### Basic Configuration

```csharp
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;

builder.Services.AddSingleton(new Agent365ExporterOptions
{
    ClusterCategory = "preprod",
    TokenResolver = (agentId, tenantId) => GetAuthToken(agentId, tenantId)
});

builder.Services.AddTracing();
```

### Advanced Configuration with Batching

```csharp
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;

builder.Services.AddSingleton(new Agent365ExporterOptions
{
    ClusterCategory = "preprod",
    TokenResolver = (agentId, tenantId) => GetAuthToken(agentId, tenantId),
    
    // Batching parameters (optional, defaults shown)
    MaxQueueSize = 2048,                      // Maximum number of activities to queue
    ScheduledDelayMilliseconds = 5000,        // Delay between batch exports (5 seconds)
    ExporterTimeoutMilliseconds = 30000,      // Timeout for export operations (30 seconds)
    MaxExportBatchSize = 512                  // Maximum activities per batch
});

builder.Services.AddTracing();
```

### Batching Parameters Reference

- **MaxQueueSize** (default: 2048)
  - Maximum number of activities to queue before dropping
  - Increase for high-throughput scenarios to avoid data loss
  - Higher values use more memory

- **ScheduledDelayMilliseconds** (default: 5000)
  - Time to wait before exporting a batch
  - Lower values provide more real-time data but increase network overhead
  - Higher values reduce network calls but increase latency

- **ExporterTimeoutMilliseconds** (default: 30000)
  - Maximum time to wait for export to complete
  - Increase for slow network conditions or large batch sizes
  - Lower values fail faster but may cause unnecessary retries

- **MaxExportBatchSize** (default: 512)
  - Maximum number of activities to include in a single export batch
  - Higher values reduce network overhead but increase memory usage
  - Lower values provide more frequent exports but increase network overhead

### Cluster Categories

The exporter supports multiple deployment environments:

- `prod`: Production environment
- `preprod`: Pre-production/staging environment
- `dev`: Development environment
- `test`: Testing environment

```csharp
builder.Services.AddSingleton(new Agent365ExporterOptions
{
    ClusterCategory = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "dev",
    TokenResolver = (agentId, tenantId) => GetAuthToken(agentId, tenantId)
});
```

## Manual Scope Duration Setting

The observability scopes support manual setting of start and end times, allowing you to create scopes for operations that occurred outside the normal execution flow. This is useful for:

- Recording historical operations
- Replaying telemetry from logs
- Creating scopes for async operations that completed at different times
- Testing and simulation scenarios

### Setting Start Time

```csharp
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

// Create a scope and set a custom start time
var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
using var scope = ExecuteAgentScope.Start(
    agentId: "my-agent",
    tenantId: Guid.Parse("your-tenant-id"),
    request: null
);
scope.SetStartTime(customStartTime);

// The scope will calculate duration from the custom start time to disposal
```

### Setting Both Start and End Times

```csharp
// Create a scope with both custom start and end times
var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-10);
var customEndTime = DateTimeOffset.UtcNow.AddMinutes(-5);

using (var scope = ExecuteAgentScope.Start(
    agentId: "my-agent",
    tenantId: Guid.Parse("your-tenant-id"),
    request: null
))
{
    scope.SetStartTime(customStartTime);
    scope.SetEndTime(customEndTime);
    
    // The scope will record a duration of 5 minutes
}
```

### Setting Only End Time

```csharp
// Use actual start time, custom end time
using (var scope = ExecuteAgentScope.Start(
    agentId: "my-agent",
    tenantId: Guid.Parse("your-tenant-id"),
    request: null
))
{
    // Do some work...
    
    // Set a custom end time (uses actual start time from scope creation)
    scope.SetEndTime(DateTimeOffset.UtcNow.AddSeconds(10));
}
```

### All Scope Types Support Manual Duration

All scope types in the SDK support manual start/end times:

```csharp
var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
var customEndTime = DateTimeOffset.UtcNow.AddMinutes(-3);

// ExecuteAgentScope
var agentScope = ExecuteAgentScope.Start(agentDetails, tenantDetails, request);
agentScope.SetStartTime(customStartTime);
agentScope.SetEndTime(customEndTime);

// InvokeAgentScope
var invokeScope = InvokeAgentScope.Start(invokeAgentDetails, tenantDetails, request);
invokeScope.SetStartTime(customStartTime);
invokeScope.SetEndTime(customEndTime);

// ExecuteToolScope
var toolScope = ExecuteToolScope.Start(toolCallDetails, agentDetails, tenantDetails);
toolScope.SetStartTime(customStartTime);
toolScope.SetEndTime(customEndTime);

// InferenceScope
var inferenceScope = InferenceScope.Start(inferenceCallDetails, agentDetails, tenantDetails);
inferenceScope.SetStartTime(customStartTime);
inferenceScope.SetEndTime(customEndTime);
```

## Package Structure

### Tracing

- **Exporters**: Agent365 exporter and export options
- **Scopes**: Scope implementations with duration control
  - `ExecuteAgentScope`: Agent execution tracing
  - `InvokeAgentScope`: Agent invocation tracing
  - `ExecuteToolScope`: Tool execution tracing
  - `InferenceScope`: AI inference tracing

### DTOs (Data Transfer Objects)

- **Common**: Shared DTO definitions
- **Telemetry Models**: Structured telemetry data models
- **Request/Response Models**: API interaction models

### Builder

- **Service Configuration**: Builder pattern for service setup
- **Pipeline Configuration**: Telemetry pipeline builders

## Advanced Usage

### Custom Token Resolution

```csharp
builder.Services.AddSingleton(new Agent365ExporterOptions
{
    ClusterCategory = "prod",
    TokenResolver = async (agentId, tenantId) =>
    {
        // Custom token acquisition logic
        var token = await AcquireTokenAsync(agentId, tenantId);
        return token;
    }
});
```

### Multiple Exporters

```csharp
// Configure multiple exporters for redundancy
builder.Services.AddTracing(options =>
{
    options.Exporters.Add(new Agent365ExporterOptions
    {
        ClusterCategory = "prod",
        TokenResolver = ProdTokenResolver
    });
    
    options.Exporters.Add(new Agent365ExporterOptions
    {
        ClusterCategory = "backup",
        TokenResolver = BackupTokenResolver
    });
});
```

### Error Handling

```csharp
builder.Services.AddSingleton(new Agent365ExporterOptions
{
    ClusterCategory = "prod",
    TokenResolver = (agentId, tenantId) => GetAuthToken(agentId, tenantId),
    OnExportError = (exception, activities) =>
    {
        // Custom error handling
        _logger.LogError(exception, "Failed to export {Count} activities", activities.Count);
    }
});
```

## Performance Tuning

### High-Throughput Scenarios

```csharp
// Optimized for high throughput
builder.Services.AddSingleton(new Agent365ExporterOptions
{
    MaxQueueSize = 8192,              // Larger queue
    MaxExportBatchSize = 2048,        // Larger batches
    ScheduledDelayMilliseconds = 1000 // More frequent exports
});
```

### Low-Latency Scenarios

```csharp
// Optimized for low latency
builder.Services.AddSingleton(new Agent365ExporterOptions
{
    MaxQueueSize = 512,               // Smaller queue
    MaxExportBatchSize = 128,         // Smaller batches
    ScheduledDelayMilliseconds = 500  // Very frequent exports
});
```

### Resource-Constrained Scenarios

```csharp
// Optimized for low resource usage
builder.Services.AddSingleton(new Agent365ExporterOptions
{
    MaxQueueSize = 256,                // Minimal queue
    MaxExportBatchSize = 64,           // Small batches
    ScheduledDelayMilliseconds = 10000 // Infrequent exports
});
```

## Best Practices

1. **Choose Appropriate Cluster**: Use the correct cluster category for your environment
2. **Secure Token Resolution**: Never hardcode tokens; use secure token acquisition
3. **Tune Batch Parameters**: Adjust based on your throughput and latency requirements
4. **Monitor Queue Health**: Watch for queue overflow in high-traffic scenarios
5. **Handle Export Errors**: Implement proper error handling and retry logic
6. **Test Manual Durations**: Validate custom start/end times in test environments

## Integration with Other Packages

- **Microsoft.Agents.A365.Observability**: Core tracing functionality
- **Microsoft.Agents.A365.Observability.Hosting**: ASP.NET Core integration
- **OpenTelemetry**: Standard telemetry APIs and exporters

## Related Documentation

- [Observability Module Overview](../README.md)
- [Core Package](../Core/README.md)
- [Hosting Package](../Hosting/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.

