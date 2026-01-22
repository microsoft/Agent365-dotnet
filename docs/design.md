# Microsoft Agent 365 SDK for .NET - Design Documentation

This document describes the architecture and design of the Microsoft Agent 365 SDK for .NET, helping developers understand the project structure and begin reading and contributing code.

## Overview

The Microsoft Agent 365 SDK for .NET is an enterprise-grade extension framework built on top of the Microsoft 365 Agents SDK. It provides four main pillars of functionality:

- **Observability**: OpenTelemetry-based distributed tracing with custom exporters
- **Notifications**: Event-driven notification handling for Microsoft 365 applications
- **Runtime**: Core utilities for multi-tenant agent applications
- **Tooling**: MCP (Model Context Protocol) server integration for tool discovery and registration

**Target Frameworks**:
- Most packages: `net8.0`
- Observability Runtime and Hosting packages: `netstandard2.0` (for broader compatibility)

## Repository Structure

```
src/
├── Runtime/
│   └── Core/                                    # Core runtime utilities
├── Observability/
│   ├── Runtime/                                 # Observability runtime (OpenTelemetry)
│   ├── Hosting/                                 # ASP.NET Core hosting middleware
│   └── Extensions/
│       ├── AgentFramework/                      # Agent Framework tracing
│       ├── SemanticKernel/                      # Semantic Kernel tracing
│       └── OpenAI/                              # OpenAI SDK tracing
├── Notification/
│   └── Microsoft.Agents.A365.Notifications/     # Notification handling
├── Tooling/
│   ├── Core/                                    # MCP server configuration
│   └── Extensions/
│       ├── SemanticKernel/                      # SK tool registration
│       ├── AgentFramework/                      # AF tool registration
│       └── AzureAIFoundry/                      # Azure AI Foundry integration
└── Tests/                                       # Test projects
docs/
└── design.md                                    # This file
```

## Packages

| Package | Description | Design Document |
|---------|-------------|-----------------|
| `Microsoft.Agents.A365.Runtime` | Core runtime utilities including tenant context, operation results, and authorization | [design.md](../src/Runtime/Core/docs/design.md) |
| `Microsoft.Agents.A365.Observability.Runtime` | OpenTelemetry configuration, span processors, and scope classes | [design.md](../src/Observability/Runtime/docs/design.md) |
| `Microsoft.Agents.A365.Observability.Hosting` | ASP.NET Core middleware and hosting extensions | [design.md](../src/Observability/Hosting/docs/design.md) |
| `Microsoft.Agents.A365.Observability.Extensions.AgentFramework` | Agent Framework telemetry integration | [design.md](../src/Observability/Extensions/AgentFramework/docs/design.md) |
| `Microsoft.Agents.A365.Observability.Extensions.SemanticKernel` | Semantic Kernel telemetry integration | [design.md](../src/Observability/Extensions/SemanticKernel/docs/design.md) |
| `Microsoft.Agents.A365.Observability.Extensions.OpenAI` | OpenAI SDK telemetry integration | [design.md](../src/Observability/Extensions/OpenAI/docs/design.md) |
| `Microsoft.Agents.A365.Notifications` | Microsoft 365 notification handling | [design.md](../src/Notification/Microsoft.Agents.A365.Notifications/docs/design.md) |
| `Microsoft.Agents.A365.Tooling` | MCP server discovery and configuration | [design.md](../src/Tooling/Core/docs/design.md) |
| `Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel` | Semantic Kernel MCP tool registration | [design.md](../src/Tooling/Extensions/SemanticKernel/docs/design.md) |
| `Microsoft.Agents.A365.Tooling.Extensions.AgentFramework` | Agent Framework MCP tool registration | [design.md](../src/Tooling/Extensions/AgentFramework/docs/design.md) |
| `Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry` | Azure AI Foundry MCP tool registration | [design.md](../src/Tooling/Extensions/AzureAIFoundry/docs/design.md) |

