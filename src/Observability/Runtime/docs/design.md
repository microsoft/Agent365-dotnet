# Microsoft.Agents.A365.Observability.Runtime - Design Documentation

## Overview

The `Microsoft.Agents.A365.Observability.Runtime` package provides OpenTelemetry-based distributed tracing infrastructure for Microsoft Agent 365 applications. It includes a fluent builder for configuration, scope classes for tracing operations, and custom exporters for Agent365 telemetry.

## Architecture

```
Microsoft.Agents.A365.Observability.Runtime
├── Public API
│   ├── Builder                  # Fluent OpenTelemetry configuration
│   └── Tracing/
│       ├── Scopes/
│       │   ├── InvokeAgentScope     # Agent invocation tracing
│       │   ├── InferenceScope       # LLM inference tracing
│       │   ├── ExecuteToolScope     # Tool execution tracing
│       │   └── OpenTelemetryScope   # Base scope class
│       └── Contracts/
│           ├── AgentDetails         # Agent metadata
│           ├── TenantDetails        # Tenant context
│           ├── CallerDetails        # Caller information
│           └── InferenceCallDetails # Inference metadata
├── Common/
│   ├── ActivityExtensions       # Activity helper methods
│   ├── BaggageBuilder          # Baggage context management
│   └── EnvironmentUtils        # Environment detection
├── DTOs/
│   ├── BaseData                # Base telemetry data
│   ├── ExecuteInferenceData    # Inference telemetry
│   ├── ExecuteToolData         # Tool telemetry
│   └── InvokeAgentData         # Agent invocation telemetry
├── Etw/
│   ├── A365EtwLogger           # ETW logging implementation
│   ├── EtwEventSource          # ETW event source
│   └── EtwLoggingBuilder       # ETW configuration
└── Tracing/
    ├── Processors/
    │   └── ActivityProcessor    # Base span processor
    └── Exporters/
        └── Agent365Exporter     # Custom telemetry exporter
```

## Key Components

### Builder

**Source**: [Builder.cs](../Builder.cs)

The main entry point for configuring OpenTelemetry tracing with Agent365 extensions.

```csharp
// Basic configuration
new Builder(services, configuration)
    .Build();

// With framework extensions (added via extension packages)
new Builder(services, configuration)
    .WithAgentFramework()      // From Extensions.AgentFramework
    .WithSemanticKernel()      // From Extensions.SemanticKernel
    .WithOpenAI()              // From Extensions.OpenAI
    .Build();

// Without OpenTelemetry builder (standalone tracer provider)
new Builder(services, configuration, useOpenTelemetryBuilder: false)
    .Build();
```

**Configuration Options:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `services` | `IServiceCollection` | Required | DI service collection |
| `configuration` | `IConfiguration` | Required | App configuration |
| `useOpenTelemetryBuilder` | `bool` | `true` | Use OpenTelemetry DI integration |
| `agent365ExporterType` | `Agent365ExporterType` | `Agent365Exporter` | Exporter type |

**Environment Variables:**

| Variable | Description |
|----------|-------------|
| `EnableAgent365Exporter` | Set to `true` to enable Agent365 exporter |

### InvokeAgentScope

**Source**: [InvokeAgentScope.cs](../Tracing/Scopes/InvokeAgentScope.cs)

Tracing scope for agent invocation operations. Creates an OpenTelemetry span with standardized attributes.

```csharp
using var scope = InvokeAgentScope.Start(
    invokeAgentDetails: new InvokeAgentDetails(
        endpoint: new Uri("https://agent.example.com"),
        details: new AgentDetails { AgentName = "MyAgent", AgentId = "agent-123" },
        sessionId: "session-456"
    ),
    tenantDetails: new TenantDetails { TenantId = "tenant-789" },
    request: new Request { Content = "User message", ExecutionType = ExecutionType.User },
    callerDetails: new CallerDetails { CallerId = "user-abc", CallerUpn = "user@contoso.com" }
);

// Record response
scope.RecordResponse("Agent response text");

// Record input/output messages
scope.RecordInputMessages(new[] { "message1", "message2" });
scope.RecordOutputMessages(new[] { "response1", "response2" });
```

**Certification Requirements:**

The following parameters must be provided (not null) for certification:
- `invokeAgentDetails`
- `tenantDetails`
- `request`
- `callerDetails`

### InferenceScope

**Source**: [InferenceScope.cs](../Tracing/Scopes/InferenceScope.cs)

Tracing scope for LLM inference operations.

```csharp
using var scope = InferenceScope.Start(
    details: new InferenceCallDetails
    {
        OperationName = InferenceOperationType.Chat,
        Model = "gpt-4",
        ProviderName = "Azure OpenAI",
        InputTokens = 150,
        OutputTokens = 200,
        FinishReasons = new[] { "stop" },
        ResponseId = "chatcmpl-123"
    },
    agentDetails: new AgentDetails { AgentName = "MyAgent" },
    tenantDetails: new TenantDetails { TenantId = "tenant-789" }
);

// Record additional metrics
scope.RecordInputMessages(new[] { "User: Hello" });
scope.RecordOutputMessages(new[] { "Assistant: Hi there!" });
scope.RecordInputTokens(150);
scope.RecordOutputTokens(200);
```

