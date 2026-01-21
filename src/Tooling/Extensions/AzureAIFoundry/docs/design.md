# Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry - Design Documentation

## Overview

The `Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry` package provides MCP (Model Context Protocol) tool registration for Azure AI Foundry Persistent Agents. It enables agents built with Azure AI Foundry to discover and use MCP tools.

## Architecture

```
Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry
├── Public API
│   ├── Services/
│   │   ├── IMcpToolRegistrationService     # Service interface
│   │   └── McpToolRegistrationService      # Service implementation
│   └── ServiceCollectionExtensions         # DI registration
├── Internal
│   └── Agent365AzureAIFoundrySdkUserAgentConfiguration  # User agent config
```

## Key Components

### IMcpToolRegistrationService

**Source**: [IMcpToolRegistrationService.cs](../Services/IMcpToolRegistrationService.cs)

Interface for registering MCP tools with Azure AI Foundry Persistent Agents.

```csharp
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Gets MCP tools for registration with Azure AI Foundry agents.
    /// </summary>
    /// <param name="agentInstanceId">Agent instance ID.</param>
    /// <param name="turnContext">Turn context for current request.</param>
    /// <param name="userAuthorization">User authorization information.</param>
    /// <param name="authHandlerName">Authentication handler name.</param>
    /// <param name="authToken">Optional auth token for MCP servers.</param>
    /// <returns>List of tools compatible with Azure AI Foundry.</returns>
    Task<IList<ToolDefinition>> GetMcpToolsAsync(
        string agentInstanceId,
        ITurnContext turnContext,
        UserAuthorization userAuthorization,
        string authHandlerName,
        string? authToken = null);

    /// <summary>
    /// Adds MCP tools to an Azure AI Foundry Persistent Agent.
    /// </summary>
    /// <param name="agentClient">The Azure AI Foundry agent client.</param>
    /// <param name="agentId">The agent ID to add tools to.</param>
    /// <param name="turnContext">Turn context for current request.</param>
    /// <param name="userAuthorization">User authorization information.</param>
    /// <param name="authHandlerName">Authentication handler name.</param>
    /// <param name="authToken">Optional auth token for MCP servers.</param>
    Task AddToolsToAgentAsync(
        PersistentAgentsClient agentClient,
        string agentId,
        ITurnContext turnContext,
        UserAuthorization userAuthorization,
        string authHandlerName,
        string? authToken = null);

    /// <summary>
    /// Sends chat history for real-time threat protection.
    /// </summary>
    Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        IEnumerable<ThreadMessage> messages,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        IEnumerable<ThreadMessage> messages,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);
}
```

### McpToolRegistrationService

**Source**: [McpToolRegistrationService.cs](../Services/McpToolRegistrationService.cs)

Implementation that bridges MCP tools to Azure AI Foundry's tool definitions.

```csharp
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly IMcpToolServerConfigurationService _configService;
    private readonly ILogger<McpToolRegistrationService> _logger;

    public async Task<IList<ToolDefinition>> GetMcpToolsAsync(
        string agentInstanceId,
        ITurnContext turnContext,
        UserAuthorization userAuthorization,
        string authHandlerName,
        string? authToken = null)
    {
        // Get auth token if not provided
        authToken ??= await GetAuthTokenAsync(userAuthorization, authHandlerName, turnContext);

        // List available MCP servers
        var servers = await _configService.ListToolServersAsync(agentInstanceId, authToken);

        var tools = new List<ToolDefinition>();

        foreach (var server in servers)
        {
            var mcpTools = await _configService.GetMcpClientToolsAsync(
                turnContext, server, authToken, new ToolOptions());

            // Convert MCP tools to Azure AI Foundry ToolDefinition
            foreach (var mcpTool in mcpTools)
            {
                tools.Add(ConvertToToolDefinition(mcpTool, server));
            }
        }

        return tools;
    }

    public async Task AddToolsToAgentAsync(
        PersistentAgentsClient agentClient,
        string agentId,
        ITurnContext turnContext,
        UserAuthorization userAuthorization,
        string authHandlerName,
        string? authToken = null)
    {
        var tools = await GetMcpToolsAsync(
            agentId, turnContext, userAuthorization, authHandlerName, authToken);

        // Update agent with new tools
        await agentClient.UpdateAgentAsync(agentId, new AgentUpdateOptions
        {
            Tools = tools
        });
    }

    private ToolDefinition ConvertToToolDefinition(
        McpClientTool mcpTool,
        MCPServerConfig server)
    {
        return new FunctionToolDefinition(
            mcpTool.Name,
            mcpTool.Description,
            ConvertInputSchema(mcpTool.InputSchema));
    }
}
```

