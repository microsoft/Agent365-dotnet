# Microsoft.Agents.A365.Tooling

Core tooling functionality for MCP (Model Context Protocol) tool server management in Microsoft Agents A365 applications. This package provides the foundation for discovering, registering, and managing tool servers across different AI frameworks.

## Overview

This package contains the core abstractions and implementations for:

- MCP tool server discovery and listing
- Tool server lifecycle management
- Authentication and authorization handling
- Framework-agnostic tool registration interfaces
- Tool server health monitoring

## Features

- **Tool Server Discovery**: Discover and enumerate available MCP tool servers
- **Registration Services**: Core interfaces for tool server registration
- **Authentication Support**: Agentic and custom token-based authentication
- **Health Monitoring**: Monitor tool server availability and health
- **Type Safety**: Strongly-typed interfaces for reliable integration

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling
```

## Quick Start

### Basic Tool Server Discovery

```csharp
using Microsoft.Agents.A365.Tooling.Services;

var toolService = serviceProvider.GetRequiredService<IMcpToolServerConfigurationService>();

// List all available tool servers for an agent
var toolServers = await toolService.ListToolServers(agentInstanceId, environmentId, authToken);

foreach (var server in toolServers)
{
    Console.WriteLine($"Tool Server: {server.mcpServerName}");
    Console.WriteLine($"  Server URL: {server.url}");
    Console.WriteLine($"  Transport: {server.transportType}");
}
```

### Get MCP Client Tools from a Server

```csharp
using Microsoft.Agents.A365.Tooling.Services;
using ModelContextProtocol.Client;

var toolService = serviceProvider.GetRequiredService<IMcpToolServerConfigurationService>();

// Get list of servers
var servers = await toolService.ListToolServers(agentInstanceId, environmentId, authToken);

// Get tools from a specific server
foreach (var server in servers)
{
    var mcpTools = await toolService.GetMcpClientTools(
        turnContext,
        server,
        environmentId,
        authToken);
    
    foreach (var tool in mcpTools)
    {
        Console.WriteLine($"  Tool: {tool.Name}");
        Console.WriteLine($"    Description: {tool.Description}");
    }
}
```

### Service Registration

```csharp
using Microsoft.Agents.A365.Tooling.Services;

builder.Services.AddSingleton<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();
```

## Core Interfaces

### IMcpToolServerConfigurationService

Primary interface for MCP tool server configuration and discovery:

```csharp
public interface IMcpToolServerConfigurationService
{
    /// <summary>
    /// Gets the list of MCP Servers that are configured for the agent.
    /// </summary>
    Task<List<MCPServerConfig>> ListToolServers(string agentInstanceId, string environmentId, string authToken);

    /// <summary>
    /// Gets the MCP Client Tools from the specified MCP server.
    /// </summary>
    Task<IList<McpClientTool>> GetMcpClientTools(ITurnContext turnContext, MCPServerConfig mCPServerConfig, string environmentId, string authToken);
}
```

### MCPServerConfig Model

Represents MCP server configuration:

```csharp
public class MCPServerConfig
{
    public string mcpServerName { get; set; }
    public string url { get; set; }
    public string transportType { get; set; }
    // Additional properties...
}
```

## Best Practices

1. **Use Dependency Injection**: Register services through DI for better testability
2. **Handle Authentication**: Always provide valid authentication tokens
3. **Error Handling**: Implement proper error handling for server communication failures
4. **Agent Context**: Ensure valid turn context is available when getting client tools
5. **Environment Isolation**: Use appropriate environment IDs to isolate tool configurations

## Integration with Extensions

This core package is designed to work with framework-specific extensions:

- **Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel**: For Semantic Kernel integration
- **Microsoft.Agents.A365.Tooling.Extensions.AgentFramework**: For Agent Framework integration
- **Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry**: For Azure AI Foundry integration

## Related Documentation

- [Tooling Module Overview](../README.md)
- [Semantic Kernel Extension](../Extensions/SemanticKernel/README.md)
- [Agent Framework Extension](../Extensions/AgentFramework/README.md)
- [Azure AI Foundry Extension](../Extensions/AzureAIFoundry/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