### ExecuteToolScope

Tracing scope for tool/function execution operations.

```csharp
using var scope = ExecuteToolScope.Start(
    toolDetails: new ToolDetails { ToolName = "search_web", ToolId = "tool-123" },
    agentDetails: agentDetails,
    tenantDetails: tenantDetails
);

scope.RecordToolResult("Search results...");
```

### BaggageBuilder

**Source**: [BaggageBuilder.cs](../Common/BaggageBuilder.cs)

Manages OpenTelemetry baggage context for propagating request metadata across distributed traces.

```csharp
// Set request context for the current async context
using (BaggageBuilder.SetRequestContext(tenantId: "tenant-123", agentId: "agent-456"))
{
    // All spans created here will have tenant/agent baggage
    await ProcessRequestAsync();
}

// Add custom baggage
BaggageBuilder.AddBaggage("correlation_id", correlationId);
```

**Standard Baggage Keys:**

| Key | Description |
|-----|-------------|
| `tenant_id` | Tenant identifier |
| `gen_ai.agent.id` | Agent identifier |
| `correlation_id` | Request correlation ID |

### ActivityExtensions

**Source**: [ActivityExtensions.cs](../Common/ActivityExtensions.cs)

Extension methods for `System.Diagnostics.Activity`.

```csharp
// Get attribute or fallback to baggage
string? value = activity.GetAttributeOrBaggage("tenant_id");

// Set tag only if not already present (coalesce)
activity.CoalesceTag("gen_ai.agent.name", agentName, fallbackName);
```

### OpenTelemetryConstants

Defines standard attribute keys following OpenTelemetry semantic conventions and Agent365 extensions.

```csharp
public static class OpenTelemetryConstants
{
    public const string SourceName = "Microsoft.Agents.A365.Observability";

    // GenAI semantic conventions
    public const string GenAiOperationNameKey = "gen_ai.operation.name";
    public const string GenAiRequestModelKey = "gen_ai.request.model";
    public const string GenAiProviderNameKey = "gen_ai.system";
    public const string GenAiUsageInputTokensKey = "gen_ai.usage.input_tokens";
    public const string GenAiUsageOutputTokensKey = "gen_ai.usage.output_tokens";

    // Agent365 extensions
    public const string GenAiAgentNameKey = "gen_ai.agent.name";
    public const string GenAiAgentIdKey = "gen_ai.agent.id";
    public const string TenantIdKey = "tenant_id";
}
```

## Design Patterns

### Builder Pattern

The `Builder` class implements the fluent builder pattern for configuring OpenTelemetry:

```csharp
public sealed class Builder
{
    private readonly IServiceCollection _services;
    private bool _isBuilt = false;

    public Builder(IServiceCollection services, IConfiguration? configuration) { ... }

    // Extension methods add framework support
    // public Builder WithAgentFramework() { ... }  // From extension package

    public IServiceCollection Build()
    {
        EnsureBuilt();
        return _services;
    }

    private void EnsureBuilt()
    {
        if (_isBuilt) return;

        // Configure OpenTelemetry
        _services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder => Configure(tracerProviderBuilder));

        _isBuilt = true;
    }
}
```

### Disposable Pattern

Scope classes implement `IDisposable` to automatically end spans:

```csharp
public sealed class InvokeAgentScope : OpenTelemetryScope
{
    private readonly Activity? _activity;

    private InvokeAgentScope(...)
        : base(kind: ActivityKind.Client, ...)
    {
        // Activity is started in base constructor
    }

    // Dispose ends the activity/span
}

// Usage - span automatically ended when scope is disposed
using var scope = InvokeAgentScope.Start(...);
```

### Strategy Pattern

Exporter selection based on environment:

```csharp
private void Configure(TracerProviderBuilder builder)
{
    builder.AddSource(OpenTelemetryConstants.SourceName)
           .AddProcessor(new ActivityProcessor());

    if (IsAgent365ExporterEnabled())
        builder.AddAgent365Exporter(exporterType: _agent365ExporterType);
    else if (EnvironmentUtils.IsDevelopmentEnvironment())
        builder.AddConsoleExporter();
}
```

## Data Flow

