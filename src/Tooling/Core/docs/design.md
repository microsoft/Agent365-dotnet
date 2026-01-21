# Microsoft.Agents.A365.Tooling - Design Documentation

## Overview

The `Microsoft.Agents.A365.Tooling` package provides MCP (Model Context Protocol) server discovery and configuration for Microsoft Agent 365. It enables agents to discover, configure, and connect to MCP tool servers that provide additional capabilities.

## Architecture

```
Microsoft.Agents.A365.Tooling
├── Public API
│   ├── Services/
│   │   ├── IMcpToolServerConfigurationService  # Service interface
│   │   └── McpToolServerConfigurationService   # Service implementation
│   └── Models/
│       ├── MCPServerConfig                     # Server configuration
│       ├── ToolOptions                         # Configuration options
│       └── ChatHistoryMessage                  # Chat message model
├── Internal
│   ├── Handlers/
│   │   ├── BearerTokenHandler                  # Auth token handling
│   │   ├── HttpContextHeadersHandler           # Header propagation
│   │   └── HttpLoggingHandler                  # HTTP logging
│   └── Utility/
│       ├── Constants                           # Constants
│       └── Utility                             # Helper methods
```

## Key Components

### IMcpToolServerConfigurationService

**Source**: [IMcpToolServerConfigurationService.cs](../Services/IMcpToolServerConfigurationService.cs)

Interface for discovering and configuring MCP tool servers.

```csharp
public interface IMcpToolServerConfigurationService
{
    /// <summary>
    /// Gets the list of MCP Servers configured for the agent.
    /// </summary>
    Task<List<MCPServerConfig>> ListToolServersAsync(
        string agentInstanceId,
        string authToken);

    Task<List<MCPServerConfig>> ListToolServersAsync(
        string agentInstanceId,
        string authToken,
        ToolOptions toolOptions);

    /// <summary>
    /// Gets MCP Client Tools from the specified server.
    /// </summary>
    Task<IList<McpClientTool>> GetMcpClientToolsAsync(
        ITurnContext turnContext,
        MCPServerConfig mCPServerConfig,
        string authToken,
        ToolOptions toolOptions);

    /// <summary>
    /// Sends chat history for real-time threat protection.
    /// </summary>
    Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        ChatHistoryMessage[] chatHistoryMessages,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        ChatHistoryMessage[] chatHistoryMessages,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);
}
```

### McpToolServerConfigurationService

**Source**: [McpToolServerConfigurationService.cs](../Services/McpToolServerConfigurationService.cs)

Implementation of the MCP server configuration service.

```csharp
public class McpToolServerConfigurationService : IMcpToolServerConfigurationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpToolServerConfigurationService> _logger;

    public async Task<List<MCPServerConfig>> ListToolServersAsync(
        string agentInstanceId,
        string authToken,
        ToolOptions toolOptions)
    {
        // Query Tooling Gateway for configured servers
        var endpoint = BuildToolingGatewayEndpoint(toolOptions);
        var response = await _httpClient.GetAsync(endpoint);

        return await response.Content.ReadFromJsonAsync<List<MCPServerConfig>>();
    }

    public async Task<IList<McpClientTool>> GetMcpClientToolsAsync(
        ITurnContext turnContext,
        MCPServerConfig serverConfig,
        string authToken,
        ToolOptions toolOptions)
    {
        // Connect to MCP server and retrieve tools
        var transport = new StreamableHTTPClientTransport(
            new Uri(serverConfig.url),
            authToken);

        var client = await McpClientFactory.CreateAsync(transport);
        return await client.ListToolsAsync();
    }
}
```

### MCPServerConfig

**Source**: [MCPServerConfig.cs](../Models/MCPServerConfig.cs)

Model representing an MCP server configuration.

