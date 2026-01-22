# Microsoft.Agents.A365.Observability.Extensions.SemanticKernel - Design Documentation

## Overview

The `Microsoft.Agents.A365.Observability.Extensions.SemanticKernel` package provides OpenTelemetry tracing integration for Microsoft Semantic Kernel. It includes a span processor that intercepts and transforms Semantic Kernel telemetry to align with Agent365 observability standards, plus a function invocation filter for enhanced tool tracing.

## Architecture

```
Microsoft.Agents.A365.Observability.Extensions.SemanticKernel
├── Public API
│   ├── BuilderExtensions                # Extension method for Builder
│   ├── ChatCompletionAgentExtensions    # Agent helper extensions
│   └── FunctionInvocationFilter         # SK function filter
├── Internal
│   ├── SemanticKernelSpanProcessor      # Span processing logic
│   ├── SemanticKernelTelemetryConstants # Telemetry constants
│   └── Utils/
│       └── SemanticKernelSpanProcessorHelper  # Helper methods
└── Models/
    ├── MessageContent                   # Chat message model
    ├── AiChoice                         # AI response choice
    └── NestedContent                    # Nested content model
```

## Key Components

### BuilderExtensions

**Source**: [BuilderExtensions.cs](../BuilderExtensions.cs)

Provides the `WithSemanticKernel()` extension method for the observability `Builder`.

```csharp
// Configure observability with Semantic Kernel support
new Builder(services, configuration)
    .WithSemanticKernel()
    .Build();

// Without related sources (manual configuration)
new Builder(services, configuration)
    .WithSemanticKernel(enableRelatedSources: false)
    .Build();
```

**Implementation:**

```csharp
public static Builder WithSemanticKernel(this Builder builder, bool enableRelatedSources = true)
{
    builder.Services.AddSingleton<IFunctionInvocationFilter, FunctionInvocationFilter>();

    if (enableRelatedSources)
    {
        AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

        var telmConfig = builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(SemanticKernelTelemetryConstants.SemanticKernelSourceWildcard)
                .AddProcessor(new SemanticKernelSpanProcessor(builder.Configuration)));

        if (builder.Configuration != null
            && !string.IsNullOrEmpty(builder.Configuration["EnableOtlpExporter"])
            && bool.TryParse(builder.Configuration["EnableOtlpExporter"], out bool enabled) && enabled)
        {
            telmConfig.UseOtlpExporter();
        }
    }

    return builder;
}
```

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `enableRelatedSources` | `bool` | `true` | Enable OpenTelemetry for Semantic Kernel and add related sources |

**Configuration:**

| Key | Description |
|-----|-------------|
| `EnableOtlpExporter` | Set to `true` to enable OTLP exporter |

### SemanticKernelSpanProcessor

**Source**: [SemanticKernelSpanProcessor.cs](../SemanticKernelSpanProcessor.cs)

A `BaseProcessor<Activity>` that processes spans from the Semantic Kernel activity source (`Microsoft.SemanticKernel`).

**Processed Operations:**

| Operation | Processing |
|-----------|------------|
| `invoke_agent` | Processes input/output tags, optionally suppresses input |
| `execute_tool` | No modification (SK follows Agent365 schema) |
| `chat.completions` | Transforms to `chat`, extracts user/choice messages |

```csharp
internal class SemanticKernelSpanProcessor : BaseProcessor<Activity>
{
    private readonly bool _suppressInvokeAgentInput;

    public SemanticKernelSpanProcessor(IConfiguration? configuration = null)
    {
        _suppressInvokeAgentInput = configuration != null
            && bool.TryParse(configuration[SuppressInvokeAgentInputConfigKey], out var suppress)
            && suppress;
    }

    public override void OnEnd(Activity activity)
    {
        if (activity.Source.Name.StartsWith(SemanticKernelSource))
        {
            var operationName = activity.GetTagItem(GenAiOperationNameKey);
            switch (operationName)
            {
                case "invoke_agent":
                    ProcessInvocationInputOutputTag(activity, _suppressInvokeAgentInput);
                    break;

                case "chat.completions":
                    // Transform to standard "chat" operation
                    activity.SetTag(GenAiOperationNameKey, "Chat");
                    activity.DisplayName = activity.DisplayName.Replace("chat.completions", "Chat");

                    // Extract user and choice messages from events
                    var messages = GetGenAiUserAndChoiceMessageContent(activity);
                    // Set gen_ai.input_messages and gen_ai.output_messages
                    break;
            }
        }
    }
}
```

