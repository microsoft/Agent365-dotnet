# Microsoft Agent 365 SDK - C# /.NET

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Observability.svg?label=NuGet&logo=nuget)](https://www.nuget.org/packages?q=Microsoft.Agents.A365)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.Observability.svg?label=Downloads&logo=nuget)](https://www.nuget.org/packages?q=Microsoft.Agents.A365)
[![Build Status](https://img.shields.io/github/actions/workflow/status/microsoft/Agent365-dotnet/build.yml?branch=main&label=Build&logo=github)](https://github.com/microsoft/Agent365-dotnet/actions)
[![License](https://img.shields.io/github/license/microsoft/Agent365-dotnet?label=License)](LICENSE.md)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Contributors](https://img.shields.io/github/contributors/microsoft/Agent365-dotnet?label=Contributors&logo=github)](https://github.com/microsoft/Agent365-dotnet/graphs/contributors)

> #### Note:
> Use the information in this README to contribute to this open-source project. To learn about using this SDK in your projects, refer to the [Microsoft Agent 365 Developer documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/).

The Microsoft Agent 365 SDK extends the Microsoft 365 Agents SDK with enterprise-grade capabilities for building sophisticated agents. This SDK provides comprehensive tooling for observability, notifications, runtime utilities, and development tools that help developers create production-ready agents for platforms including M365, Teams, Copilot Studio, and Webchat.

The Microsoft Agent 365 SDK focuses on four core areas:

- **Observability**: Comprehensive tracing, caching, and monitoring capabilities for agent applications
- **Notifications**: Agent notification services and models for handling user notifications
- **Runtime**: Core utilities and extensions for agent runtime operations
- **Tooling**: Developer tools and utilities for building sophisticated agent applications

## Current Project State

This project is currently in active development. Packages are published to NuGet as they become available.

### Public Nuget feed

The best way to consume this SDK is via our NuGet packages found here: [nuget.org](https://www.nuget.org/packages?q=Microsoft.Agents.A365). All packages begin with **Microsoft.Agents.A365**.

## Working with this codebase

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 (recommended) or Visual Studio Code
- Git

### Building the project

1. Clone the repository:

   ```bash
   git clone https://github.com/microsoft/Agent365-dotnet.git
   cd Agent365-dotnet
   ```

2. Build the solution:

   ```bash
   dotnet build src/Microsoft.Agents.A365.Sdk.sln
   ```

3. Run tests:

   ```bash
   dotnet test src/Microsoft.Agents.A365.Sdk.sln
   ```

For more detailed build instructions, see the [build documentation](build/BUILD.md).

## Project Structure

- **src/DevTools**: Microsoft Agent 365 Developer Tools - Development tools and code analyzers
- **src/Notification**: Microsoft Agent 365 Notifications - Agent notification services and models
- **src/Observability**: Microsoft Agent 365 Observability - Tracing, caching, and monitoring capabilities
  - Core: Core observability functionality
  - Extensions: Framework-specific extensions for various AI platforms
  - Hosting: ETW and hosting support
  - Runtime: Runtime observability services
- **src/Runtime**: Microsoft Agent 365 Runtime - Core runtime utilities and extensions
  - Core: Core runtime functionality
  - Extensions: Runtime extensions for various AI frameworks
- **src/Tooling**: Microsoft Agent 365 Tooling - Agent tooling and MCP integration
- **src/Tests**: Unit and integration tests

## Support

For issues, questions, or feedback:

- **Issues**: Please file issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- **Documentation**: See the [Microsoft Agents 365 Developer documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/)
- **Security**: For security issues, please see [SECURITY.md](SECURITY.md)

## Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.

## Useful Links

### Microsoft 365 Agents SDK

The core SDK for building conversational AI agents for Microsoft 365 platforms.

- [Microsoft 365 Agents SDK - C# /.NET repository](https://github.com/Microsoft/Agents-for-net)
- [Microsoft 365 Agents SDK - NodeJS /TypeScript repository](https://github.com/Microsoft/Agents-for-js)
- [Microsoft 365 Agents SDK - Python repository](https://github.com/Microsoft/Agents-for-python)
- [Microsoft 365 Agents documentation](https://learn.microsoft.com/microsoft-365/agents-sdk/)

### Microsoft Agent 365 SDK

Enterprise-grade extensions for observability, notifications, runtime utilities, and developer tools.

- [Microsoft Agent 365 SDK - C# /.NET repository](https://github.com/microsoft/Agent365-dotnet) - You are here
- [Microsoft Agent 365 SDK - Python repository](https://github.com/microsoft/Agent365-python) 
- [Microsoft Agent 365 SDK - Node.js/TypeScript repository](https://github.com/microsoft/Agent365-nodejs)
- [Microsoft Agent 365 SDK Samples repository](https://github.com/microsoft/Agent365-Samples)
- [Microsoft Agent 365 developer documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/)

### Additional Resources

[.NET documentation](https://learn.microsoft.com/dotnet/api/?view=m365-agents-sdk&preserve-view=true)

## Trademarks

*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](LICENSE.md) file for details.
