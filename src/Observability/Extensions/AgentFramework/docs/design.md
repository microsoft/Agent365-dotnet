# Microsoft.Agents.A365.Observability.Extensions.AgentFramework - Design Documentation

## Overview

The `Microsoft.Agents.A365.Observability.Extensions.AgentFramework` package provides OpenTelemetry tracing integration for the Microsoft Agent Framework. It includes a span processor that intercepts and enriches telemetry from Agent Framework activity sources.

## Architecture

```
Microsoft.Agents.A365.Observability.Extensions.AgentFramework
├── Public API
│   └── BuilderExtensions            # Extension method for Builder
├── Internal
│   ├── AgentFrameworkSpanProcessor  # Span processing logic
│   └── Utils/
│       └── AgentFrameworkSpanProcessorHelper  # Helper methods
└── Models/
    └── AgentFrameworkMessageContent  # Message content wrapper
```

## Key Components

### BuilderExtensions

**Source**: [BuilderExtensions.cs](../BuilderExtensions.cs)

Provides the `WithAgentFramework()` extension method for the observability `Builder`.

```csharp
// Configure observability with Agent Framework support
new Builder(services, configuration)
    .WithAgentFramework()
    .Build();

// With additional activity sources
new Builder(services, configuration)
    .WithAgentFramework("Custom.Source.Name", "Another.Source")
    .Build();
```

**Tracked Activity Sources:**
- `Experimental.Microsoft.Agents.AI`
- `Experimental.Microsoft.Agents.AI.Agent`
- `Experimental.Microsoft.Agents.AI.ChatClient`
- Additional custom sources (optional)

### AgentFrameworkSpanProcessor

**Source**: [AgentFrameworkSpanProcessor.cs](../AgentFrameworkSpanProcessor.cs)

A `BaseProcessor<Activity>` that processes spans from Agent Framework activity sources.

**Processed Operations:**

| Operation | Processing |
|-----------|------------|
| `invoke_agent` | Processes input/output messages |
| `chat` | Processes input/output messages |
| `execute_tool` | Extracts tool call result to event content |

```csharp
internal class AgentFrameworkSpanProcessor : BaseProcessor<Activity>
{
    private const string InvokeAgentOperation = "invoke_agent";
    private const string ChatOperation = "chat";
    private const string ExecuteToolOperation = "execute_tool";

    public override void OnEnd(Activity activity)
    {
        if (IsTrackedSource(activity.Source.Name))
        {
            var operationName = activity.GetTagItem(GenAiOperationNameKey);
            switch (operationName)
            {
                case InvokeAgentOperation:
                case ChatOperation:
                    AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);
                    break;

                case ExecuteToolOperation:
                    var result = activity.GetTagItem(ToolCallResultTag);
                    activity.SetTag(EventContentTag, result);
                    break;
            }
        }
    }
}
```

### AgentFrameworkSpanProcessorHelper

**Source**: [AgentFrameworkSpanProcessorHelper.cs](../Utils/AgentFrameworkSpanProcessorHelper.cs)

Helper class for processing span attributes and message content.

```csharp
internal static class AgentFrameworkSpanProcessorHelper
{
    public static void ProcessInputOutputMessages(Activity activity)
    {
        // Extract and process gen_ai.content.prompt events
        // Extract and process gen_ai.content.completion events
        // Set gen_ai.input_messages and gen_ai.output_messages tags
    }
}
```

### AgentFrameworkMessageContent

Model class for wrapping message content from Agent Framework spans.

```csharp
internal class AgentFrameworkMessageContent
{
    public string? Role { get; set; }
    public string? Content { get; set; }
}
```

## Design Patterns

### Extension Pattern

The package extends the core `Builder` through extension methods:

```csharp
public static class BuilderExtensions
{
    public const string AgentFrameworkSource = "Experimental.Microsoft.Agents.AI";

    public static Builder WithAgentFramework(
        this Builder builder,
        params string[] additionalSources)
    {
        // Add activity sources
        builder.AddSource(AgentFrameworkSource);
        builder.AddSource($"{AgentFrameworkSource}.Agent");
        builder.AddSource($"{AgentFrameworkSource}.ChatClient");

        foreach (var source in additionalSources)
            builder.AddSource(source);

        // Add span processor
        builder.AddProcessor(new AgentFrameworkSpanProcessor(additionalSources));

        return builder;
    }
}
```

