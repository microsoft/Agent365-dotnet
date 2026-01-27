# Microsoft.Agents.A365.Tooling.Extensions.AgentFramework - Design Documentation

## Overview

The `Microsoft.Agents.A365.Tooling.Extensions.AgentFramework` package provides MCP (Model Context Protocol) tool registration for the Microsoft Agent Framework. It enables agents built with Agent Framework to discover and use MCP tools as native `AITool` instances.

## Architecture

```
Microsoft.Agents.A365.Tooling.Extensions.AgentFramework
├── Public API
│   ├── Services/
│   │   ├── IMcpToolRegistrationService     # Service interface
│   │   └── McpToolRegistrationService      # Service implementation
│   └── ServiceCollectionExtensions         # DI registration
├── Internal
│   └── Agent365AgentFrameworkSdkUserAgentConfiguration  # User agent config
```

## Key Components

### IMcpToolRegistrationService

**Source**: [IMcpToolRegistrationService.cs](../Services/IMcpToolRegistrationService.cs)

Interface for registering MCP tools with Agent Framework.

```csharp
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Add new MCP servers to the agent by creating a new Agent instance.
    ///
    /// Note: Due to Microsoft.Extensions.AI framework limitations, MCP tools must be set
    /// during Agent creation. If new tools are found, this method creates a new Agent
    /// instance with all tools (existing + new) properly initialized.
    /// </summary>
    /// <param name="chatClient">The configured IChatClient.</param>
    /// <param name="agentInstructions">The agent instructions.</param>
    /// <param name="initialTools">Existing tools to keep.</param>
    /// <param name="agentUserId">Agent user ID.</param>
    /// <param name="turnContext">Turn context for current request.</param>
    /// <param name="userAuthorization">User authorization information.</param>
    /// <param name="authHandlerName">Authentication handler name.</param>
    /// <param name="authToken">Optional auth token for MCP servers.</param>
    /// <returns>New Agent instance with all MCP tools.</returns>
    Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null);

    /// <summary>
    /// Returns a List of MCP tools to be added to the agent.
    /// </summary>
    Task<IList<AITool>> GetMcpToolsAsync(
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null);

    /// <summary>
    /// Sends chat history to the MCP platform.
    /// </summary>
    Task<OperationResult> SendChatHistoryAsync(
        IEnumerable<ChatMessage> chatMessages,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SendChatHistoryAsync(
        IEnumerable<ChatMessage> chatMessages,
        ITurnContext turnContext,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends chat history from a ChatMessageStore.
    /// </summary>
    Task<OperationResult> SendChatHistoryAsync(
        ChatMessageStore chatMessageStore,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SendChatHistoryAsync(
        ChatMessageStore chatMessageStore,
        ITurnContext turnContext,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);
}
```

### McpToolRegistrationService

**Source**: [McpToolRegistrationService.cs](../Services/McpToolRegistrationService.cs)

Implementation that creates new Agent instances with MCP tools.

```csharp
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly IMcpToolServerConfigurationService _configService;
    private readonly ILogger<McpToolRegistrationService> _logger;

    public async Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null)
    {
        // Get MCP tools
        var mcpTools = await GetMcpToolsAsync(
            agentUserId, userAuthorization, authHandlerName, turnContext, authToken);

        // Combine initial tools with MCP tools
        var allTools = new List<AITool>(initialTools);
        allTools.AddRange(mcpTools);

        // Create new Agent with all tools
        // Note: Agent Framework requires tools at creation time
        return new AIAgent(chatClient, agentInstructions, allTools);
    }

    public async Task<IList<AITool>> GetMcpToolsAsync(
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null)
    {
        // Get auth token if not provided
        authToken ??= await GetAuthTokenAsync(userAuthorization, authHandlerName, turnContext);

        // List available MCP servers
        var servers = await _configService.ListToolServersAsync(agentUserId, authToken);

        var tools = new List<AITool>();

        foreach (var server in servers)
        {
            var mcpTools = await _configService.GetMcpClientToolsAsync(
                turnContext, server, authToken, new ToolOptions());

            // Convert MCP tools to AITool
            foreach (var mcpTool in mcpTools)
            {
                tools.Add(ConvertToAITool(mcpTool, server, authToken));
            }
        }

        return tools;
    }
}
```

