# Microsoft Agents A365 Tooling

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Tooling.svg?label=Core)](https://www.nuget.org/packages/Microsoft.Agents.A365.Tooling/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.svg?label=Semantic%20Kernel)](https://www.nuget.org/packages/Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.svg?label=Agent%20Framework)](https://www.nuget.org/packages/Microsoft.Agents.A365.Tooling.Extensions.AgentFramework/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry.svg?label=Azure%20AI%20Foundry)](https://www.nuget.org/packages/Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.Tooling.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Tooling/)

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

## Package Structure

The Tooling module provides MCP tool server integration through multiple packages:

### Core Package

- **[Microsoft.Agents.A365.Tooling](Core/README.md)** - Core MCP tool server configuration service and models

### Framework Extensions

- **[Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel](Extensions/SemanticKernel/README.md)** - Semantic Kernel integration with automatic plugin registration
- **[Microsoft.Agents.A365.Tooling.Extensions.AgentFramework](Extensions/AgentFramework/README.md)** - Agent Framework integration returning immutable AIAgent
- **[Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry](Extensions/AzureAIFoundry/README.md)** - Azure AI Foundry Persistent Agents integration

## Getting Started

This module enables MCP tool server discovery and registration. Choose the extension that matches your AI framework:

1. **For Semantic Kernel** - Tools are registered as kernel plugins with automatic 64-character name filtering. See [Semantic Kernel Extension](Extensions/SemanticKernel/README.md)
2. **For Agent Framework** - Returns new AIAgent instances with tools (immutable pattern). See [Agent Framework Extension](Extensions/AgentFramework/README.md)
3. **For Azure AI Foundry** - Updates Persistent Agents through Administration API. See [Azure AI Foundry Extension](Extensions/AzureAIFoundry/README.md)
4. **Core Service** - For custom integrations, use the core IMcpToolServerConfigurationService. See [Core Package](Core/README.md)

## Key Capabilities

### Automatic Tool Discovery

- List available MCP tool servers for a given environment
- Retrieve tool definitions and metadata
- Support for both agentic and custom authentication

### Framework-Specific Registration

- **Semantic Kernel**: Tools registered as kernel plugins with 64-character name limit
- **Agent Framework**: Returns new immutable AIAgent instances with tools included
- **Azure AI Foundry**: Updates Persistent Agents through Administration API

### Authentication Support

- Agentic authentication (default) - Automatic token handling for agent-to-agent scenarios
- Custom token authentication - Bring your own auth token for specialized scenarios

## Package Documentation

For detailed code examples, configuration, and usage patterns, refer to the individual package READMEs:

- [Core Package](Core/README.md) - IMcpToolServerConfigurationService interface and models
- [Semantic Kernel Extension](Extensions/SemanticKernel/README.md) - Complete Semantic Kernel integration examples
- [Agent Framework Extension](Extensions/AgentFramework/README.md) - Complete Agent Framework integration examples
- [Azure AI Foundry Extension](Extensions/AzureAIFoundry/README.md) - Complete Azure AI Foundry integration examples

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
