# Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry

Azure AI Foundry integration extensions for Microsoft Agents 365 Tooling. This package provides seamless MCP tool server integration with Azure AI Foundry-based agent applications.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry
```

## Usage

### Register Tool Servers with Azure AI Foundry

```csharp
using Microsoft.Agents.A365.Tooling.Services;
using Azure.AI.Agents.Persistent;

// Register MCP tool servers with the Persistent Agent
// Note: Persistent Agents cannot be mutated after creation,
// so this updates the agent definition with MCP tool definitions
mcpToolRegistrationService.AddToolServersToAgent(
    agentClient,
    agentInstanceId,
    environmentId,
    userAuthorization,
    turnContext);
```

### Get Tool Definitions for Agent Creation

```csharp
// Get MCP tool definitions and resources for creating a new Persistent Agent
var (toolDefinitions, toolResources) = await mcpToolRegistrationService
    .GetMcpToolDefinitionsAndResourcesAsync(
        agentInstanceId,
        environmentId,
        authToken,
        turnContext);
```

> [!IMPORTANT]
> Persistent Agents cannot be mutated after creation; tool definitions are updated through the Administration API

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
