# Microsoft.Agents.A365.Tooling.Extensions.AgentFramework

Microsoft Agent Framework integration extensions for Microsoft Agents 365 Tooling. This package provides seamless MCP tool server integration with Agent Framework-based agent applications.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.AgentFramework
```

## Usage

### Register Tool Servers with Agent Framework

```csharp
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.AI;

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

> [!IMPORTANT]
> Agent Framework agents are immutable; new instances are created when adding tools

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
