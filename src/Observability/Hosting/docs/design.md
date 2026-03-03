# Microsoft.Agents.A365.Observability.Hosting - Design Documentation

## Overview

The `Microsoft.Agents.A365.Observability.Hosting` package provides ASP.NET Core hosting integration for the Agent365 observability infrastructure. It includes middleware for baggage propagation, token caching for exporters, and extension methods for working with the Microsoft 365 Agents SDK `TurnContext`.

## Architecture

```
Microsoft.Agents.A365.Observability.Hosting
├── Middleware/
│   └── ObservabilityBaggageMiddleware   # Per-request baggage context
├── Caching/
│   ├── IExporterTokenCache              # Token cache interface
│   ├── AgenticTokenCache                # Agentic token caching
│   ├── ServiceTokenCache                # Service token caching
│   └── AgenticTokenStruct               # Token data structure
├── Extensions/
│   ├── BaggageBuilderExtensions         # Baggage context helpers
│   ├── InvokeAgentScopeExtensions       # Scope enrichment from TurnContext
│   ├── TurnContextExtensions            # TurnContext telemetry extraction
│   ├── ObservabilityBuilderExtensions   # Builder extensions
│   └── ObservabilityServiceCollectionExtensions  # DI setup
└── Internal/
    └── AttributeKeys                    # Internal attribute key constants
```

## Key Components

### ObservabilityBaggageMiddleware

**Source**: [ObservabilityBaggageMiddleware.cs](../Middleware/ObservabilityBaggageMiddleware.cs)

ASP.NET Core middleware that sets per-request observability context (baggage) for distributed tracing.

```csharp
// Configure middleware in Program.cs
app.UseObservabilityRequestContext(ctx =>
{
    var tenantId = ctx.User?.FindFirst("tenant_id")?.Value;
    var agentId = ctx.Request.Headers["X-Agent-Id"].FirstOrDefault();
    return (tenantId, agentId);
});
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `next` | `RequestDelegate` | Next middleware in pipeline |
| `resolver` | `Func<HttpContext, (string?, string?)>` | Function to resolve tenant and agent IDs |

**Behavior:**
1. Calls the resolver function with the current `HttpContext`
2. Sets baggage context using `BaggageBuilder.SetRequestContext()`
3. Context is automatically disposed after request completes

### InvokeAgentScopeExtensions

**Source**: [InvokeAgentScopeExtensions.cs](../Extensions/InvokeAgentScopeExtensions.cs)

Extension methods for enriching `InvokeAgentScope` with data from `ITurnContext`.

```csharp
using var scope = InvokeAgentScope.Start(invokeAgentDetails, tenantDetails);

// Enrich scope with all available TurnContext data
scope.FromTurnContext(turnContext);