### FunctionInvocationFilter

**Source**: [FunctionInvocationFilter.cs](../FunctionInvocationFilter.cs)

An `IFunctionInvocationFilter` that adds telemetry tags to function/tool invocation spans.

```csharp
public class FunctionInvocationFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        // Add telemetry tags before invocation
        Activity.Current?.SetTag("gen_ai.tool.name", context.Function.Name);
        Activity.Current?.SetTag("gen_ai.tool.plugin", context.Function.PluginName);

        await next(context);

        // Add result tags after invocation
        Activity.Current?.SetTag("gen_ai.tool.result", context.Result?.ToString());
    }
}

// Register with Semantic Kernel
kernel.FunctionInvocationFilters.Add(new FunctionInvocationFilter());
```

### ChatCompletionAgentExtensions

**Source**: [ChatCompletionAgentExtensions.cs](../ChatCompletionAgentExtensions.cs)

Helper extensions for working with Semantic Kernel's `ChatCompletionAgent`.

```csharp
// Create agent with observability
var agent = kernel.CreateChatCompletionAgent("MyAgent")
    .WithObservability();
```

### SemanticKernelTelemetryConstants

**Source**: [SemanticKernelTelemetryConstants.cs](../SemanticKernelTelemetryConstants.cs)

Constants for Semantic Kernel telemetry integration.

```csharp
internal static class SemanticKernelTelemetryConstants
{
    // Operation Names
    public const string InvokeAgentOperation = "invoke_agent";
    public const string ExecuteToolOperation = "execute_tool";
    public const string ChatCompletionsOperation = "chat.completions";

    // Activity Source Names
    public const string SemanticKernelSource = "Microsoft.SemanticKernel";
    public const string SemanticKernelSourceWildcard = "Microsoft.SemanticKernel*";
    public const string AzureAISourceWildcard = "Azure.AI.*";

    // Configuration Keys
    public const string SuppressInvokeAgentInputConfigKey = "SuppressInvokeAgentInput";
}
```

### SemanticKernelSpanProcessorHelper

**Source**: [SemanticKernelSpanProcessorHelper.cs](../Utils/SemanticKernelSpanProcessorHelper.cs)

Helper methods for processing Semantic Kernel spans.

```csharp
internal static class SemanticKernelSpanProcessorHelper
{
    public static void ProcessInvocationInputOutputTag(
        Activity activity,
        bool suppressInvocationInput)
    {
        // Process input/output message tags
        // Optionally suppress input for privacy
    }

    public static Dictionary<string, object> GetGenAiUserAndChoiceMessageContent(
        Activity activity)
    {
        // Extract gen_ai.user and gen_ai.choice events
        // Return as dictionary with user messages and choice messages
    }
}
```

## Design Patterns

### Extension Pattern

The package extends the core `Builder` and registers the function invocation filter:

```csharp
public static class BuilderExtensions
{
    public static Builder WithSemanticKernel(this Builder builder, bool enableRelatedSources = true)
    {
        builder.Services.AddSingleton<IFunctionInvocationFilter, FunctionInvocationFilter>();

        if (enableRelatedSources)
        {
            AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

            var telmConfig = builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource(SemanticKernelTelemetryConstants.SemanticKernelSourceWildcard)
                    .AddProcessor(new SemanticKernelSpanProcessor(builder.Configuration)));

            if (builder.Configuration != null
                && !string.IsNullOrEmpty(builder.Configuration["EnableOtlpExporter"])
                && bool.TryParse(builder.Configuration["EnableOtlpExporter"], out bool enabled) && enabled)
            {
                telmConfig.UseOtlpExporter();
            }
        }

        return builder;
    }
}
```

### Filter Pattern

Uses Semantic Kernel's filter pipeline:

```csharp
public class FunctionInvocationFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        // Before invocation
        EnrichSpanWithFunctionInfo(context);

        await next(context);

        // After invocation
        EnrichSpanWithResult(context);
    }
}
```

### Span Transformation Pattern

Transforms Semantic Kernel spans to Agent365 schema:

```csharp
// SK emits: gen_ai.operation.name = "chat.completions"
// Transform to: gen_ai.operation.name = "Chat"
activity.SetTag(GenAiOperationNameKey, InferenceOperationType.Chat.ToString());
activity.DisplayName = activity.DisplayName.Replace("chat.completions", "Chat");
```