```
┌──────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ Application Code │────►│ Scope Classes   │────►│ Activity/Span   │
│                  │     │                 │     │                 │
│ using var scope  │     │ InvokeAgentScope│     │ OpenTelemetry   │
│ = Scope.Start()  │     │ InferenceScope  │     │ Activity        │
└──────────────────┘     └─────────────────┘     └────────┬────────┘
                                                          │
                    ┌─────────────────────────────────────┤
                    │                                     │
                    ▼                                     ▼
          ┌─────────────────┐               ┌─────────────────────┐
          │ SpanProcessor   │               │ Baggage Context     │
          │                 │               │                     │
          │ Add tags        │               │ Propagate tenant_id │
          │ Transform spans │               │ agent_id, etc.      │
          └────────┬────────┘               └─────────────────────┘
                   │
                   ▼
          ┌─────────────────┐
          │ Exporter        │
          │                 │
          │ Agent365 / OTLP │
          │ / Console       │
          └─────────────────┘
```

## File Structure

```
src/Observability/Runtime/
├── Builder.cs                       # Main configuration builder
├── Common/
│   ├── ActivityExtensions.cs        # Activity helper methods
│   ├── BaggageBuilder.cs           # Baggage context management
│   ├── EnvironmentUtils.cs         # Environment detection
│   └── PowerPlatformApiDiscovery.cs # API endpoint discovery
├── DTOs/
│   ├── BaseData.cs                 # Base telemetry DTO
│   ├── ExecuteInferenceData.cs     # Inference telemetry
│   ├── ExecuteToolData.cs          # Tool execution telemetry
│   └── InvokeAgentData.cs          # Agent invocation telemetry
├── Etw/
│   ├── A365EtwLogger.cs            # ETW logger implementation
│   ├── IA365EtwLogger.cs           # ETW logger interface
│   ├── EtwEventSource.cs           # ETW event source
│   ├── EtwLoggingBuilder.cs        # ETW logging configuration
│   ├── EtwTracingBuilder.cs        # ETW tracing configuration
│   ├── EtwLogProcessor.cs          # ETW log processor
│   └── EtwScopeEventProcessor.cs   # ETW scope processor
├── Tracing/
│   ├── Contracts/
│   │   ├── AgentDetails.cs         # Agent metadata contract
│   │   ├── TenantDetails.cs        # Tenant context contract
│   │   ├── CallerDetails.cs        # Caller information
│   │   ├── InvokeAgentDetails.cs   # Agent invocation details
│   │   ├── InferenceCallDetails.cs # Inference call details
│   │   └── SourceMetadata.cs       # Source metadata
│   ├── Scopes/
│   │   ├── OpenTelemetryScope.cs   # Base scope class
│   │   ├── OpenTelemetryConstants.cs # Attribute key constants
│   │   ├── InvokeAgentScope.cs     # Agent invocation scope
│   │   ├── InferenceScope.cs       # Inference operation scope
│   │   └── ExecuteToolScope.cs     # Tool execution scope
│   ├── Processors/
│   │   └── ActivityProcessor.cs    # Base activity processor
│   └── Exporters/
│       └── ObservabilityTracerProviderBuilderExtensions.cs
├── Microsoft.Agents.A365.Observability.Runtime.csproj
└── docs/
    └── design.md                   # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `OpenTelemetry` | Core tracing functionality |
| `OpenTelemetry.Extensions.Hosting` | DI integration |
| `OpenTelemetry.Exporter.Console` | Development exporter |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP exporter |
| `OpenTelemetry.Instrumentation.Runtime` | Runtime metrics |
| `Azure.Core` | Azure SDK integration |

## Usage Examples

### Basic Configuration

```csharp
// In Program.cs or Startup.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerBuilder =>
    {
        new Builder(builder.Services, builder.Configuration)
            .Build();
    });
```

### Manual Span Creation

```csharp
public async Task ProcessAgentRequestAsync(AgentRequest request)
{
    using var scope = InvokeAgentScope.Start(
        invokeAgentDetails: new InvokeAgentDetails(
            endpoint: _agentEndpoint,
            details: new AgentDetails
            {
                AgentName = _agentName,
                AgentId = _agentId
            },
            sessionId: request.SessionId
        ),
        tenantDetails: new TenantDetails { TenantId = request.TenantId },
        request: new Request
        {
            Content = request.Message,
            ExecutionType = ExecutionType.User
        },
        callerDetails: new CallerDetails
        {
            CallerId = request.UserId,
            CallerUpn = request.UserPrincipalName
        }
    );

    try
    {
        var response = await _agent.ProcessAsync(request);
        scope.RecordResponse(response.Content);
    }
    catch (Exception ex)
    {
        scope.SetStatus(ActivityStatusCode.Error, ex.Message);
        throw;
    }
}
```

### Context Propagation

```csharp
public class ObservabilityMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var tenantId = context.User?.FindFirst("tenant_id")?.Value;
        var agentId = context.Request.Headers["X-Agent-Id"].FirstOrDefault();

        using (BaggageBuilder.SetRequestContext(tenantId, agentId))
        {
            await next(context);
        }
    }
}
```

## External Resources

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)
- [Microsoft Agent 365 Observability](https://learn.microsoft.com/microsoft-agent-365/developer/observability)
