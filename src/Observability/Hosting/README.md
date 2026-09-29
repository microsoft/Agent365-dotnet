# Microsoft.Agents.A365.Observability.Hosting

The Hosting package provides ETW (Event Tracing for Windows) integration for the Microsoft Agent 365 Observability SDK. This package enables high-performance event tracing on Windows platforms for production monitoring scenarios.

## OBS Token Caching

OBS export is S2S-only. Hosting helpers must provide app-only tokens for the exporting
agent identity; delegated TurnContext/UserAuthorization exchange is no longer used for OBS.
`AgenticTokenCache.RegisterObservability(..., AgenticTokenStruct, ...)` and
`AgenticTokenStruct` construction are obsolete compile-time errors. Register or refresh an
`ObservabilityTokenResolver` instead, then wire `Agent365ExporterOptions.TokenResolver` to
`GetObservabilityToken`.

The resolver is responsible for acquiring and validating the final OBS token. It must reject
delegated `scp` tokens, accept app-only tokens (`idtyp=app`, or no `idtyp` with non-empty
`roles` or `oid == sub`), verify the OBS audience, and ensure the token is not expired.
Workload MCP/Graph/OBO authentication remains separate and unchanged.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Hosting
```

## Documentation

For detailed usage information, configuration examples, and best practices, see the [Microsoft Agents 365 Observability documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/observability?tabs=dotnet).

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## Trademarks

*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.