# Microsoft.Agents.A365.Observability.Extensions.SemanticKernel

Semantic Kernel integration extensions for Microsoft Agents A365 Observability. This package provides specialized observability features for Semantic Kernel-based agent applications.

## Overview

This extension package enables comprehensive monitoring and tracing of Semantic Kernel operations, including kernel invocations, plugin executions, and planner activities within your agent applications.

## Features

- **Kernel Invocation Tracing**: Automatic instrumentation of Semantic Kernel operations
- **Plugin Execution Tracking**: Monitor plugin calls and performance
- **Planner Activity Monitoring**: Track planner operations and decision-making
- **Memory Operations**: Trace semantic memory operations and queries
- **Function Call Analysis**: Detailed tracking of function invocations

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.SemanticKernel
```

## Quick Start

```csharp
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Add observability with Semantic Kernel extensions
builder.Services.AddObservability(options =>
{
    options.EnableSemanticKernelTracing = true;
});

var app = builder.Build();
```

## Configuration

```csharp
builder.Services.AddSemanticKernelObservability(options =>
{
    options.TrackPluginExecutions = true;
    options.TrackPlannerOperations = true;
    options.TrackMemoryOperations = true;
    options.EnableDetailedFunctionLogging = true;
});
```

## Related Documentation

- [Observability Module Overview](../../README.md)
- [Core Package](../../Core/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
