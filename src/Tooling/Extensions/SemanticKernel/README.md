# Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel

Semantic Kernel integration extensions for Microsoft Agents A365 Tooling. This package provides seamless MCP tool server integration with Semantic Kernel-based agent applications.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel
```

## Usage

### Register Tool Servers with Kernel

```csharp
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.SemanticKernel;

// Register tool servers with agentic authentication
_mcpToolRegistrationService.AddToolServersToAgent(
    kernel,
    environmentId,
    userAuthorization,
    turnContext);
```

### Configure Agent with Function Calling

```csharp
var agent = new ChatCompletionAgent
{
    Instructions = "You are a helpful assistant with access to external tools.",
    Kernel = _kernel,
    Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
    {
#pragma warning disable SKEXP0001
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
            options: new() { RetainArgumentTypes = true }),
#pragma warning restore SKEXP0001
    }),
};
```

> [!IMPORTANT]
> The `RetainArgumentTypes = true` option is critical for proper tool parameter handling

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