## Core Package Documentation

### Runtime (`Microsoft.Agents.A365.Runtime`)

Foundational utilities shared across the SDK.

| Class | Purpose |
|-------|---------|
| `TenantContextHelper` | Extracts tenant and worker IDs from HttpContext (claims, headers, items) |
| `OperationResult` | Standardized success/failure result pattern with error collection |
| `OperationError` | Error details with HTTP status codes |
| `AgenticAuthorizationService` | Authorization handling for agentic operations |

```csharp
// Extract tenant ID with fallback precedence
var tenantId = TenantContextHelper.GetTenantId(httpContext);

// Return operation results
return OperationResult.Success;
return OperationResult.Failed(new OperationError("Not found", HttpStatusCode.NotFound));
```

### Observability Runtime (`Microsoft.Agents.A365.Observability.Runtime`)

OpenTelemetry-based distributed tracing infrastructure.

| Class | Purpose |
|-------|---------|
| `Builder` | Fluent API for configuring OpenTelemetry tracing |
| `InvokeAgentScope` | Tracing scope for agent invocation operations |
| `InferenceScope` | Tracing scope for LLM inference operations |
| `ExecuteToolScope` | Tracing scope for tool execution |
| `BaggageBuilder` | OpenTelemetry baggage context management |
| `ActivityExtensions` | Extension methods for System.Diagnostics.Activity |

```csharp
// Configure observability
services.AddOpenTelemetry()
    .WithTracing(builder => new Builder(services, configuration)
        .WithAgentFramework()
        .WithSemanticKernel()
        .Build());

// Create tracing scopes
using var scope = InvokeAgentScope.Start(invokeAgentDetails, tenantDetails, request);
scope.RecordResponse(response);
```

### Notifications (`Microsoft.Agents.A365.Notifications`)

Event-driven notification handling for Microsoft 365 applications.

| Class | Purpose |
|-------|---------|
| `AgentNotification` | AgentExtension class for routing notifications |
| `AgentNotificationActivity` | Activity wrapper for notification data |
| `EmailReference` | Email notification entity model |
| `EmailResponse` | Email response entity for replies |
| `WpxComment` | Word/PowerPoint/Excel comment model |

**Supported Sub-channels:**
- `agents:email` - Email notifications
- `agents:word` - Word document notifications
- `agents:excel` - Excel workbook notifications
- `agents:powerpoint` - PowerPoint presentation notifications
- Federated Knowledge Service notifications

```csharp
// Register notification handlers
app.OnAgenticEmailNotification(async (turnContext, turnState, notification, ct) =>
{
    var emailRef = notification.GetEntity<EmailReference>();
    var reply = turnContext.Activity.CreateEmailResponseActivity("<html>...</html>");
    await turnContext.SendActivityAsync(reply, ct);
});

// Lifecycle notifications
app.OnAgenticUserIdentityCreatedNotification(async (context, state, notification, ct) =>
{
    // Handle user identity creation
});
```

### Tooling Core (`Microsoft.Agents.A365.Tooling`)

MCP (Model Context Protocol) server discovery and configuration.

| Class | Purpose |
|-------|---------|
| `IMcpToolServerConfigurationService` | Interface for MCP server discovery |
| `McpToolServerConfigurationService` | Implementation for discovering and configuring MCP servers |
| `MCPServerConfig` | Server configuration model (name, URL, scope, audience) |
| `ToolOptions` | Configuration options for tooling operations |
| `ChatHistoryMessage` | Chat message model for threat protection |

```csharp
// Discover available MCP servers
var servers = await configService.ListToolServersAsync(agentInstanceId, authToken);

// Get tools from a specific server
var tools = await configService.GetMcpClientToolsAsync(turnContext, serverConfig, authToken, options);

// Send chat history for threat protection
await configService.SendChatHistoryAsync(turnContext, chatHistory, cancellationToken);
```

## Architecture Diagram

