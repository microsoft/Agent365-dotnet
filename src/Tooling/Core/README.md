# Microsoft.Agents.A365.Tooling

Core tooling functionality for MCP (Model Context Protocol) tool server management in Microsoft Agents A365 applications. This package provides the foundation for discovering, registering, and managing tool servers across different AI frameworks.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling
```

## Usage

### Tool Server Discovery

```csharp
using Microsoft.Agents.A365.Tooling.Services;

var toolService = serviceProvider.GetRequiredService<IMcpToolServerConfigurationService>();

// List all available tool servers for an agent
var toolServers = await toolService.ListToolServers(agentInstanceId, environmentId, authToken);

foreach (var server in toolServers)
{
    Console.WriteLine($"Tool Server: {server.mcpServerName}");
    Console.WriteLine($"  Server URL: {server.url}");
}
```

### Get MCP Client Tools

```csharp
// Get tools from a specific server
var mcpTools = await toolService.GetMcpClientTools(
    turnContext,
    server,
    environmentId,
    authToken);
```

### Service Registration

```csharp
builder.Services.AddSingleton<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();
```

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
