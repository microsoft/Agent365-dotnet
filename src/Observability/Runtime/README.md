# Microsoft.Agents.A365.Observability.Runtime

The Observability Runtime package provides runtime components for Microsoft Agents 365 Observability, including exporters, tracing utilities, DTOs, and scope management.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Runtime
```

## Usage

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

### Configuration with Batching

```csharp
builder.Services.AddSingleton(new Agent365ExporterOptions
{
    ClusterCategory = "preprod",
    TokenResolver = (agentId, tenantId) => GetAuthToken(agentId, tenantId),
    MaxQueueSize = 2048,
    ScheduledDelayMilliseconds = 5000,
    MaxExportBatchSize = 512
});
```

### Manual Scope Duration

```csharp
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

// Create a scope with custom start and end times
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
}
```

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.

