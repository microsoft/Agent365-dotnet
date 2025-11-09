# Microsoft.Agents.A365.Observability.Extensions.OpenAI

OpenAI-specific tracing and instrumentation extensions for Microsoft Agents A365 Observability. This package provides specialized observability features for OpenAI-based agent applications.

## Overview

This extension package enables comprehensive monitoring and tracing of OpenAI API calls, token usage, model invocations, and other OpenAI-specific operations within your agent applications.

## Features

- **OpenAI API Call Tracing**: Automatic instrumentation of OpenAI API requests and responses
- **Token Usage Tracking**: Monitor token consumption across requests
- **Model Performance Metrics**: Track model invocation latency and success rates
- **Error Diagnostics**: Detailed error tracking for OpenAI-specific failures
- **Cost Monitoring**: Track API usage for cost optimization

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.OpenAI
```

## Quick Start

```csharp
using Microsoft.Agents.A365.Observability.Extensions.OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add observability with OpenAI extensions
builder.Services.AddObservability(options =>
{
    options.EnableOpenAITracing = true;
});

var app = builder.Build();
```

## Configuration

```csharp
builder.Services.AddOpenAIObservability(options =>
{
    options.TrackTokenUsage = true;
    options.TrackModelPerformance = true;
    options.EnableDetailedErrorLogging = true;
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