### Span Processor Pattern

The processor inherits from OpenTelemetry's `BaseProcessor<Activity>`:

```csharp
internal class AgentFrameworkSpanProcessor : BaseProcessor<Activity>
{
    private readonly string[] _additionalSources;

    public AgentFrameworkSpanProcessor(params string[] additionalSources)
    {
        _additionalSources = additionalSources ?? [];
    }

    public override void OnStart(Activity activity)
    {
        // Pre-processing (not used for Agent Framework)
    }

    public override void OnEnd(Activity activity)
    {
        // Post-processing - enrich spans before export
    }

    private bool IsTrackedSource(string sourceName)
    {
        if (sourceName.StartsWith(AgentFrameworkSource))
            return true;

        return _additionalSources.Any(s =>
            !string.IsNullOrWhiteSpace(s) && sourceName.StartsWith(s));
    }
}
```

## Data Flow

```
┌─────────────────────────────┐
│ Agent Framework             │
│                             │
│ AIAgent.InvokeAsync()       │
│ ChatClient.CompleteAsync()  │
│ Tool execution              │
└──────────────┬──────────────┘
               │
               ▼ Activity Source Events
┌─────────────────────────────┐
│ Experimental.Microsoft.     │
│ Agents.AI.*                 │
│                             │
│ Creates Activity/Span       │
│ with gen_ai.* tags          │
└──────────────┬──────────────┘
               │
               ▼ OnEnd callback
┌─────────────────────────────┐
│ AgentFrameworkSpanProcessor │
│                             │
│ 1. Check if tracked source  │
│ 2. Get operation name       │
│ 3. Process based on type:   │
│    - invoke_agent: messages │
│    - chat: messages         │
│    - execute_tool: result   │
└──────────────┬──────────────┘
               │
               ▼ Enriched Activity
┌─────────────────────────────┐
│ OpenTelemetry Exporter      │
│                             │
│ Export to:                  │
│ - Agent365 Exporter         │
│ - Console                   │
│ - OTLP                      │
└─────────────────────────────┘
```

## Span Attribute Mapping

### invoke_agent / chat Operations

| Source Tag | Target Tag |
|------------|------------|
| `gen_ai.content.prompt` events | `gen_ai.input_messages` |
| `gen_ai.content.completion` events | `gen_ai.output_messages` |

### execute_tool Operation

| Source Tag | Target Tag |
|------------|------------|
| `gen_ai.tool.call.result` | `gen_ai.event.content` |

## File Structure

```
src/Observability/Extensions/AgentFramework/
├── BuilderExtensions.cs                 # Builder extension method
├── AgentFrameworkSpanProcessor.cs       # Span processor
├── Utils/
│   └── AgentFrameworkSpanProcessorHelper.cs  # Helper methods
├── Models/
│   └── AgentFrameworkMessageContent.cs  # Message model
├── Microsoft.Agents.A365.Observability.Extensions.AgentFramework.csproj
└── docs/
    └── design.md                        # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.A365.Observability.Runtime` | Core observability |
| `OpenTelemetry.Instrumentation.AspNetCore` | ASP.NET Core instrumentation |
| `OpenTelemetry.Instrumentation.Http` | HTTP client instrumentation |

## Usage Examples

### Basic Setup

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerBuilder =>
    {
        new Builder(builder.Services, builder.Configuration)
            .WithAgentFramework()
            .Build();
    });
```

### With Custom Sources

```csharp
// Add custom activity sources to track
new Builder(services, configuration)
    .WithAgentFramework(
        "MyApp.CustomAgent",
        "MyApp.CustomTools"
    )
    .Build();
```

### Agent Framework Usage

```csharp
public class MyAgentService
{
    private readonly AIAgent _agent;

    public async Task<string> ProcessAsync(string message)
    {
        // Agent Framework creates spans automatically
        // AgentFrameworkSpanProcessor enriches them
        var response = await _agent.InvokeAsync(message);
        return response.Content;
    }
}
```

## External Resources

- [Microsoft Agent Framework](https://github.com/microsoft/agents)
- [OpenTelemetry Span Processors](https://opentelemetry.io/docs/specs/otel/trace/sdk/#span-processor)
- [OpenTelemetry GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/)