```csharp
public class MCPServerConfig
{
    /// <summary>
    /// The name of the MCP server.
    /// </summary>
    public required string mcpServerName { get; set; }

    /// <summary>
    /// The unique identifier of the MCP server.
    /// </summary>
    public required string id { get; set; }

    /// <summary>
    /// The URL endpoint of the MCP server.
    /// </summary>
    public required string url { get; set; }

    /// <summary>
    /// The OAuth scope required for the MCP server.
    /// </summary>
    public required string scope { get; set; }

    /// <summary>
    /// The audience for authentication tokens.
    /// </summary>
    public required string audience { get; set; }

    /// <summary>
    /// The publisher of the MCP server.
    /// </summary>
    public required string publisher { get; set; }
}
```

### ToolOptions

**Source**: [ToolOptions.cs](../Models/ToolOptions.cs)

Configuration options for tooling operations.

```csharp
public class ToolOptions
{
    /// <summary>
    /// The Tooling Gateway endpoint URL.
    /// </summary>
    public string? ToolingGatewayUrl { get; set; }

    /// <summary>
    /// Additional headers to include in requests.
    /// </summary>
    public Dictionary<string, string>? AdditionalHeaders { get; set; }

    /// <summary>
    /// Whether to enable logging of HTTP requests.
    /// </summary>
    public bool EnableHttpLogging { get; set; }
}
```

### ChatHistoryMessage

**Source**: [ChatHistoryMessage.cs](../Models/ChatHistoryMessage.cs)

Model for chat messages sent for threat protection.

```csharp
public class ChatHistoryMessage
{
    public string? Role { get; set; }
    public string? Content { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}
```

### HTTP Handlers

#### BearerTokenHandler

Adds bearer token authentication to HTTP requests.

```csharp
internal class BearerTokenHandler : DelegatingHandler
{
    private readonly string _token;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);

        return await base.SendAsync(request, cancellationToken);
    }
}
```

#### HttpContextHeadersHandler

Propagates headers from the current HTTP context.

```csharp
internal class HttpContextHeadersHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Propagate correlation headers
        var context = _httpContextAccessor.HttpContext;
        if (context?.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId) == true)
        {
            request.Headers.Add("X-Correlation-Id", correlationId.ToString());
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

## Design Patterns

### Service Pattern

The tooling service follows the service pattern with interface and implementation:

```csharp
// Register in DI
services.AddSingleton<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();

// Use via DI
public class MyAgent
{
    private readonly IMcpToolServerConfigurationService _toolService;

    public async Task<IList<McpClientTool>> GetToolsAsync(string agentId, string token)
    {
        var servers = await _toolService.ListToolServersAsync(agentId, token);

        var allTools = new List<McpClientTool>();
        foreach (var server in servers)
        {
            var tools = await _toolService.GetMcpClientToolsAsync(
                _turnContext, server, token, new ToolOptions());
            allTools.AddRange(tools);
        }
        return allTools;
    }
}
```

### Delegating Handler Pattern

HTTP handlers use the delegating handler chain:

```csharp
var handler = new BearerTokenHandler(token)
{
    InnerHandler = new HttpContextHeadersHandler(accessor)
    {
        InnerHandler = new HttpLoggingHandler(logger)
        {
            InnerHandler = new HttpClientHandler()
        }
    }
};

var client = new HttpClient(handler);
```

### Result Pattern

Operations return `OperationResult` for error handling:

```csharp
public async Task<OperationResult> SendChatHistoryAsync(
    ITurnContext turnContext,
    ChatHistoryMessage[] messages,
    CancellationToken cancellationToken)
{
    try
    {
        await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        return OperationResult.Success;
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "Failed to send chat history");
        return OperationResult.Failed(
            new OperationError(ex.Message, HttpStatusCode.ServiceUnavailable));
    }
}
```

## Data Flow

```
┌─────────────────────────────┐
│ Agent Application           │
│                             │
│ 1. Request tool discovery   │
└──────────────┬──────────────┘
               │
               ▼ ListToolServersAsync
┌─────────────────────────────┐
│ McpToolServerConfiguration  │
│ Service                     │
│                             │
│ Query Tooling Gateway       │
└──────────────┬──────────────┘
               │
               ▼ HTTP GET
