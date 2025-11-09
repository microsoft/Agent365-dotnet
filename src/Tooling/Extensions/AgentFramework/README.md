# Microsoft.Agents.A365.Tooling.Extensions.AgentFramework

Microsoft Agent Framework integration extensions for Microsoft Agents A365 Tooling. This package provides seamless MCP tool server integration with Agent Framework-based agent applications.

## Overview

This extension package enables automatic registration of MCP tool servers with the Microsoft Agent Framework, allowing agents to discover and use external tools through the framework's tooling capabilities.

## Features

- **Agent Framework Integration**: Seamless integration with Microsoft Agent Framework
- **MCP Server Support**: Full Model Context Protocol server capabilities
- **Authentication Handling**: Integrated authentication and hosting support
- **Observability Integration**: Built-in observability and runtime integration
- **Multi-Agent Support**: Tool sharing across multiple agents in the framework

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.AgentFramework
```

## Quick Start

### Basic Setup

```csharp
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Register tooling services
builder.Services.AddSingleton<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();
builder.Services.AddSingleton<IMcpToolRegistrationService, McpToolRegistrationService>();

var app = builder.Build();
```

### Register Tool Servers with Agent Framework

```csharp
public async Task<AIAgent> CreateAgentWithToolsAsync(
    IChatClient chatClient,
    string agentInstructions,
    IList<AITool> initialTools,
    string agentUserId,
    string environmentId,
    UserAuthorization userAuthorization,
    ITurnContext turnContext,
    IMcpToolRegistrationService mcpToolRegistrationService)
{
    // Create agent with MCP tool servers
    // Note: Due to Microsoft.Extensions.AI framework limitations, MCP tools must be set during
    // Agent creation. This method creates a new Agent instance with all tools properly initialized.
    var agent = await mcpToolRegistrationService.AddToolServersToAgent(
        chatClient,
        agentInstructions,
        initialTools,
        agentUserId,
        environmentId,
        userAuthorization,
        turnContext);
    
    return agent;
}
```

## Advanced Usage

### Custom Authentication

```csharp
// Use custom auth token
var agent = await mcpToolRegistrationService.AddToolServersToAgent(
    chatClient,
    agentInstructions,
    initialTools,
    agentUserId,
    environmentId,
    userAuthorization,
    turnContext,
    authToken: customToken);
```

### Creating Agent with Initial Tools

```csharp
// Start with some initial tools
var initialTools = new List<AITool>
{
    new FunctionTool("GetWeather"),
    new FunctionTool("SearchDatabase")
};

// Add MCP tool servers to the agent
var agent = await mcpToolRegistrationService.AddToolServersToAgent(
    chatClient,
    agentInstructions,
    initialTools,
    agentUserId,
    environmentId,
    userAuthorization,
    turnContext);
```

## Best Practices

1. **Immutable Agents**: Agent Framework agents are immutable; new instances are created when adding tools
2. **Agent Context**: Ensure proper turn context is available
3. **Authentication**: Use agentic authentication when possible
4. **Initial Tools**: Pass any existing tools in the initialTools parameter
5. **Error Handling**: Implement proper error handling for tool server failures - failed servers are logged but don't stop the process

## Related Documentation

- [Tooling Module Overview](../../README.md)
- [Core Tooling Package](../../Core/README.md)
- [Semantic Kernel Extension](../SemanticKernel/README.md)
- [Azure AI Foundry Extension](../AzureAIFoundry/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
