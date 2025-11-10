# Microsoft.Agents.A365.Observability.Extensions.OpenAI

OpenAI-specific tracing and instrumentation extensions for Microsoft Agents 365 Observability. This package provides specialized observability features for OpenAI-based agent applications.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Extensions.OpenAI
```

## Usage

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
```

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
