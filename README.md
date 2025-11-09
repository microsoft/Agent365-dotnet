# Microsoft Agents A365 SDK - C# /.NET

The Microsoft Agents A365 SDK extends the Microsoft 365 Agents SDK with enterprise-grade capabilities for building sophisticated agents. This SDK provides comprehensive tooling for observability, notifications, runtime utilities, and development tools that help developers create production-ready agents for platforms including M365, Teams, Copilot Studio, and Webchat.

The Microsoft Agents A365 SDK focuses on four core areas:

- **Observability**: Comprehensive tracing, caching, and monitoring capabilities for agent applications
- **Notifications**: Agent notification services and models for handling user notifications
- **Runtime**: Core utilities and extensions for agent runtime operations
- **Tooling**: Developer tools and utilities for building sophisticated agent applications

## Current Project State

This project is currently in active development. Packages are published to NuGet as they become available.

### Public Nuget feed

The best way to consume this SDK is via our NuGet packages found here: [nuget.org](https://www.nuget.org/packages?q=Microsoft.Agents.A365). All packages begin with **Microsoft.Agents.A365**.

### Nightly Nuget feed

Nightly builds are available through our development feed. These packages provide the latest features but may be less stable than official releases. Packages on this feed will have version numbers ending with **-alpha**.

- This feed is updated overnight (PT) whenever commits occur in our repo
- Packages on this feed are more up-to-date with the current repository state
- These packages are not necessarily stable and should be used for testing purposes

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

- **src/DevTools**: Microsoft Agents A365 DevTools - Development tools and code analyzers
- **src/Notification**: Microsoft Agents A365 Notifications - Agent notification services and models
- **src/Observability**: Microsoft Agents A365 Observability - Tracing, caching, and monitoring capabilities
  - Core: Core observability functionality
  - Extensions: Framework-specific extensions for various AI platforms
  - Hosting: ETW and hosting support
  - Runtime: Runtime observability services
- **src/Runtime**: Microsoft Agents A365 Runtime - Core runtime utilities and extensions
  - Core: Core runtime functionality
  - Extensions: Runtime extensions for various AI frameworks
- **src/Tooling**: Microsoft Agents A365 Tooling - Agent tooling and MCP integration
- **src/Tests**: Unit and integration tests

## Support

For issues, questions, or feedback:

- **Issues**: Please file issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- **Documentation**: See the [Microsoft Agents A365 Developer Documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/)
- **Security**: For security issues, please see [SECURITY.md](SECURITY.md)

## Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.

## Useful Links

### Microsoft 365 Agents SDK

The core SDK for building conversational AI agents for Microsoft 365 platforms.

- [Microsoft 365 Agents SDK](https://aka.ms/agents)
- [Agents-for-net Repository](https://github.com/Microsoft/Agents-for-net)
- [Agents-for-js Repository](https://github.com/Microsoft/Agents-for-js)
- [Agents-for-python Repository](https://github.com/Microsoft/Agents-for-python)
- [Official Agents Documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)

### Microsoft Agents A365 SDK

Enterprise-grade extensions for observability, notifications, runtime utilities, and developer tools.

- [Agent365-dotnet Repository](https://github.com/microsoft/Agent365-dotnet) - You are here
- [Agent365-python Repository](https://github.com/microsoft/Agent365-python)
- [Agent365-nodejs Repository](https://github.com/microsoft/Agent365-nodejs)
- [Agent365-Samples Repository](https://github.com/microsoft/Agent365-Samples)
- [Microsoft Agents A365 Developer Documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/)

### Additional Resources

- [.NET Documentation](https://learn.microsoft.com/en-us/dotnet/api/?view=m365-agents-sdk&preserve-view=true)

## Data Collection Notice

The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at <https://go.microsoft.com/fwlink/?LinkID=824704>. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