┌─────────────────────────────┐
│ Tooling Gateway             │
│                             │
│ Returns list of configured  │
│ MCP servers for agent       │
└──────────────┬──────────────┘
               │
               ▼ List<MCPServerConfig>
┌─────────────────────────────┐
│ For each server:            │
│ GetMcpClientToolsAsync      │
│                             │
│ Connect via MCP SDK         │
│ StreamableHTTPClientTransport│
└──────────────┬──────────────┘
               │
               ▼ IList<McpClientTool>
┌─────────────────────────────┐
│ Return tools to agent       │
│                             │
│ Tools ready for registration│
│ with orchestrator           │
└─────────────────────────────┘
```

## File Structure

```
src/Tooling/Core/
├── Services/
│   ├── IMcpToolServerConfigurationService.cs  # Service interface
│   └── McpToolServerConfigurationService.cs   # Service implementation
├── Models/
│   ├── MCPServerConfig.cs                     # Server config model
│   ├── ToolOptions.cs                         # Options model
│   ├── ChatHistoryMessage.cs                  # Chat message model
│   └── ChatMessageRequest.cs                  # Request model
├── Handlers/
│   ├── BearerTokenHandler.cs                  # Auth handler
│   ├── HttpContextHeadersHandler.cs           # Header propagation
│   └── HttpLoggingHandler.cs                  # Logging handler
├── Constants.cs                               # Constants
├── Utility.cs                                 # Helper methods
├── Microsoft.Agents.A365.Tooling.csproj
└── docs/
    └── design.md                              # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.A365.Runtime` | OperationResult, tenant context |
| `Microsoft.Agents.Builder` | ITurnContext |
| `ModelContextProtocol.Core` | MCP client SDK |
| `Microsoft.SemanticKernel.Agents.Core` | Agent integration |
| `Microsoft.Extensions.Logging.Abstractions` | Logging |

## Usage Examples

### Basic Tool Discovery

```csharp
public class ToolDiscoveryService
{
    private readonly IMcpToolServerConfigurationService _configService;

    public async Task<List<McpClientTool>> DiscoverToolsAsync(
        ITurnContext turnContext,
        string agentInstanceId,
        string authToken)
    {
        // Get list of configured servers
        var servers = await _configService.ListToolServersAsync(
            agentInstanceId,
            authToken);

        // Collect tools from all servers
        var allTools = new List<McpClientTool>();

        foreach (var server in servers)
        {
            var tools = await _configService.GetMcpClientToolsAsync(
                turnContext,
                server,
                authToken,
                new ToolOptions());

            allTools.AddRange(tools);
        }

        return allTools;
    }
}
```

### With Custom Options

```csharp
var options = new ToolOptions
{
    ToolingGatewayUrl = "https://custom-gateway.example.com",
    EnableHttpLogging = true,
    AdditionalHeaders = new Dictionary<string, string>
    {
        ["X-Custom-Header"] = "value"
    }
};

var servers = await _configService.ListToolServersAsync(
    agentInstanceId,
    authToken,
    options);
```

### Threat Protection

```csharp
public async Task ProcessMessageAsync(ITurnContext turnContext)
{
    // Send chat history for threat detection
    var messages = new[]
    {
        new ChatHistoryMessage
        {
            Role = "user",
            Content = turnContext.Activity.Text,
            Timestamp = DateTimeOffset.UtcNow
        }
    };

    var result = await _configService.SendChatHistoryAsync(
        turnContext,
        messages);

    if (!result.Succeeded)
    {
        _logger.LogWarning("Threat protection check failed: {Errors}",
            string.Join(", ", result.Errors.Select(e => e.Message)));
    }
}
```

### Dependency Injection Setup

```csharp
// Program.cs
builder.Services.AddHttpClient<IMcpToolServerConfigurationService, McpToolServerConfigurationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.Configure<ToolOptions>(
    builder.Configuration.GetSection("Tooling"));
```

## External Resources

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [MCP .NET SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [Microsoft Agent 365 Tooling](https://learn.microsoft.com/microsoft-agent-365/developer/tooling)
