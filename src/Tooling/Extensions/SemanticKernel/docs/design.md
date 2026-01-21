# Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel - Design Documentation

## Overview

The `Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel` package provides MCP (Model Context Protocol) tool registration for Microsoft Semantic Kernel. It enables agents built with Semantic Kernel to discover and use MCP tools as native Kernel plugins.

## Architecture

```
Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel
├── Public API
│   ├── Services/
│   │   ├── IMcpToolRegistrationService     # Service interface
│   │   └── McpToolRegistrationService      # Service implementation
│   └── ServiceCollectionExtensions         # DI registration
├── Internal
│   └── Agent365SemanticKernelSdkUserAgentConfiguration  # User agent config
```

## Key Components

### IMcpToolRegistrationService

**Source**: [IMcpToolRegistrationService.cs](../Services/IMcpToolRegistrationService.cs)

Interface for registering MCP tools with Semantic Kernel.

```csharp
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Adds A365 MCP Tool Servers to the Semantic Kernel.
    /// </summary>
    /// <param name="kernel">The kernel to add tools to.</param>
    /// <param name="userAuthorization">User authorization system.</param>
    /// <param name="authHandlerName">Authentication handler name.</param>
    /// <param name="turnContext">The current turn context.</param>
    /// <param name="authToken">Optional auth token for MCP servers.</param>
    Task AddToolServersToAgentAsync(
        Kernel kernel,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null);

    /// <summary>
    /// Sends chat history for real-time threat protection.
    /// </summary>
    /// <param name="turnContext">The turn context.</param>
    /// <param name="chatHistory">The Semantic Kernel chat history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result indicating success or failure.</returns>
    Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        ChatHistory chatHistory,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);
}
```

### McpToolRegistrationService

**Source**: [McpToolRegistrationService.cs](../Services/McpToolRegistrationService.cs)

Implementation that bridges MCP tools to Semantic Kernel plugins.

```csharp
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly IMcpToolServerConfigurationService _configService;
    private readonly ILogger<McpToolRegistrationService> _logger;

    public async Task AddToolServersToAgentAsync(
        Kernel kernel,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null)
    {
        // Get auth token if not provided
        authToken ??= await GetAuthTokenAsync(userAuthorization, authHandlerName, turnContext);

        // Get agent instance ID
        var agentInstanceId = GetAgentInstanceId(turnContext);

        // List available MCP servers
        var servers = await _configService.ListToolServersAsync(agentInstanceId, authToken);

        // Register tools from each server as Kernel plugins
        foreach (var server in servers)
        {
            var tools = await _configService.GetMcpClientToolsAsync(
                turnContext, server, authToken, new ToolOptions());

            // Convert MCP tools to Kernel functions
            foreach (var tool in tools)
            {
                var function = CreateKernelFunctionFromMcpTool(tool, server, authToken);
                kernel.Plugins.AddFromFunctions(server.mcpServerName, new[] { function });
            }
        }
    }

    public async Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        // Convert Semantic Kernel ChatHistory to ChatHistoryMessage[]
        var messages = chatHistory.Select(m => new ChatHistoryMessage
        {
            Role = m.Role.ToString(),
            Content = m.Content,
            Timestamp = DateTimeOffset.UtcNow  // SK doesn't store timestamps
        }).ToArray();

        return await _configService.SendChatHistoryAsync(
            turnContext, messages, cancellationToken);
    }
}
```

### ServiceCollectionExtensions

**Source**: [ServiceCollectionExtensions.cs](../ServiceCollectionExtensions.cs)

Extension methods for registering services in the DI container.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgent365ToolingForSemanticKernel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register core tooling service
        services.AddSingleton<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();

        // Register SK-specific registration service
        services.AddSingleton<IMcpToolRegistrationService, McpToolRegistrationService>();

        // Configure user agent
        services.AddSingleton<IUserAgentConfiguration, Agent365SemanticKernelSdkUserAgentConfiguration>();

        return services;
    }
}
```

## Design Patterns

### Bridge Pattern

The service bridges MCP tools to Semantic Kernel's plugin system:

```csharp
// MCP Tool -> Semantic Kernel Function
private KernelFunction CreateKernelFunctionFromMcpTool(
    McpClientTool mcpTool,
    MCPServerConfig server,
    string authToken)
{
    return KernelFunctionFactory.CreateFromMethod(
        async (KernelArguments args, CancellationToken ct) =>
        {
            // Call MCP tool with arguments
            var result = await mcpTool.InvokeAsync(args.ToDictionary(), ct);
            return result.ToString();
        },
        mcpTool.Name,
        mcpTool.Description,
        CreateParametersFromSchema(mcpTool.InputSchema)
    );
}
```

### Adapter Pattern

Converts between Semantic Kernel and MCP data formats:

```csharp
// ChatHistory -> ChatHistoryMessage[]
var messages = chatHistory.Select(m => new ChatHistoryMessage
{
    Role = m.Role.ToString(),
    Content = m.Content,
    Timestamp = DateTimeOffset.UtcNow
}).ToArray();
```

### Service Pattern

Uses dependency injection for loose coupling:

```csharp
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly IMcpToolServerConfigurationService _configService;
    private readonly ILogger<McpToolRegistrationService> _logger;

    public McpToolRegistrationService(
        IMcpToolServerConfigurationService configService,
        ILogger<McpToolRegistrationService> logger)
    {
        _configService = configService;
        _logger = logger;
    }
}
```

## Data Flow

```
┌─────────────────────────────┐
│ Agent Application           │
│                             │
│ AddToolServersToAgentAsync  │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ McpToolRegistrationService  │
│                             │
│ 1. Get auth token           │
│ 2. List MCP servers         │
│ 3. Get tools from servers   │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ IMcpToolServerConfiguration │
│ Service                     │
│                             │
│ Core tooling discovery      │
└──────────────┬──────────────┘
               │
               ▼ McpClientTool[]
