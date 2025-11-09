# Microsoft.Agents.A365.Observability.Extensions.AgentFramework

Microsoft Agent Framework integration extensions for Microsoft Agents A365 Observability. This package enables integration between the Agent Framework and the Observability module, processing OpenTelemetry traces from the Agent Framework to align with the Observability schema.

## Overview

This extension package provides seamless integration with the Microsoft Agent Framework, automatically translating Agent Framework telemetry into the Microsoft Agents A365 Observability schema for unified monitoring and analysis.

## Features

- **Agent Framework Integration**: Automatic instrumentation of Agent Framework operations
- **Schema Translation**: Converts Agent Framework telemetry to Observability schema
- **Turn Context Tracking**: Monitor conversation turns and context management
- **Activity Processing**: Track bot activities and message flows
- **Multi-Source Activity Tracking**: Traces agent, chat client, and general AI operations
- **Agent Framework Span Processing**: Custom span processor for Agent Framework-specific telemetry enrichment
- **Zero-Configuration Setup**: Simple builder pattern integration with automatic activity source registration
- **Middleware Integration**: Seamless integration with Agent Framework middleware

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.AgentFramework
```

## Quick Start

### Basic Setup with Builder Pattern

```csharp
using Microsoft.Agents.A365.Observability.Extensions.AgentFramework;
using Microsoft.Agents.A365.Observability.Runtime;

var builder = Builder.Create(services);

// Add Agent Framework observability with related tracing sources
builder.WithAgentFramework(enableRelatedSources: true);

var observability = builder.Build();
```

### Web API with Agent Framework

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add observability services
var observabilityBuilder = Builder.Create(builder.Services);
observabilityBuilder
    .WithAgentFramework()
    .WithTracing()
    .WithMetrics();

// Add Agent Framework services
builder.Services.AddChatClient(/* configure your chat client */);

var app = builder.Build();
```

## Configuration Options

### Enable Agent Framework Tracing

The `WithAgentFramework` extension method configures:

- **Agent Framework Activity Sources**: Registers three activity sources:
  - `Experimental.Microsoft.Agents.AI` - General AI operations
  - `Experimental.Microsoft.Agents.AI.Agent` - Agent-specific operations
  - `Experimental.Microsoft.Agents.AI.ChatClient` - Chat client operations
- **Custom Processor**: Registers `AgentFrameworkSpanProcessor` for span enrichment

```csharp
// Enable with related sources (recommended)
builder.WithAgentFramework(enableRelatedSources: true);

// Or disable related sources if you only want custom processing
builder.WithAgentFramework(enableRelatedSources: false);
```

### Activity Source Constants

The `BuilderExtensions` class exposes three public constants for the activity sources:

```csharp
public const string AgentFrameworkSource = "Experimental.Microsoft.Agents.AI";
public const string AgentFrameworkAgentSource = "Experimental.Microsoft.Agents.AI.Agent";
public const string AgentFrameworkChatClientSource = "Experimental.Microsoft.Agents.AI.ChatClient";
```

These can be used for custom telemetry configuration or filtering.

## Architecture

### Activity Sources

Three separate activity sources capture different aspects of Agent Framework operations:

1. **AgentFrameworkSource**: General AI framework activities
2. **AgentFrameworkAgentSource**: Agent lifecycle and invocation activities
3. **AgentFrameworkChatClientSource**: Chat client request/response activities

### Agent Framework Span Processor

The `AgentFrameworkSpanProcessor` enhances Agent Framework-generated spans with additional context and normalizes telemetry data to align with the Microsoft Agents A365 Observability schema.

## Usage Scenarios

### Azure Functions with Agent Framework

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        var builder = Builder.Create(services);
        builder.WithAgentFramework();
        
        services.AddChatClient(/* config */);
    })
    .Build();
```

### Console Application with Agent Framework

```csharp
var services = new ServiceCollection();

var observabilityBuilder = Builder.Create(services);
observabilityBuilder
    .WithAgentFramework()
    .WithTracing();

services.AddChatClient(/* configure chat client */);

var serviceProvider = services.BuildServiceProvider();

// All Agent Framework operations are now automatically traced
var chatClient = serviceProvider.GetRequiredService<IChatClient>();
var response = await chatClient.CompleteAsync("Hello!");
```

### Multi-Framework Integration

```csharp
var builder = Builder.Create(services);

// Combine multiple observability extensions
builder
    .WithAgentFramework()
    .WithSemanticKernel()
    .WithOpenAI()
    .WithTracing()
    .WithMetrics();

// Now you have unified tracing across all frameworks
```

## Telemetry Captured

When `enableRelatedSources: true`, the extension captures:

- **Agent Invocations**: Start/end of agent operations
- **Chat Completions**: Request/response cycles
- **Tool Executions**: Function calling and tool usage
- **Pipeline Operations**: Middleware and filter execution
- **Error Conditions**: Exceptions and failure scenarios

All telemetry is automatically correlated and aligned with the Microsoft Agents A365 Observability schema for consistent querying and visualization.

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