```
                                    ┌─────────────────────────────────────────────────────┐
                                    │                  Agent Application                   │
                                    └──────────┬──────────────────┬───────────────────────┘
                                               │                  │
              ┌────────────────────────────────┼──────────────────┼────────────────────────────────┐
              │                                │                  │                                │
              ▼                                ▼                  ▼                                ▼
    ┌─────────────────┐            ┌─────────────────┐  ┌─────────────────┐           ┌─────────────────┐
    │     Runtime     │            │  Observability  │  │  Notifications  │           │     Tooling     │
    │                 │            │                 │  │                 │           │                 │
    │ TenantContext   │◄──────────►│ Builder         │  │ AgentNotification│           │ McpToolServer   │
    │ OperationResult │            │ Scopes          │  │ EmailReference  │           │ ConfigService   │
    │ Authorization   │            │ SpanProcessors  │  │ WpxComment      │           │ MCPServerConfig │
    └─────────────────┘            └────────┬────────┘  └─────────────────┘           └────────┬────────┘
                                            │                                                  │
                    ┌───────────────────────┼───────────────────────┐                         │
                    │                       │                       │                         │
                    ▼                       ▼                       ▼                         │
          ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐               │
          │ AgentFramework  │     │ SemanticKernel  │     │     OpenAI      │               │
          │   Extension     │     │   Extension     │     │   Extension     │               │
          │                 │     │                 │     │                 │               │
          │ SpanProcessor   │     │ SpanProcessor   │     │ SpanProcessor   │               │
          │ BuilderExt      │     │ FunctionFilter  │     │ ChatToolCallExt │               │
          └─────────────────┘     └─────────────────┘     └─────────────────┘               │
                                                                                             │
                                  ┌───────────────────────┬────────────────────────┐        │
                                  │                       │                        │        │
                                  ▼                       ▼                        ▼        │
                        ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐ │
                        │ SemanticKernel  │     │ AgentFramework  │     │ AzureAIFoundry  │◄┘
                        │   Tooling Ext   │     │   Tooling Ext   │     │   Tooling Ext   │
                        │                 │     │                 │     │                 │
                        │ McpToolReg      │     │ McpToolReg      │     │ McpToolReg      │
                        │ Service         │     │ Service         │     │ Service         │
                        └─────────────────┘     └─────────────────┘     └─────────────────┘
```

## Data Flow: Agent Invocation Tracing

```
┌──────────────┐     ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Request    │────►│ BaggageMiddleware│────►│ InvokeAgentScope│────►│ InferenceScope  │
│              │     │                 │     │                 │     │                 │
│ HTTP Request │     │ Set tenant/agent│     │ Agent invocation│     │ LLM inference   │
│ with headers │     │ baggage context │     │ tracing span    │     │ tracing span    │
└──────────────┘     └─────────────────┘     └────────┬────────┘     └────────┬────────┘
                                                      │                       │
                                                      │                       ▼
                                                      │              ┌─────────────────┐
                                                      │              │ ExecuteToolScope│
                                                      │              │                 │
                                                      │              │ Tool execution  │
                                                      │              │ tracing span    │
                                                      │              └────────┬────────┘
                                                      │                       │
                                                      ▼                       ▼
                                             ┌─────────────────────────────────────────┐
                                             │            SpanProcessor                 │
                                             │                                         │
                                             │ Process spans, add tags, export to      │
                                             │ Agent365 Exporter or Console            │
                                             └─────────────────────────────────────────┘
```

## Data Flow: MCP Tool Discovery

```
┌──────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
│ Agent receives   │────►│ ListToolServersAsync│────►│ For each server:    │
│ user message     │     │                     │     │ GetMcpClientTools   │
└──────────────────┘     │ Query Tooling       │     │                     │
                         │ Gateway for servers │     │ Connect via MCP SDK │
                         └─────────────────────┘     └──────────┬──────────┘
                                                                │
                                                                ▼
                                                   ┌─────────────────────┐
                                                   │ Register tools with │
                                                   │ orchestrator:       │
                                                   │ - Semantic Kernel   │
                                                   │ - Agent Framework   │
                                                   │ - Azure AI Foundry  │
                                                   └─────────────────────┘
```

