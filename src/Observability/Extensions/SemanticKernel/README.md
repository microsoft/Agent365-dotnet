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
- **Agent Tracing Extensions**: Simple `WithTracing()` extension for ChatCompletionAgent
- **Semantic Kernel Span Processing**: Custom span processor for Semantic Kernel-specific telemetry enrichment
- **Zero-Configuration Setup**: Automatic integration with Semantic Kernel's experimental OpenTelemetry support

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.SemanticKernel
```

## Quick Start

### Basic Setup with Builder Pattern

```csharp
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;
using Microsoft.Agents.A365.Observability.Runtime;

var builder = Builder.Create(services);

// Add Semantic Kernel observability with related tracing sources
builder.WithSemanticKernel(enableRelatedSources: true);

var observability = builder.Build();
```

### Enable Tracing on ChatCompletionAgent

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

// Create your agent
var agent = new ChatCompletionAgent
{
    Name = "Assistant",
    Instructions = "You are a helpful assistant.",
    Kernel = kernel
};

// Enable automatic tracing
agent = agent.WithTracing();

// Now all function invocations will be traced
await foreach (var message in agent.InvokeAsync(history))
{
    Console.WriteLine(message.Content);
}
```

## Configuration Options

### Enable Semantic Kernel Tracing

The `WithSemanticKernel` extension method configures:

- **Semantic Kernel SDK Tracing**: Enables `Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive` AppContext switch
- **Activity Source**: Adds `Microsoft.SemanticKernel.*` activity source to OpenTelemetry
- **Function Invocation Filter**: Registers `IFunctionInvocationFilter` for automatic function call tracing
- **Custom Processor**: Registers `SemanticKernelSpanProcessor` for span enrichment

```csharp
// Enable with related sources (recommended)
builder.WithSemanticKernel(enableRelatedSources: true);

// Or disable related sources if you only want custom processing
builder.WithSemanticKernel(enableRelatedSources: false);
```

### Agent Tracing Extension

The `WithTracing()` extension method on `ChatCompletionAgent`:

- Checks if `FunctionInvocationFilter` is already registered in the kernel
- Adds the filter if not present
- Returns the same agent instance for continued use

This ensures all function calls made by the agent are automatically traced without manual instrumentation.

## Architecture

### Function Invocation Filter

The `FunctionInvocationFilter` implements `IFunctionInvocationFilter` to intercept and trace every kernel function invocation, capturing:

- Function name and parameters
- Execution timing
- Success/failure status
- Return values

### Semantic Kernel Span Processor

The `SemanticKernelSpanProcessor` enhances Semantic Kernel-generated spans with additional context and normalizes telemetry data to align with the Microsoft Agents A365 Observability schema.

### Telemetry Constants

`SemanticKernelTelemetryConstants.SemanticKernelSourceWildcard` defines the activity source pattern (`Microsoft.SemanticKernel.*`) used to capture all Semantic Kernel SDK operations.

## Usage Scenarios

### Web API with Semantic Kernel

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add observability services
var observabilityBuilder = Builder.Create(builder.Services);
observabilityBuilder
    .WithSemanticKernel()
    .WithTracing()
    .WithMetrics();

// Add Semantic Kernel services
builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(/* config */);

var app = builder.Build();
```

### Azure Functions with Semantic Kernel Agent

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        var builder = Builder.Create(services);
        builder.WithSemanticKernel();
        
        services.AddKernel()
            .AddAzureOpenAIChatCompletion(/* config */);
    })
    .Build();
```

### Console Application with Traced Agent

```csharp
var services = new ServiceCollection();

var observabilityBuilder = Builder.Create(services);
observabilityBuilder.WithSemanticKernel();

var serviceProvider = services.BuildServiceProvider();
var kernel = serviceProvider.GetRequiredService<Kernel>();

var agent = new ChatCompletionAgent
{
    Name = "Assistant",
    Kernel = kernel
}.WithTracing();

// All function calls are now automatically traced
await agent.InvokeAsync("Hello!");
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

