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

The Tooling module provides MCP tool server integration through multiple packages:

### Core Package

- **[Microsoft.Agents.A365.Tooling](Core/README.md)** - Core MCP tool server configuration service and models

### Framework Extensions

- **[Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel](Extensions/SemanticKernel/README.md)** - Semantic Kernel integration with automatic plugin registration
- **[Microsoft.Agents.A365.Tooling.Extensions.AgentFramework](Extensions/AgentFramework/README.md)** - Agent Framework integration returning immutable AIAgent
- **[Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry](Extensions/AzureAIFoundry/README.md)** - Azure AI Foundry Persistent Agents integration

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
