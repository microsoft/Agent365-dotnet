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