## Design Patterns

### Bridge Pattern

Bridges MCP tools to Azure AI Foundry's tool format:

```csharp
// MCP Tool -> Azure AI Foundry ToolDefinition
private ToolDefinition ConvertToToolDefinition(
    McpClientTool mcpTool,
    MCPServerConfig server)
{
    return new FunctionToolDefinition(
        mcpTool.Name,
        mcpTool.Description,
        ConvertInputSchema(mcpTool.InputSchema));
}

private BinaryData ConvertInputSchema(JsonElement schema)
{
    // Convert MCP JSON Schema to Azure AI Foundry format
    return BinaryData.FromObjectAsJson(schema);
}
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

### Adapter Pattern for Messages

Converts between Azure AI Foundry messages and threat protection format:

```csharp
public async Task<OperationResult> SendChatHistoryAsync(
    ITurnContext turnContext,
    IEnumerable<ThreadMessage> messages,
    CancellationToken cancellationToken = default)
{
    // Convert ThreadMessage to ChatHistoryMessage
    var chatMessages = messages.Select(m => new ChatHistoryMessage
    {
        Role = m.Role.ToString(),
        Content = ExtractContent(m),
        Timestamp = m.CreatedAt
    }).ToArray();

    return await _configService.SendChatHistoryAsync(
        turnContext, chatMessages, cancellationToken);
}
```

## Data Flow

```
┌─────────────────────────────┐
│ Agent Application           │
│                             │
│ GetMcpToolsAsync() or       │
│ AddToolsToAgentAsync()      │
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
│ Convert to ToolDefinition   │
│                             │
│ ConvertToToolDefinition()   │
│ for Azure AI Foundry format │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ Return/Register with        │
│ Azure AI Foundry            │
│                             │
│ PersistentAgentsClient.     │
│ UpdateAgentAsync()          │
└─────────────────────────────┘
```

## File Structure

```
src/Tooling/Extensions/AzureAIFoundry/
├── Services/
│   ├── IMcpToolRegistrationService.cs      # Service interface
│   └── McpToolRegistrationService.cs       # Service implementation
├── ServiceCollectionExtensions.cs          # DI registration
├── Agent365AzureAIFoundrySdkUserAgentConfiguration.cs  # User agent config
├── Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry.csproj
└── docs/
    └── design.md                           # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.A365.Tooling` | Core tooling service |
| `Microsoft.Agents.A365.Runtime` | OperationResult |
| `Azure.AI.Agents.Persistent` | Azure AI Foundry Persistent Agents (beta) |
| `ModelContextProtocol.Core` | MCP client SDK |
| `Microsoft.Agents.Authentication.Msal` | Authentication |
| `Microsoft.Agents.Hosting.AspNetCore` | Hosting |

## Usage Examples

### Getting Tools for Agent Creation

