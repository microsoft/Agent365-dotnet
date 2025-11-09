# Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel

Semantic Kernel integration extensions for Microsoft Agents A365 Tooling. This package provides seamless MCP tool server integration with Semantic Kernel-based agent applications.

## Overview

This extension package enables automatic registration of MCP tool servers as Semantic Kernel functions, allowing agents to discover and use external tools through Semantic Kernel's function calling capabilities.

## Features

- **Automatic Function Registration**: MCP tool servers are automatically registered as Kernel functions
- **Function Calling Support**: Full integration with Semantic Kernel's function calling behavior
- **Type-Safe Parameters**: Strongly-typed function parameters with automatic marshalling
- **Authentication Integration**: Seamless authentication flow for tool server access
- **Error Handling**: Comprehensive error handling and retry logic

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel
```

## Quick Start

### Basic Setup

```csharp
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Register tooling services
builder.Services.AddSingleton<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();
builder.Services.AddSingleton<IMcpToolRegistrationService, McpToolRegistrationService>();

var app = builder.Build();
```

### Register Tool Servers with Kernel

```csharp
public class ToolingAgent
{
    private readonly Kernel _kernel;
    private readonly IMcpToolRegistrationService _mcpToolRegistrationService;
    
    public ToolingAgent(
        Kernel kernel,
        IMcpToolRegistrationService mcpToolRegistrationService,
        UserAuthorization userAuthorization,
        ITurnContext turnContext,
        string environmentId)
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

### Configure Agent with Function Calling

```csharp
var agent = new ChatCompletionAgent
{
    Instructions = "You are a helpful assistant with access to external tools.",
    Name = "ToolingAgent",
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

## Advanced Usage

### Custom Authentication

```csharp
// Use custom auth token instead of agentic authentication
_mcpToolRegistrationService.AddToolServersToAgent(
    kernel,
    environmentId,
    userAuthorization,
    turnContext,
    authToken: customToken);
```

### Selective Tool Registration

MCP tools are automatically filtered to ensure tool names (server name + tool name) don't exceed 64 characters, as required by the Semantic Kernel framework.

```csharp
// All configured MCP tool servers are registered automatically
// Tools with names exceeding 64 characters are automatically filtered out
_mcpToolRegistrationService.AddToolServersToAgent(
    kernel,
    environmentId,
    userAuthorization,
    turnContext);
```

## Best Practices

1. **Enable Function Calling**: Always configure `FunctionChoiceBehavior.Auto()` for tool support
2. **Retain Argument Types**: Set `RetainArgumentTypes = true` for proper parameter handling
3. **Handle Authentication**: Ensure proper authentication tokens are provided
4. **Monitor Tool Execution**: Use observability features to track tool invocations
5. **Error Handling**: Implement proper error handling for tool failures

## Related Documentation

- [Tooling Module Overview](../../README.md)
- [Core Tooling Package](../../Core/README.md)
- [Agent Framework Extension](../AgentFramework/README.md)
- [Azure AI Foundry Extension](../AzureAIFoundry/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
