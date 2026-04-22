
# Microsoft.Agents.A365.Tooling.Extensions.AgentFramework

Microsoft Agent Framework integration for the Microsoft Agent 365 Tooling SDK. Provides MCP (Model Context Protocol) tool server discovery and registration for agents built with `Microsoft.Extensions.AI` and `Microsoft.Agents.AI`.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.AgentFramework
```

## Key Features

- **MCP tool discovery** — queries the Agent 365 Tooling Gateway and connects to configured MCP servers
- **`AddToolServersToAgent`** — builds an `AIAgent` with MCP tools loaded alongside any existing tools
- **`GetMcpToolsAsync`** — returns discovered MCP tools as `IList<AITool>` for manual agent wiring
- **`SendChatHistoryAsync`** — forwards conversation history to the real-time threat-protection endpoint
- **V1/V2 per-audience token support** — V1 servers share the ATG token; V2 servers (distinct `audience`) receive audience-scoped OBO tokens automatically
- **Dev-mode support** — tokens sourced from environment variables when `ASPNETCORE_ENVIRONMENT=Development`

## Service Registration

```csharp
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Extensions;

// Program.cs
builder.Services.AddMcpServices();
```

`AddMcpServices()` registers:
- `IMcpToolServerConfigurationService` → `McpToolServerConfigurationService` (scoped)
- `IMcpToolRegistrationService` → `McpToolRegistrationService` (scoped)

## Usage

### Adding MCP tools to an agent

```csharp
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;

public class MyActivityHandler : ActivityHandler
{
    private readonly IMcpToolRegistrationService _mcpTools;
    private readonly IChatClient _chatClient;

    public MyActivityHandler(IMcpToolRegistrationService mcpTools, IChatClient chatClient)
    {
        _mcpTools = mcpTools;
        _chatClient = chatClient;
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var userAuth = turnContext.Activity.GetUserAuthorization();

        var agent = await _mcpTools.AddToolServersToAgent(
            chatClient: _chatClient,
            agentInstructions: "You are a helpful assistant.",
            initialTools: [],
            agentUserId: turnContext.Activity.From.Id,
            userAuthorization: userAuth,
            authHandlerName: "default",
            turnContext: turnContext);

        var response = await agent.CompleteAsync(turnContext.Activity.Text, cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync(response.Message.Text, cancellationToken: cancellationToken);
    }
}
```

### Retrieving tools without building an agent

```csharp
IList<AITool> tools = await _mcpTools.GetMcpToolsAsync(
    agentUserId: turnContext.Activity.From.Id,
    userAuthorization: userAuth,
    authHandlerName: "default",
    turnContext: turnContext);
```

### Sending chat history

```csharp
// From a list of ChatMessage objects
OperationResult result = await _mcpTools.SendChatHistoryAsync(chatMessages, turnContext, cancellationToken);
```

## Authentication

### Production

Token acquisition is automatic. The service uses the Agent Framework `UserAuthorization` and `authHandlerName` parameters to perform agentic OBO token exchange via `AgenticMcpTokenProvider`.

- **V1 servers** (no `audience` field, or `audience` matches the ATG App ID) — receive the shared ATG-scoped token.
- **V2 servers** (`audience` identifies a different application) — receive a token scoped to `{audience}/{scope}` or `{audience}/.default`.

### Local development

Set `ASPNETCORE_ENVIRONMENT=Development` (or `DOTNET_ENVIRONMENT=Development`). Tokens are read from environment variables instead of the OBO flow:

| Variable | Purpose |
|---|---|
| `BEARER_TOKEN_<SERVERNAME>` | Per-server token (hyphens in name replaced with underscores, uppercased) |
| `BEARER_TOKEN` | Shared fallback token for all servers |

Example: for a server named `my-mcp-server`, set `BEARER_TOKEN_MY_MCP_SERVER`.

## Environment Variables

| Variable | Purpose | Default |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT` | Set to `Development` to enable dev mode | — |
| `MCP_PLATFORM_ENDPOINT` | Override the Tooling Gateway base URL | `https://agent365.svc.cloud.microsoft` |
| `BEARER_TOKEN_<SERVERNAME>` | Dev-mode per-server Bearer token | — |
| `BEARER_TOKEN` | Dev-mode shared Bearer token fallback | — |

## Trademarks

*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
