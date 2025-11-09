# Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry

Azure AI Foundry integration extensions for Microsoft Agents A365 Tooling. This package provides seamless MCP tool server integration with Azure AI Foundry-based agent applications.

## Overview

This extension package enables automatic registration of MCP tool servers with Azure AI Foundry, allowing agents to discover and use external tools through Azure AI Foundry's cloud-based orchestration capabilities.

## Features

- **Azure AI Foundry Integration**: Seamless integration with Azure AI Foundry endpoints
- **Cloud-Based Orchestration**: Tool registration with cloud-based agent orchestration
- **MCP Server Support**: Full Model Context Protocol server capabilities
- **Authentication Handling**: Azure-native authentication and authorization
- **Scalability**: Cloud-scale tool server management

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry
```

## Quick Start

### Basic Setup

```csharp
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;
using Azure.AI.Agents.Persistent;

var builder = WebApplication.CreateBuilder(args);

// Register tooling services
builder.Services.AddSingleton<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();
builder.Services.AddSingleton<IMcpToolRegistrationService, McpToolRegistrationService>();

var app = builder.Build();
```

### Register Tool Servers with Azure AI Foundry

```csharp
public void ConfigureAgentWithMcpTools(
    PersistentAgentsClient agentClient,
    string agentInstanceId,
    string environmentId,
    UserAuthorization userAuthorization,
    ITurnContext turnContext,
    IMcpToolRegistrationService mcpToolRegistrationService)
{
    // Register MCP tool servers with the Persistent Agent
    // Note: Persistent Agents cannot be mutated after creation,
    // so this updates the agent definition with MCP tool definitions
    mcpToolRegistrationService.AddToolServersToAgent(
        agentClient,
        agentInstanceId,
        environmentId,
        userAuthorization,
        turnContext);
}
```

### Alternative: Get Tool Definitions for Agent Creation

```csharp
public async Task<(IList<MCPToolDefinition>, ToolResources?)> GetMcpToolsForAgentCreationAsync(
    string agentInstanceId,
    string environmentId,
    string authToken,
    ITurnContext turnContext,
    IMcpToolRegistrationService mcpToolRegistrationService)
{
    // Get MCP tool definitions and resources for creating a new Persistent Agent
    var (toolDefinitions, toolResources) = await mcpToolRegistrationService
        .GetMcpToolDefinitionsAndResourcesAsync(
            agentInstanceId,
            environmentId,
            authToken,
            turnContext);
    
    return (toolDefinitions, toolResources);
}
```

## Configuration

### Environment Variables

Configuration is typically handled through turn context and authentication services. The MCP tool servers are configured per agent instance in the specified environment.

```csharp
// Authentication is handled through UserAuthorization and ITurnContext
// No additional appsettings.json configuration is required
```

## Advanced Usage

### Custom Authentication

```csharp
// Use custom auth token instead of agentic authentication
mcpToolRegistrationService.AddToolServersToAgent(
    agentClient,
    agentInstanceId,
    environmentId,
    userAuthorization,
    turnContext,
    authToken: customToken);
```

### Using agenticAppId from Turn Context

```csharp
// The agenticAppId is extracted from the turn context automatically
// If not provided as agentInstanceId parameter
mcpToolRegistrationService.AddToolServersToAgent(
    agentClient,
    environmentId,
    userAuthorization,
    turnContext); // agenticAppId extracted from turnContext.Activity.Recipient.AgenticAppId
```

## Best Practices

1. **Persistent Agents**: Note that Persistent Agents cannot be mutated after creation; tool definitions are updated through the Administration API
2. **Authentication**: Use agentic authentication when possible for seamless integration
3. **Error Handling**: Implement proper error handling as tool server failures are logged
4. **Tool Definitions**: Use `GetMcpToolDefinitionsAndResourcesAsync` when creating new agents with tools
5. **Agent Updates**: Use `AddToolServersToAgent` to update existing agent tool configurations

## Related Documentation

- [Tooling Module Overview](../../README.md)
- [Core Tooling Package](../../Core/README.md)
- [Semantic Kernel Extension](../SemanticKernel/README.md)
- [Agent Framework Extension](../AgentFramework/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
