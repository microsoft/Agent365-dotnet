# Microsoft.Agents.A365.Observability.Extensions.OpenAI - Design Documentation

## Overview

The `Microsoft.Agents.A365.Observability.Extensions.OpenAI` package provides OpenTelemetry tracing integration for the Azure OpenAI SDK. It includes a span processor that intercepts OpenAI API telemetry and extension methods for working with tool calls.

## Architecture

```
Microsoft.Agents.A365.Observability.Extensions.OpenAI
├── Public API
│   ├── BuilderExtensions           # Extension method for Builder
│   └── ChatToolCallExtensions      # Tool call helper extensions
├── Internal
│   ├── OpenAISpanProcessor         # Span processing logic
│   └── OpenAITelemetryConstants    # Telemetry constants
```

## Key Components

### BuilderExtensions

**Source**: [BuilderExtensions.cs](../BuilderExtensions.cs)

Provides the `WithOpenAI()` extension method for the observability `Builder`.

```csharp
// Configure observability with OpenAI support
new Builder(services, configuration)
    .WithOpenAI()
    .Build();
```

### OpenAISpanProcessor

**Source**: [OpenAISpanProcessor.cs](../OpenAISpanProcessor.cs)

A `BaseProcessor<Activity>` that processes spans from the Azure OpenAI SDK activity source.

**Processed Operations:**
- Chat completions
- Embeddings
- Other OpenAI API calls

```csharp
internal class OpenAISpanProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        if (IsOpenAISource(activity.Source.Name))
        {
            // Process OpenAI-specific span attributes
            ProcessOpenAISpan(activity);
        }
    }

    private void ProcessOpenAISpan(Activity activity)
    {
        // Normalize tag names to Agent365 schema
        // Extract token usage
        // Process response data
    }
}
```

### ChatToolCallExtensions

**Source**: [ChatToolCallExtensions.cs](../ChatToolCallExtensions.cs)

Extension methods for working with OpenAI `ChatToolCall` objects in telemetry contexts.

```csharp
// Extract telemetry data from tool calls
var toolCall = response.ToolCalls.First();
var telemetryData = toolCall.ToTelemetryData();

// Record tool call in current span
Activity.Current?.RecordToolCall(toolCall);
```

**Extension Methods:**

| Method | Description |
|--------|-------------|
| `ToTelemetryData()` | Converts tool call to telemetry-friendly format |
| `RecordToolCall()` | Records tool call details in Activity |
| `GetToolName()` | Extracts tool name from call |
| `GetToolArguments()` | Extracts tool arguments as dictionary |

### OpenAITelemetryConstants

**Source**: [OpenAITelemetryConstants.cs](../OpenAITelemetryConstants.cs)

Constants for OpenAI telemetry integration.

```csharp
internal static class OpenAITelemetryConstants
{
    public const string OpenAISource = "Azure.AI.OpenAI";
    public const string ChatCompletionsOperation = "chat.completions";
    public const string EmbeddingsOperation = "embeddings";

    // OpenAI-specific tag keys
    public const string ModelKey = "gen_ai.request.model";
    public const string PromptTokensKey = "gen_ai.usage.prompt_tokens";
    public const string CompletionTokensKey = "gen_ai.usage.completion_tokens";
}
```

## Design Patterns

### Extension Pattern

The package extends the core `Builder`:

```csharp
public static class BuilderExtensions
{
    public static Builder WithOpenAI(this Builder builder)
    {
        builder.AddSource(OpenAITelemetryConstants.OpenAISource);
        builder.AddProcessor(new OpenAISpanProcessor());
        return builder;
    }
}
```

### Span Processor Pattern

Processes spans from Azure OpenAI SDK:

```csharp
internal class OpenAISpanProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        if (activity.Source.Name.StartsWith(OpenAISource))
        {
            // Enrich with standard attributes
            NormalizeTokenUsage(activity);
            ProcessModelInformation(activity);
        }
    }

    private void NormalizeTokenUsage(Activity activity)
    {
        // Map OpenAI token counts to standard keys
        var promptTokens = activity.GetTagItem("prompt_tokens");
        if (promptTokens != null)
        {
            activity.SetTag(GenAiUsageInputTokensKey, promptTokens);
        }
    }
}
```

## Data Flow

```
┌─────────────────────────────┐
│ Azure OpenAI SDK            │
│                             │
│ ChatClient.CompleteAsync()  │
│ EmbeddingClient.Generate()  │
└──────────────┬──────────────┘
               │
               ▼ Activity Source Events
┌─────────────────────────────┐
│ Azure.AI.OpenAI             │
│                             │
│ Creates Activity/Span       │
│ with OpenAI-specific tags   │
└──────────────┬──────────────┘
               │
               ▼ OnEnd callback
┌─────────────────────────────┐
│ OpenAISpanProcessor         │
│                             │
│ 1. Check if OpenAI source   │
│ 2. Normalize tag names      │
│ 3. Extract token usage      │
│ 4. Process response data    │
└──────────────┬──────────────┘
               │
               ▼ Enriched Activity
┌─────────────────────────────┐
│ OpenTelemetry Exporter      │
│                             │
│ Spans follow Agent365       │
│ schema standards            │
└─────────────────────────────┘
```

## Span Attribute Mapping

| OpenAI SDK Tag | Agent365 Tag |
|----------------|--------------|
| `prompt_tokens` | `gen_ai.usage.input_tokens` |
| `completion_tokens` | `gen_ai.usage.output_tokens` |
| `model` | `gen_ai.request.model` |
| `finish_reason` | `gen_ai.response.finish_reasons` |

## File Structure

```
src/Observability/Extensions/OpenAI/
├── BuilderExtensions.cs              # Builder extension
├── OpenAISpanProcessor.cs            # Span processor
├── OpenAITelemetryConstants.cs       # Constants
├── ChatToolCallExtensions.cs         # Tool call extensions
├── Microsoft.Agents.A365.Observability.Extensions.OpenAI.csproj
└── docs/
    └── design.md                     # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.A365.Observability.Runtime` | Core observability |
| `Azure.AI.OpenAI` | Azure OpenAI SDK |

## Usage Examples

### Basic Setup

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerBuilder =>
    {
        new Builder(builder.Services, builder.Configuration)
            .WithOpenAI()
            .Build();
    });
```

### With Azure OpenAI Client

```csharp
var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey)
);

var chatClient = client.GetChatClient(deploymentName);

// Spans are automatically captured and processed
var response = await chatClient.CompleteChatAsync(messages);
```

### Tool Call Telemetry

```csharp
var response = await chatClient.CompleteChatAsync(messages, options);

foreach (var toolCall in response.Value.ToolCalls)
{
    // Record tool call in telemetry
    Activity.Current?.RecordToolCall(toolCall);

    // Execute tool
    var result = await ExecuteToolAsync(toolCall);

    // Add result to messages for next completion
    messages.Add(new ToolChatMessage(toolCall.Id, result));
}
```

### Combined with Other Extensions

```csharp
// Use multiple framework extensions together
new Builder(services, configuration)
    .WithAgentFramework()
    .WithSemanticKernel()
    .WithOpenAI()
    .Build();
```

## External Resources

- [Azure OpenAI SDK for .NET](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI)
- [OpenTelemetry GenAI Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