┌─────────────────────────────┐
│ Convert to KernelFunctions  │
│                             │
│ CreateKernelFunctionFrom    │
│ McpTool()                   │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ Register with Kernel        │
│                             │
│ kernel.Plugins.AddFrom      │
│ Functions()                 │
└─────────────────────────────┘
```

## File Structure

```
src/Tooling/Extensions/SemanticKernel/
├── Services/
│   ├── IMcpToolRegistrationService.cs      # Service interface
│   └── McpToolRegistrationService.cs       # Service implementation
├── ServiceCollectionExtensions.cs          # DI registration
├── Agent365SemanticKernelSdkUserAgentConfiguration.cs  # User agent config
├── Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.csproj
└── docs/
    └── design.md                           # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.A365.Tooling` | Core tooling service |
| `Microsoft.Agents.A365.Runtime` | OperationResult |
| `Microsoft.SemanticKernel` | Semantic Kernel SDK |
| `Microsoft.SemanticKernel.Agents.Core` | SK Agents |
| `ModelContextProtocol.Core` | MCP client SDK |
| `Microsoft.Agents.Authentication.Msal` | Authentication |
| `Microsoft.Agents.Hosting.AspNetCore` | Hosting |

## Usage Examples

### Basic Setup

```csharp
// Program.cs
builder.Services.AddAgent365ToolingForSemanticKernel(builder.Configuration);

// Create kernel
var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey)
    .Build();

// Register MCP tools
var registrationService = serviceProvider.GetRequiredService<IMcpToolRegistrationService>();
await registrationService.AddToolServersToAgentAsync(
    kernel,
    userAuthorization,
    "default",
    turnContext);

// Tools are now available as Kernel plugins
var result = await kernel.InvokePromptAsync("Search for recent news about AI");
```

### With Agent Handler

```csharp
public class MyAgentHandler : IActivityHandler
{
    private readonly IMcpToolRegistrationService _toolService;
    private readonly Kernel _kernel;
    private readonly UserAuthorization _userAuth;

    public async Task OnMessageActivityAsync(ITurnContext turnContext, CancellationToken ct)
    {
        // Register MCP tools for this conversation
        await _toolService.AddToolServersToAgentAsync(
            _kernel,
            _userAuth,
            "MyAuthHandler",
            turnContext);

        // Use kernel with registered tools
        var agent = new ChatCompletionAgent
        {
            Name = "Assistant",
            Instructions = "You are a helpful assistant with access to external tools.",
            Kernel = _kernel
        };

        var history = new ChatHistory();
        history.AddUserMessage(turnContext.Activity.Text);

        await foreach (var message in agent.InvokeAsync(history))
        {
            await turnContext.SendActivityAsync(message.Content, cancellationToken: ct);
        }
    }
}
```

### Threat Protection

```csharp
public async Task ProcessConversationAsync(
    ITurnContext turnContext,
    ChatHistory chatHistory)
{
    // Send chat history for threat detection
    var result = await _toolService.SendChatHistoryAsync(
        turnContext,
        chatHistory);

    if (!result.Succeeded)
    {
        _logger.LogWarning("Threat protection returned: {Errors}",
            string.Join(", ", result.Errors.Select(e => e.Message)));
    }

    // Continue processing...
}
```

### With Custom Auth Token

```csharp
// If you have your own auth token
var customToken = await GetCustomAuthTokenAsync();

await _toolService.AddToolServersToAgentAsync(
    kernel,
    userAuthorization,
    "default",
    turnContext,
    authToken: customToken);
```

### With Tool Options

```csharp
var options = new ToolOptions
{
    ToolingGatewayUrl = "https://custom-gateway.example.com",
    EnableHttpLogging = true
};

var result = await _toolService.SendChatHistoryAsync(
    turnContext,
    chatHistory,
    options);
```

## External Resources

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Semantic Kernel Plugins](https://learn.microsoft.com/semantic-kernel/concepts/plugins/)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Microsoft Agent 365 Tooling](https://learn.microsoft.com/microsoft-agent-365/developer/tooling)
