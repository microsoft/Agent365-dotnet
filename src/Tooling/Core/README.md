# Microsoft.Agents.A365.Tooling

Core tooling functionality for MCP (Model Context Protocol) tool server management in Microsoft Agent 365 applications. This package provides the foundation for discovering, registering, and managing tool servers across different AI frameworks.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Tooling
```

## Tool server discovery

Tool discovery loads `ToolingManifest.json` when it exists, regardless of the environment name, and logs a warning when doing so. It searches the application base directory, current working directory, and entry assembly directory, in that order. If no manifest exists, discovery uses the tooling service.

To use the tooling service even when a manifest exists (including in Development), set `A365_USE_TOOLING_SERVICE=true` in your application's configuration or environment variables. Environment variables must be included in the application's `IConfiguration`. When unset or not `true`, an existing manifest remains the default.

## Documentation

For detailed usage information, configuration examples, and best practices, see the [Microsoft Agents 365 Tooling documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/tooling?tabs=dotnet).

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## Trademarks

*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.