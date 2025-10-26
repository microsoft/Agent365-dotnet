# Observability Runtime

This package provides the runtime components for the Agent365 Observability SDK, including exporters and tracing utilities.

## Agent365 Exporter

The Agent365 exporter allows you to send telemetry data to the Agent365 observability platform using OpenTelemetry's `BatchActivityExportProcessor`.

### Configuration

Configure the exporter by registering `Agent365ExporterOptions` in your service collection:

```csharp
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;

builder.Services.AddSingleton(new Agent365ExporterOptions
{
    ClusterCategory = "preprod",
    TokenResolver = (agentId, tenantId) => GetAuthToken(agentId, tenantId)
});

builder.Services.AddTracing();
```

### Batching Parameters

The Agent365 exporter uses OpenTelemetry's `BatchActivityExportProcessor` to batch telemetry data before exporting. You can customize the batching behavior by configuring the following parameters:

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

#### Batching Parameters Reference

- **MaxQueueSize** (default: 2048)
  - Maximum number of activities to queue before dropping.
  - Increase for high-throughput scenarios to avoid data loss.
  - Higher values use more memory.

- **ScheduledDelayMilliseconds** (default: 5000)
  - Time to wait before exporting a batch.
  - Lower values provide more real-time data but increase network overhead.
  - Higher values reduce network calls but increase latency.

- **ExporterTimeoutMilliseconds** (default: 30000)
  - Maximum time to wait for export to complete.
  - Increase for slow network conditions or large batch sizes.
  - Lower values fail faster but may cause unnecessary retries.

- **MaxExportBatchSize** (default: 512)
  - Maximum number of activities to include in a single export batch.
  - Higher values reduce network overhead but increase memory usage and export duration.
  - Lower values provide more frequent exports but increase network overhead.

## Manual Scope Duration Setting

The observability scopes support manual setting of start and end times, allowing you to create scopes for operations that occurred outside the normal execution flow. This is useful for:

- Recording historical operations
- Replaying telemetry from logs
- Creating scopes for async operations that completed at different times

You can set custom times via methods by calling `SetStartTime()` and/or `SetEndTime()` on the scope instance.

### Usage Examples

#### Setting Start Time via Method

```csharp
using System;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

// Create a scope and set a custom start time via method
var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
using var scope = ExecuteAgentScope.Start(
    agentId: "my-agent",
    tenantId: Guid.Parse("your-tenant-id"),
    request: null
);
scope.SetStartTime(customStartTime);

// The scope will calculate duration from the custom start time to now
```

#### Setting Both Start and End Times

```csharp
// Create a scope and set both custom start and end times
var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-10);
var customEndTime = DateTimeOffset.UtcNow.AddMinutes(-5);

using (var scope = ExecuteAgentScope.Start(
    agentId: "my-agent",
    tenantId: Guid.Parse("your-tenant-id"),
    request: null
))
{
    // Set the custom start and end times via methods
    scope.SetStartTime(customStartTime);
    scope.SetEndTime(customEndTime);
    
    // The scope will record a duration of 5 minutes (customEndTime - customStartTime)
}
```

#### Setting Only End Time

```csharp
// Create a scope and set only a custom end time
using (var scope = ExecuteAgentScope.Start(
    agentId: "my-agent",
    tenantId: Guid.Parse("your-tenant-id"),
    request: null
))
{
    // Do some work...
    
    // Set a custom end time (uses the actual start time from scope creation)
    scope.SetEndTime(DateTimeOffset.UtcNow.AddSeconds(10));
}
```

#### All Scope Types Support Manual Duration

All scope types support manual start/end times via methods:

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
inferenceScope.SetStartTime(customStartTime);
inferenceScope.SetEndTime(customEndTime);
```