## Design Patterns

### Builder Pattern

The observability `Builder` class uses a fluent API for configuration:

```csharp
new Builder(services, configuration)
    .WithAgentFramework()
    .WithSemanticKernel()
    .WithOpenAI()
    .Build();
```

### Disposable Pattern

Scope classes implement `IDisposable` for automatic span lifecycle management:

```csharp
using var scope = InvokeAgentScope.Start(details, tenantDetails, request);
// Span is automatically ended when scope is disposed
scope.RecordResponse(response);
```

### Result Pattern

`OperationResult` provides a consistent way to handle success/failure:

```csharp
public async Task<OperationResult> DoWorkAsync()
{
    try
    {
        // Work...
        return OperationResult.Success;
    }
    catch (Exception ex)
    {
        return OperationResult.Failed(new OperationError(ex.Message, HttpStatusCode.InternalServerError));
    }
}

var result = await DoWorkAsync();
if (!result.Succeeded)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"{error.StatusCode}: {error.Message}");
}
```

### Extension Pattern

Framework integrations use extension methods:

```csharp
// Observability extensions
public static Builder WithAgentFramework(this Builder builder) { ... }
public static Builder WithSemanticKernel(this Builder builder) { ... }

// Notification extensions
public static void OnAgenticEmailNotification(this AgentApplication app, ...) { ... }

// Scope extensions
public static InvokeAgentScope FromTurnContext(this InvokeAgentScope scope, ITurnContext ctx) { ... }
```

### Strategy Pattern

Environment-conditional behavior for exporters:

```csharp
if (IsAgent365ExporterEnabled())
    tracerProviderBuilder.AddAgent365Exporter(exporterType);
else if (EnvironmentUtils.IsDevelopmentEnvironment())
    tracerProviderBuilder.AddConsoleExporter();
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `EnableAgent365Exporter` | Enable Agent365 telemetry exporter | `false` |
| `ASPNETCORE_ENVIRONMENT` | Environment name (Development, Production) | - |
| `SuppressInvokeAgentInput` | Suppress input messages in invoke_agent spans | `false` |

## Testing

Tests are organized by package in the `src/Tests/` directory:

```
src/Tests/
├── Runtime.Tests/
├── Microsoft.Agents.A365.Observability.Runtime.Tests/
├── Microsoft.Agents.A365.Observability.Runtime.IntegrationTests/
├── Microsoft.Agents.A365.Observability.Hosting.Tests/
├── Microsoft.Agents.A365.Observability.Extension.Tests/
├── Microsoft.Agents.A365.Notifications.Tests/
├── Microsoft.Agents.A365.Tooling.Tests/
├── Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Tests/
├── Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests/
└── Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry.Tests/
```

Run tests with:

```bash
dotnet test
```

## Development

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension

### Building

```bash
dotnet build src/Microsoft.Agents.A365.Sdk.sln
```

### Package Management

The solution uses centralized package management via `Directory.Packages.props`. All package versions are defined there.

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Agents.Builder` | 1.3.163-beta | Core Agents SDK |
| `OpenTelemetry` | 1.11.2 | Distributed tracing |
| `Microsoft.SemanticKernel` | 1.65.0 | Semantic Kernel orchestration |
| `Microsoft.Agents.AI` | 1.0.0-preview.251016.1 | Agent Framework |
| `ModelContextProtocol.Core` | 0.2.0-preview.3 | MCP client SDK |
| `Azure.AI.Agents.Persistent` | 1.2.0-beta.4 | Azure AI Foundry |

## External Resources

- [Microsoft Agent 365 Documentation](https://learn.microsoft.com/en-us/dotnet/api/agent365-sdk-dotnet/agent365-overview)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
