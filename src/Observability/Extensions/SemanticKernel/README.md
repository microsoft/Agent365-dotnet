# Microsoft.Agents.A365.Observability.Extensions.SemanticKernel

Semantic Kernel integration extensions for Microsoft Agents 365 Observability. This package provides specialized observability features for Semantic Kernel-based agent applications.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.SemanticKernel
```

## Usage

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

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.