// Or selectively enrich specific attributes
scope.SetCallerTags(turnContext);
scope.SetExecutionTypeTags(turnContext);
scope.SetTargetAgentTags(turnContext);
scope.SetTenantIdTags(turnContext);
scope.SetSourceMetadataTags(turnContext);
scope.SetConversationIdTags(turnContext);
scope.SetInputMessageTags(turnContext);
```

**Extension Methods:**

| Method | Purpose |
|--------|---------|
| `FromTurnContext()` | Sets all available tags from TurnContext |
| `SetCallerTags()` | Sets caller-related attributes |
| `SetExecutionTypeTags()` | Sets execution type based on caller/recipient status |
| `SetTargetAgentTags()` | Sets target agent attributes |
| `SetTenantIdTags()` | Sets tenant ID from ChannelData |
| `SetSourceMetadataTags()` | Sets source metadata attributes |
| `SetConversationIdTags()` | Sets conversation ID and item link |
| `SetInputMessageTags()` | Sets input message from Activity.Text |

### TurnContextExtensions

**Source**: [TurnContextExtensions.cs](../Extensions/TurnContextExtensions.cs)

Extension methods for extracting telemetry data from `ITurnContext`.

```csharp
// Get baggage key-value pairs
var callerPairs = turnContext.GetCallerBaggagePairs();
var executionTypePair = turnContext.GetExecutionTypePair();
var targetAgentPairs = turnContext.GetTargetAgentBaggagePairs();
var tenantIdPair = turnContext.GetTenantIdPair();
var conversationPairs = turnContext.GetConversationIdAndItemLinkPairs();
var sourceMetadataPairs = turnContext.GetSourceMetadataBaggagePairs();
```

**Extracted Attributes:**

| Category | Attributes |
|----------|------------|
| Caller | `gen_ai.caller.id`, `gen_ai.caller.upn`, `gen_ai.caller.name`, `gen_ai.caller.client_ip` |
| Execution | `gen_ai.execution.type` (User, Agent, AgentToAgent) |
| Target Agent | `gen_ai.agent.name`, `gen_ai.agent.id`, `gen_ai.agent.upn` |
| Tenant | `tenant_id` |
| Conversation | `conversation_id`, `item_link` |
| Source | Source metadata from activity |

### Token Caching

Token caches for managing authentication tokens used by telemetry exporters.

**IExporterTokenCache Interface:**

```csharp
public interface IExporterTokenCache
{
    Task<string?> GetTokenAsync(string resource, CancellationToken cancellationToken);
    Task SetTokenAsync(string resource, string token, DateTimeOffset expiry, CancellationToken cancellationToken);
}
```

**AgenticTokenCache:**

Caches tokens for agentic operations, using the user's delegated identity.

**ServiceTokenCache:**

Caches tokens for service-to-service operations, using the application identity.

### BaggageBuilderExtensions

**Source**: [BaggageBuilderExtensions.cs](../Extensions/BaggageBuilderExtensions.cs)

Extensions for the `BaggageBuilder` class from the Runtime package.

```csharp
// Set baggage from TurnContext
BaggageBuilder.SetFromTurnContext(turnContext);

// Set baggage from HttpContext
BaggageBuilder.SetFromHttpContext(httpContext);
```

### ObservabilityServiceCollectionExtensions

**Source**: [ObservabilityServiceCollectionExtensions.cs](../Extensions/ObservabilityServiceCollectionExtensions.cs)

Extension methods for configuring observability services in the DI container.

```csharp
// Add observability services
services.AddAgent365Observability(configuration);

// Add with custom options
services.AddAgent365Observability(configuration, options =>
{
    options.EnableAgent365Exporter = true;
    options.ServiceName = "MyAgent";
});
```

## Design Patterns

### Middleware Pattern

The baggage middleware follows ASP.NET Core middleware conventions:

```csharp
public sealed class ObservabilityBaggageMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Func<HttpContext, (string?, string?)> _resolver;

    public ObservabilityBaggageMiddleware(
        RequestDelegate next,
        Func<HttpContext, (string?, string?)>? resolver)
    {
        _next = next;
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var (tenant, agent) = _resolver(ctx);

        using (BaggageBuilder.SetRequestContext(tenant, agent))
        {
            await _next(ctx);
        }
    }
}
```

### Extension Methods Pattern

TurnContext extensions use a consistent pattern for extracting key-value pairs:

```csharp
public static class TurnContextExtensions
{
    public static IEnumerable<KeyValuePair<string, string?>> GetCallerBaggagePairs(
        this ITurnContext turnContext)
    {
        var activity = turnContext?.Activity;
        if (activity == null) yield break;

        yield return new KeyValuePair<string, string?>(
            OpenTelemetryConstants.GenAiCallerIdKey,
            activity.From?.Id);

        yield return new KeyValuePair<string, string?>(
            OpenTelemetryConstants.GenAiCallerUpnKey,
            activity.From?.Properties?.GetValueOrDefault("upn")?.ToString());

        // ... more attributes
    }
}
```

### Fluent Extension Pattern

InvokeAgentScope extensions support method chaining:

```csharp
public static InvokeAgentScope SetCallerTags(
    this InvokeAgentScope invokeAgentScope,
    ITurnContext turnContext)
{
    invokeAgentScope.RecordAttributes(turnContext.GetCallerBaggagePairs());
    return invokeAgentScope;  // Enable chaining
}
```

## Data Flow

```
┌─────────────────┐     ┌─────────────────────────┐     ┌─────────────────┐
│ HTTP Request    │────►│ ObservabilityBaggage    │────►│ BaggageBuilder  │
│                 │     │ Middleware              │     │                 │
│ Headers, Claims │     │                         │     │ Set tenant_id   │
│                 │     │ resolver(ctx) =>        │     │ Set agent_id    │
│                 │     │ (tenant, agent)         │     │                 │
└─────────────────┘     └─────────────────────────┘     └────────┬────────┘
                                                                 │
                                                                 ▼
