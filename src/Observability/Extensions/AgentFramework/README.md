# Microsoft.Agents.A365.Observability.Extensions.AgentFramework

Microsoft Agent Framework integration extensions for Microsoft Agents A365 Observability. This package enables integration between the Agent Framework and the Observability module, processing OpenTelemetry traces from the Agent Framework to align with the Observability schema.

## Overview

This extension package provides seamless integration with the Microsoft Agent Framework, automatically translating Agent Framework telemetry into the Microsoft Agents A365 Observability schema for unified monitoring and analysis.

## Features

- **Agent Framework Integration**: Automatic instrumentation of Agent Framework operations
- **Schema Translation**: Converts Agent Framework telemetry to Observability schema
- **Turn Context Tracking**: Monitor conversation turns and context management
- **Activity Processing**: Track bot activities and message flows
- **Middleware Integration**: Seamless integration with Agent Framework middleware

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.AgentFramework
```

## Quick Start

```csharp
using Microsoft.Agents.A365.Observability.Extensions.AgentFramework;

var builder = WebApplication.CreateBuilder(args);

// Add observability with Agent Framework extensions
builder.Services.AddObservability(options =>
{
    options.EnableAgentFrameworkTracing = true;
});

var app = builder.Build();
```

## Configuration

```csharp
builder.Services.AddAgentFrameworkObservability(options =>
{
    options.TrackTurnContext = true;
    options.TrackActivities = true;
    options.EnableSchemaTranslation = true;
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