## Data Flow

```
┌─────────────────────────────┐
│ Semantic Kernel             │
│                             │
│ ChatCompletionAgent         │
│ Kernel.InvokeAsync()        │
│ Plugin functions            │
└──────────────┬──────────────┘
               │
               ▼ Activity Source Events
┌─────────────────────────────┐
│ Microsoft.SemanticKernel    │
│                             │
│ Creates Activity/Span       │
│ with SK-specific tags       │
│ - chat.completions          │
│ - invoke_agent              │
│ - execute_tool              │
└──────────────┬──────────────┘
               │
               ▼ OnEnd callback
┌─────────────────────────────┐
│ SemanticKernelSpanProcessor │
│                             │
│ 1. Check if SK source       │
│ 2. Get operation name       │
│ 3. Transform:               │
│    - chat.completions→Chat  │
│    - Extract user/choice    │
│    - Process input/output   │
└──────────────┬──────────────┘
               │
               ▼ Transformed Activity
┌─────────────────────────────┐
│ OpenTelemetry Exporter      │
│                             │
│ Spans follow Agent365       │
│ schema standards            │
└─────────────────────────────┘
```

## Span Attribute Mapping

### chat.completions Operation

| Source | Target |
|--------|--------|
| `gen_ai.operation.name = "chat.completions"` | `gen_ai.operation.name = "Chat"` |
| `gen_ai.user` events | `gen_ai.input_messages` |
| `gen_ai.choice` events | `gen_ai.output_messages` |

### invoke_agent Operation

| Source | Target |
|--------|--------|
| Input message content | `gen_ai.input_messages` (unless suppressed) |
| Output message content | `gen_ai.output_messages` |

### execute_tool Operation

No transformation needed - Semantic Kernel follows Agent365 schema.

## Configuration

| Configuration Key | Description | Default |
|-------------------|-------------|---------|
| `SuppressInvokeAgentInput` | Suppress input messages in invoke_agent spans | `false` |

```json
{
  "SuppressInvokeAgentInput": "true"
}
```

## File Structure

```
src/Observability/Extensions/SemanticKernel/
├── BuilderExtensions.cs                    # Builder extension
├── SemanticKernelSpanProcessor.cs          # Span processor
├── SemanticKernelTelemetryConstants.cs     # Constants
├── FunctionInvocationFilter.cs             # SK function filter
├── ChatCompletionAgentExtensions.cs        # Agent extensions
├── Utils/
│   └── SemanticKernelSpanProcessorHelper.cs  # Helper methods
├── Models/
│   ├── MessageContent.cs                   # Message model
│   ├── AiChoice.cs                         # Choice model
│   └── NestedContent.cs                    # Nested content
├── Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.csproj
└── docs/
    └── design.md                           # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.A365.Observability.Runtime` | Core observability |
| `Microsoft.SemanticKernel` | Semantic Kernel SDK |
| `Microsoft.SemanticKernel.Agents.Core` | SK Agents |

## Usage Examples

### Basic Setup

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerBuilder =>
    {
        new Builder(builder.Services, builder.Configuration)
            .WithSemanticKernel()
            .Build();
    });
```

### With Function Filter

```csharp
// Add filter to kernel
var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey)
    .Build();

kernel.FunctionInvocationFilters.Add(new FunctionInvocationFilter());

// Use kernel - spans will include function telemetry
var result = await kernel.InvokeAsync(myFunction, arguments);
```

### Suppress Input Messages

```csharp
// appsettings.json
{
  "SuppressInvokeAgentInput": "true"
}

// Or via environment variable
Environment.SetEnvironmentVariable("SuppressInvokeAgentInput", "true");
```

### Chat Completion Agent

```csharp
var agent = new ChatCompletionAgent
{
    Name = "MyAssistant",
    Instructions = "You are a helpful assistant.",
    Kernel = kernel
};

// Spans will be processed and transformed automatically
var history = new ChatHistory();
history.AddUserMessage("Hello!");

await foreach (var message in agent.InvokeAsync(history))
{
    Console.WriteLine(message.Content);
}
```

## External Resources

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Semantic Kernel Telemetry](https://learn.microsoft.com/semantic-kernel/concepts/enterprise-readiness/observability)
- [OpenTelemetry GenAI Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/)