┌─────────────────┐     ┌─────────────────────────┐     ┌─────────────────┐
│ TurnContext     │────►│ InvokeAgentScope        │────►│ OpenTelemetry   │
│                 │     │ Extensions              │     │ Span            │
│ Activity        │     │                         │     │                 │
│ From, Recipient │     │ .FromTurnContext()      │     │ With all        │
│ ChannelData     │     │ .SetCallerTags()        │     │ enriched tags   │
└─────────────────┘     └─────────────────────────┘     └─────────────────┘
```

## File Structure

```
src/Observability/Hosting/
├── Middleware/
│   └── ObservabilityBaggageMiddleware.cs  # Per-request baggage
├── Caching/
│   ├── IExporterTokenCache.cs             # Token cache interface
│   ├── AgenticTokenCache.cs               # Agentic token caching
│   ├── ServiceTokenCache.cs               # Service token caching
│   └── AgenticTokenStruct.cs              # Token data structure
├── Extensions/
│   ├── BaggageBuilderExtensions.cs        # Baggage extensions
│   ├── InvokeAgentScopeExtensions.cs      # Scope enrichment
│   ├── TurnContextExtensions.cs           # TurnContext data extraction
│   ├── ObservabilityBuilderExtensions.cs  # Builder extensions
│   └── ObservabilityServiceCollectionExtensions.cs  # DI setup
├── Microsoft.Agents.A365.Observability.Hosting.csproj
└── docs/
    └── design.md                          # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.A365.Observability.Runtime` | Core observability |
| `Microsoft.Agents.Builder` | TurnContext, Activity |
| `Microsoft.AspNetCore.Hosting` | ASP.NET Core hosting |
| `Microsoft.AspNetCore.Http.Abstractions` | HttpContext |

## Usage Examples

### Full Pipeline Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add observability services
builder.Services.AddAgent365Observability(builder.Configuration);

var app = builder.Build();

// Add baggage middleware early in pipeline
app.UseObservabilityRequestContext(ctx =>
{
    // Extract from JWT claims
    var tenantId = ctx.User?.FindFirst("tid")?.Value
                ?? ctx.User?.FindFirst("tenant_id")?.Value;

    // Extract from custom header
    var agentId = ctx.Request.Headers["X-Agent-Id"].FirstOrDefault();

    return (tenantId, agentId);
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Agent Message Handler

```csharp
public class AgentHandler : IActivityHandler
{
    public async Task OnMessageActivityAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var tenantDetails = new TenantDetails
        {
            TenantId = turnContext.Activity.ChannelData?
                .GetProperty("tenant")?.GetProperty("id")?.GetString()
        };

        using var scope = InvokeAgentScope.Start(
            invokeAgentDetails: CreateInvokeDetails(turnContext),
            tenantDetails: tenantDetails
        );

        // Enrich with all available context
        scope.FromTurnContext(turnContext);

        try
        {
            var response = await ProcessMessageAsync(turnContext.Activity.Text);
            scope.RecordResponse(response);

            await turnContext.SendActivityAsync(response, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            scope.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

### Custom Token Cache Implementation

```csharp
public class RedisTokenCache : IExporterTokenCache
{
    private readonly IDistributedCache _cache;

    public async Task<string?> GetTokenAsync(string resource, CancellationToken ct)
    {
        var key = $"exporter_token:{resource}";
        return await _cache.GetStringAsync(key, ct);
    }

    public async Task SetTokenAsync(
        string resource, string token, DateTimeOffset expiry, CancellationToken ct)
    {
        var key = $"exporter_token:{resource}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiry
        };
        await _cache.SetStringAsync(key, token, options, ct);
    }
}

// Register in DI
services.AddSingleton<IExporterTokenCache, RedisTokenCache>();
```

## External Resources

- [ASP.NET Core Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
- [OpenTelemetry Context Propagation](https://opentelemetry.io/docs/concepts/context-propagation/)
