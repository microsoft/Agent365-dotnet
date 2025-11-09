# Microsoft.Agents.A365.Observability.Extensions.OpenAI

OpenAI-specific tracing and instrumentation extensions for Microsoft Agents A365 Observability. This package provides specialized observability features for OpenAI-based agent applications.

## Overview

This extension package enables comprehensive monitoring and tracing of OpenAI API calls, token usage, model invocations, and other OpenAI-specific operations within your agent applications.

## Features

- **OpenAI API Call Tracing**: Automatic instrumentation of OpenAI API requests and responses
- **Token Usage Tracking**: Monitor token consumption across requests
- **Model Performance Metrics**: Track model invocation latency and success rates
- **Error Diagnostics**: Detailed error tracking for OpenAI-specific failures
- **ChatToolCall Extensions**: Manual tracing support for tool execution scenarios
- **OpenAI Span Processing**: Custom span processor for OpenAI-specific telemetry enrichment
- **Zero-Configuration Setup**: Automatic integration with OpenAI's experimental OpenTelemetry support

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.OpenAI
```

## Quick Start

### Basic Setup with Builder Pattern

```csharp
using Microsoft.Agents.A365.Observability.Extensions.OpenAI;
using Microsoft.Agents.A365.Observability.Runtime;

var builder = Builder.Create(services);

// Add OpenAI observability with related tracing sources
builder.WithOpenAI(enableRelatedSources: true);

var observability = builder.Build();
```

### Manual Tool Call Tracing

```csharp
using OpenAI.Chat;
using Microsoft.Agents.A365.Observability.Extensions.OpenAI;

// When executing a tool call, create a trace scope
var chatToolCall = // ... from ChatCompletion response

using var scope = chatToolCall.Trace(
    agentId: "my-agent", 
    tenantId: myTenantGuid
);

// Execute your tool logic
var result = await ExecuteToolAsync(chatToolCall);

// Scope automatically completes when disposed
```

## Configuration

### Enable OpenAI Tracing

The `WithOpenAI` extension method configures:

- **OpenAI SDK Tracing**: Enables `OpenAI.Experimental.EnableOpenTelemetry` AppContext switch
- **Activity Source**: Adds `Azure.AI.OpenAI.*` activity source to OpenTelemetry
- **Custom Processor**: Registers `OpenAISpanProcessor` for span enrichment



```csharp
// Enable with related sources (recommended)
builder.WithOpenAI(enableRelatedSources: true);

// Or disable related sources if you only want custom processing
builder.WithOpenAI(enableRelatedSources: false);
```

### Tool Call Tracing Details

The `Trace` extension method on `ChatToolCall` creates an `ExecuteToolScope` with:

- **Tool Details**: Function name, arguments, call ID, and kind
- **Agent Context**: Agent ID for correlation
- **Tenant Context**: Tenant ID for multi-tenant scenarios

## Architecture

### OpenAI Span Processor

The `OpenAISpanProcessor` enhances OpenAI-generated spans with additional context and normalizes telemetry data to align with the Microsoft Agents A365 Observability schema.

### Telemetry Constants

`OpenAITelemetryConstants.OpenAISourceWildcard` defines the activity source pattern (`Azure.AI.OpenAI.*`) used to capture all OpenAI SDK operations.

## Usage Scenarios

### Web API with OpenAI

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add observability services
var observabilityBuilder = Builder.Create(builder.Services);
observabilityBuilder
    .WithOpenAI()
    .WithTracing()
    .WithMetrics();

var app = builder.Build();
```

### Azure Functions with OpenAI

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        var builder = Builder.Create(services);
        builder.WithOpenAI();
    })
    .Build();
```

## Related Documentation

- [Observability Module Overview](../../README.md)
- [Core Package](../../Core/README.md)
- [Observability Runtime Package](../../Runtime/README.md)
- [Microsoft Agents A365 Developer Documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
