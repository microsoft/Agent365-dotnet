# Microsoft.Agents.A365.Observability.Extensions.AgentFramework

Microsoft Agent Framework integration extensions for Microsoft Agents 365 Observability. This package enables integration between the Agent Framework and the Observability module, processing OpenTelemetry traces from the Agent Framework to align with the Observability schema.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.AgentFramework
```

## Usage

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

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.