## Design Patterns

### Immutable Agent Pattern

Due to Agent Framework design, tools must be set at agent creation:

```csharp
// Agent Framework tools are immutable after creation
// Must create new agent instance when tools change

public async Task<AIAgent> AddToolServersToAgent(
    IChatClient chatClient,
    string agentInstructions,
    IList<AITool> initialTools,
    ...)
{
    var mcpTools = await GetMcpToolsAsync(...);

    // Combine all tools
    var allTools = initialTools.Concat(mcpTools).ToList();

    // Create NEW agent with complete tool set
    return new AIAgent(chatClient, agentInstructions, allTools);
}
```

### Adapter Pattern

Converts MCP tools to Agent Framework's `AITool`:

```csharp
private AITool ConvertToAITool(
    McpClientTool mcpTool,
    MCPServerConfig server,
    string authToken)
{
    return AIFunctionFactory.Create(
        async (IDictionary<string, object?> args, CancellationToken ct) =>
        {
            var result = await mcpTool.InvokeAsync(args, ct);
            return result.ToString();
        },
        mcpTool.Name,
        mcpTool.Description,
        CreateParameterMetadata(mcpTool.InputSchema)
    );
}
```

### Multiple Input Adapters

Supports multiple chat history input formats:

```csharp
// From IEnumerable<ChatMessage>
Task<OperationResult> SendChatHistoryAsync(
    IEnumerable<ChatMessage> chatMessages, ...);

// From ChatMessageStore
Task<OperationResult> SendChatHistoryAsync(
    ChatMessageStore chatMessageStore, ...);
```

## Data Flow

```
┌─────────────────────────────┐
│ Agent Application           │
│                             │
│ AddToolServersToAgent()     │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ McpToolRegistrationService  │
│                             │
│ 1. Get auth token           │
│ 2. GetMcpToolsAsync()       │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ IMcpToolServerConfiguration │
│ Service                     │
│                             │
│ List servers, get tools     │
└──────────────┬──────────────┘
               │
               ▼ McpClientTool[]
┌─────────────────────────────┐
│ Convert to AITool[]         │
│                             │
│ ConvertToAITool() for each  │
└──────────────┬──────────────┘
               │
               ▼ IList<AITool>
┌─────────────────────────────┐
│ Create new AIAgent          │
│                             │
│ new AIAgent(client,         │
│   instructions, allTools)   │
└─────────────────────────────┘
```

## File Structure

```
src/Tooling/Extensions/AgentFramework/
├── Services/
│   ├── IMcpToolRegistrationService.cs      # Service interface
│   └── McpToolRegistrationService.cs       # Service implementation
├── ServiceCollectionExtensions.cs          # DI registration
├── Agent365AgentFrameworkSdkUserAgentConfiguration.cs  # User agent config
├── Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.csproj
└── docs/
    └── design.md                           # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.A365.Tooling` | Core tooling service |
| `Microsoft.Agents.A365.Runtime` | OperationResult |
| `Microsoft.Agents.AI` | Agent Framework SDK |
| `Microsoft.Extensions.AI` | AI abstractions |
| `Microsoft.Extensions.AI.AzureAIInference` | Azure AI integration |
| `Microsoft.Extensions.AI.OpenAI` | OpenAI integration |
| `Azure.AI.OpenAI` | Azure OpenAI SDK |
| `Azure.Identity` | Azure authentication |
| `ModelContextProtocol.Core` | MCP client SDK |

## Usage Examples

### Basic Agent with MCP Tools

