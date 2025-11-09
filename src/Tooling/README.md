# Microsoft Agents A365 Tooling

The Microsoft Agents A365 Tooling module provides developer tools and utilities for building sophisticated agent applications with Model Context Protocol (MCP) tool server integration. This module simplifies the discovery and registration of tool servers with AI agent frameworks.

## Overview

The Tooling module enables developers to:

- Discover and list available MCP tool servers
- Automatically register tool servers with agent frameworks
- Integrate tools seamlessly with multiple AI frameworks
- Support both agentic authentication and custom authorization
- Manage tool server lifecycles and dependencies

## Features

- **Tool Server Discovery**: List and discover MCP tool servers available to agents
- **Automatic Registration**: Easy integration with AI frameworks
- **Authentication Support**: Both agentic authentication and custom token-based auth
- **Type-Safe Integration**: Strongly-typed tool registration with compile-time safety
- **Framework Extensions**: Ready-to-use extensions for multiple AI frameworks

## Installation

```bash
# Core tooling package
dotnet add package Microsoft.Agents.A365.Tooling

# For Semantic Kernel integration
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel

# For Agent Framework integration
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.AgentFramework

# For Azure AI Foundry integration
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry
```

## Package Structure

### Core Package

- **Microsoft.Agents.A365.Tooling** (`Core/`): Core tooling functionality for MCP tool server management

### Extensions

- **Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel** (`Extensions/SemanticKernel/`): Semantic Kernel integration for tool registration
- **Microsoft.Agents.A365.Tooling.Extensions.AgentFramework** (`Extensions/AgentFramework/`): Agent Framework integration
- **Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry** (`Extensions/AzureAIFoundry/`): Azure AI Foundry integration

## Quick Start

### Basic Tool Server Registration

1. **Register required services**:

   ```csharp
   using Microsoft.Agents.A365.Tooling;
   using Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel;
   
   builder.Services.AddSingleton<IMcpToolRegistrationService, McpToolRegistrationService>();
   ```

2. **Create agent with tool server support**:

   ```csharp
   public class ToolingAgent
   {
       private readonly Kernel _kernel;
       private readonly IMcpToolRegistrationService _mcpToolRegistrationService;
       
       public ToolingAgent(
           Kernel kernel, 
           IServiceProvider service, 
           IMcpToolRegistrationService mcpToolRegistrationService, 
           UserAuthorization userAuthorization, 
           ITurnContext turnContext)
       {
           _kernel = kernel;
           _mcpToolRegistrationService = mcpToolRegistrationService;
           
           // Register tool servers with agentic authentication
           _mcpToolRegistrationService.AddToolServersToAgent(
               kernel, 
               environmentId, 
               userAuthorization, 
               turnContext);
       }
   }
   ```

3. **Configure agent with tool support**:

   ```csharp
   // Define the agent with function calling enabled
   var agent = new ChatCompletionAgent
   {
       Instructions = AgentInstructions(),
       Name = AgentName,
       Kernel = _kernel,
       Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
       {
   #pragma warning disable SKEXP0001
           FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
               options: new() { RetainArgumentTypes = true }),
   #pragma warning restore SKEXP0001
           ResponseFormat = "json_object", 
       }),
   };
   ```

   > [!IMPORTANT]
   > The `RetainArgumentTypes = true` option is critical for proper tool parameter handling. Do not omit this configuration.

### Using Custom Authentication

```csharp
// Use custom auth token instead of agentic authentication
_mcpToolRegistrationService.AddToolServersToAgent(
    kernel, 
    environmentId, 
    userAuthorization, 
    turnContext, 
    authToken: customToken);
```

### Listing Available Tool Servers

```csharp
public class ToolServerDiscovery
{
    private readonly IMcpToolRegistrationService _toolService;
    
    public async Task<IEnumerable<ToolServerInfo>> ListAvailableToolsAsync()
    {
        // Get list of all available MCP tool servers
        var toolServers = await _toolService.ListToolServersAsync();
        
        foreach (var server in toolServers)
        {
            Console.WriteLine($"Tool Server: {server.Name}");
            Console.WriteLine($"  Description: {server.Description}");
            Console.WriteLine($"  Tools: {string.Join(", ", server.Tools)}");
        }
        
        return toolServers;
    }
}
```

## Advanced Usage

### Selective Tool Registration

```csharp
// Register only specific tool servers
var selectedServers = new[] { "weather-tools", "database-tools" };

_mcpToolRegistrationService.AddToolServersToAgent(
    kernel,
    environmentId,
    userAuthorization,
    turnContext,
    toolServerNames: selectedServers);
```

### Tool Server Health Monitoring

```csharp
// Monitor tool server health and availability
var healthStatus = await _mcpToolRegistrationService.CheckToolServerHealthAsync();

foreach (var status in healthStatus)
{
    if (!status.IsHealthy)
    {
        _logger.LogWarning($"Tool server {status.Name} is unhealthy: {status.Message}");
    }
}
```

## Framework Integration

Framework-specific integrations provide seamless tool server registration:

### Semantic Kernel

```csharp
// Tools are automatically registered as Kernel functions
await kernel.InvokeAsync("ToolName", new KernelArguments { ... });
```

### Agent Framework

```csharp
// Tools are available to all agents in the framework
agentGroup.RegisterToolServers(toolServers);
```

### Azure AI Foundry

```csharp
// Tools are registered with Azure AI Foundry endpoints
foundryClient.RegisterToolServers(toolServers, foundryEndpoint);
```

## Configuration

### appsettings.json

```json
{
  "ToolServer": {
    "DiscoveryEndpoint": "https://toolserver.example.com/api/discovery",
    "AuthenticationMode": "Agentic",
    "TimeoutSeconds": 30,
    "EnableCaching": true
  }
}
```

## Useful Links

### Microsoft Agents A365 SDK

- [Microsoft Agents A365 Runtime](../Runtime/README.md) - Core runtime utilities for agents
- [Microsoft Agents A365 Observability](../Observability/README.md) - Monitoring and tracing for tool executions
- [Microsoft Agents A365 Notifications](../Notification/README.md) - Agent notification services
- [Microsoft Agents A365 DevTools](../DevTools/README.md) - Code analyzers and development tools

### Documentation

- [Microsoft Agents A365 Developer Documentation](<https://learn.microsoft.com/en-us/microsoft-agent-365/developer/>)
- [Core Tooling Documentation](Core/README.md)
- [Semantic Kernel Extension](Extensions/SemanticKernel/README.md)
- [Agent Framework Extension](Extensions/AgentFramework/README.md)
- [Azure AI Foundry Extension](Extensions/AzureAIFoundry/README.md)

### Related Repositories

- [Agent365-python](<https://github.com/microsoft/Agent365-python>) - Python SDK for Microsoft Agents A365
- [Agent365-nodejs](<https://github.com/microsoft/Agent365-nodejs>) - Node.js SDK for Microsoft Agents A365
- [Agent365-Samples](<https://github.com/microsoft/Agent365-Samples>) - Sample applications and code examples

## Sample Applications

- **Semantic Kernel Multiturn**: Demonstrates tool server integration with multi-turn conversations

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