```csharp
// Program.cs
builder.Services.AddAgent365ToolingForAzureAIFoundry(builder.Configuration);

// In agent handler
public class MyAgentHandler
{
    private readonly IMcpToolRegistrationService _toolService;
    private readonly PersistentAgentsClient _agentClient;

    public async Task CreateAgentWithMcpToolsAsync(ITurnContext turnContext)
    {
        // Get MCP tools
        var mcpTools = await _toolService.GetMcpToolsAsync(
            "my-agent-instance",
            turnContext,
            _userAuth,
            "default");

        // Combine with built-in tools
        var allTools = new List<ToolDefinition>
        {
            new CodeInterpreterToolDefinition(),
            new FileSearchToolDefinition()
        };
        allTools.AddRange(mcpTools);

        // Create agent with all tools
        var agent = await _agentClient.CreateAgentAsync(
            model: "gpt-4o",
            new AgentCreationOptions
            {
                Name = "My Assistant",
                Instructions = "You are a helpful assistant with access to external tools.",
                Tools = allTools
            });
    }
}
```

### Adding Tools to Existing Agent

```csharp
// Add MCP tools to an existing Persistent Agent
await _toolService.AddToolsToAgentAsync(
    _agentClient,
    existingAgentId,
    turnContext,
    _userAuth,
    "default");
```

### Threat Protection

```csharp
public async Task ProcessThreadAsync(
    ITurnContext turnContext,
    string threadId)
{
    // Get thread messages
    var messages = await _agentClient.GetThreadMessagesAsync(threadId);

    // Send for threat detection
    var result = await _toolService.SendChatHistoryAsync(
        turnContext,
        messages);

    if (!result.Succeeded)
    {
        _logger.LogWarning("Threat protection: {Errors}",
            string.Join(", ", result.Errors.Select(e => e.Message)));
    }
}
```

### Full Agent Conversation Flow

```csharp
public class AzureAIFoundryAgentService
{
    private readonly PersistentAgentsClient _agentClient;
    private readonly IMcpToolRegistrationService _toolService;

    public async Task<string> ProcessConversationAsync(
        string userMessage,
        ITurnContext turnContext)
    {
        // Create agent with MCP tools
        var mcpTools = await _toolService.GetMcpToolsAsync(
            "agent-instance-1", turnContext, _userAuth, "default");

        var agent = await _agentClient.CreateAgentAsync("gpt-4o", new AgentCreationOptions
        {
            Name = "Assistant",
            Instructions = "You are helpful.",
            Tools = mcpTools.ToList()
        });

        // Create thread and run
        var thread = await _agentClient.CreateThreadAsync();
        await _agentClient.CreateMessageAsync(thread.Value.Id, MessageRole.User, userMessage);

        var run = await _agentClient.CreateRunAsync(thread.Value.Id, agent.Value.Id);

        // Wait for completion and handle tool calls
        while (run.Value.Status == RunStatus.InProgress ||
               run.Value.Status == RunStatus.RequiresAction)
        {
            await Task.Delay(1000);
            run = await _agentClient.GetRunAsync(thread.Value.Id, run.Value.Id);

            if (run.Value.Status == RunStatus.RequiresAction)
            {
                // Handle tool calls via MCP
                await HandleToolCallsAsync(run.Value, thread.Value.Id);
            }
        }

        // Get response
        var messages = await _agentClient.GetMessagesAsync(thread.Value.Id);
        return messages.Value.Data.Last().Content.First().Text;
    }
}
```

## Notes

### Beta Status

Azure AI Foundry Persistent Agents SDK is currently in beta (v1.2.0-beta.4). APIs may change in future releases.

### Tool Execution

Unlike Semantic Kernel and Agent Framework integrations, Azure AI Foundry handles tool execution on the server side. MCP tools registered here will be called by the Azure AI infrastructure.

## External Resources

- [Azure AI Foundry](https://learn.microsoft.com/azure/ai-studio/)
- [Azure AI Agents Persistent](https://learn.microsoft.com/azure/ai-services/agents/)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Microsoft Agent 365 Tooling](https://learn.microsoft.com/microsoft-agent-365/developer/tooling)