```csharp
// Program.cs
builder.Services.AddAgent365ToolingForAgentFramework(builder.Configuration);

// In agent handler
public class MyAgentHandler : IActivityHandler
{
    private readonly IMcpToolRegistrationService _toolService;

    public async Task OnMessageActivityAsync(ITurnContext turnContext, CancellationToken ct)
    {
        // Create chat client
        var chatClient = new AzureOpenAIChatClient(deployment, endpoint, credential);

        // Initial tools (if any)
        var initialTools = new List<AITool>
        {
            AIFunctionFactory.Create(GetWeather, "get_weather", "Get weather for a location")
        };

        // Create agent with MCP tools
        var agent = await _toolService.AddToolServersToAgent(
            chatClient,
            "You are a helpful assistant with access to external tools.",
            initialTools,
            turnContext.Activity.From.Id,
            _userAuth,
            "default",
            turnContext);

        // Use agent
        var response = await agent.InvokeAsync(turnContext.Activity.Text);
        await turnContext.SendActivityAsync(response, cancellationToken: ct);
    }
}
```

### Getting Tools Separately

```csharp
// Get tools without creating agent
var mcpTools = await _toolService.GetMcpToolsAsync(
    agentUserId,
    userAuthorization,
    "default",
    turnContext);

// Use with existing agent creation logic
var allTools = existingTools.Concat(mcpTools).ToList();
var agent = new AIAgent(chatClient, instructions, allTools);
```

### Threat Protection with ChatMessage

```csharp
public async Task ProcessConversationAsync(
    ITurnContext turnContext,
    IEnumerable<ChatMessage> messages)
{
    var result = await _toolService.SendChatHistoryAsync(
        messages,
        turnContext);

    if (!result.Succeeded)
    {
        _logger.LogWarning("Threat protection: {Errors}",
            string.Join(", ", result.Errors.Select(e => e.Message)));
    }
}
```

### Threat Protection with ChatMessageStore

```csharp
public async Task ProcessConversationAsync(
    ITurnContext turnContext,
    ChatMessageStore messageStore)
{
    var result = await _toolService.SendChatHistoryAsync(
        messageStore,
        turnContext);

    if (!result.Succeeded)
    {
        _logger.LogWarning("Threat protection: {Errors}",
            string.Join(", ", result.Errors.Select(e => e.Message)));
    }
}
```

### With Custom Options

```csharp
var options = new ToolOptions
{
    ToolingGatewayUrl = "https://custom-gateway.example.com",
    EnableHttpLogging = true
};

var result = await _toolService.SendChatHistoryAsync(
    chatMessages,
    turnContext,
    options);
```

## Important Notes

### Tool Immutability

Agent Framework requires tools to be specified at agent creation time. The `AddToolServersToAgent` method returns a **new** `AIAgent` instance with all tools combined:

```csharp
// WRONG - trying to add tools to existing agent
agent.AddTool(mcpTool);  // Not supported

// CORRECT - create new agent with combined tools
var newAgent = await _toolService.AddToolServersToAgent(
    chatClient, instructions, existingTools, ...);
```

### Chat History and Empty Arrays

The `SendChatHistoryAsync` method passes all messages to the MCP platform, including empty arrays. This is important because:
- The MCP platform may need to be notified even when no messages exist
- An empty array signals a valid state (e.g., conversation initialization)
- The platform handles empty arrays according to its own logic

```csharp
// Empty arrays are passed to MCP platform - this is valid and expected
var emptyMessages = new List<ChatMessage>();
var result = await _toolService.SendChatHistoryAsync(emptyMessages, turnContext);
// Result indicates success/failure from MCP platform, not local validation
```

### Performance Considerations

Since a new agent must be created when tools change, consider:
- Caching tool discovery results when possible
- Minimizing tool discovery calls per conversation
- Pre-warming tool discovery during application startup

## External Resources

- [Microsoft Agent Framework](https://github.com/microsoft/agents)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Microsoft Agent 365 Tooling](https://learn.microsoft.com/microsoft-agent-365/developer/tooling)
