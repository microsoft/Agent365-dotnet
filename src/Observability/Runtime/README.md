# Microsoft.Agents.A365.Observability.Runtime

The Runtime package provides runtime components for the Microsoft Agent 365 Observability SDK, including exporters, tracing utilities, DTOs, and scope management.

## Agent 365 Exporter Authentication

Agent 365 OBS export is S2S-only. The exporter always posts OTLP traces to
`https://{endpoint}/observabilityService/tenants/{tenantId}/otlp/agents/{agentId}/traces?api-version=1`;
the legacy `Agent365ExporterOptions.UseS2SEndpoint` switch is obsolete and ignored.

Configure either `TokenResolver` or `ContextualTokenResolver` to return the final app-only
OBS token for the exporting agent and tenant. The default app-only OBS scope is
`api://9b975845-388f-4429-889e-eab1ef63949c/.default`. The SDK never reads a delegated
request token, never performs OBO/user_fic authentication for OBS export, and never falls
back to `/observability` on 401, 403, or 404. The resolver is invoked once per tenant/agent
identity group in each export batch, so a batch that contains several identities invokes it
several times. Cache tokens per agent and tenant and refresh only near expiry.

Resolvers must validate the returned token before handing it to the exporter: accept
`idtyp=app`, or, when `idtyp` is absent, a non-empty `roles` array or a non-empty `oid`
equal to `sub`; reject any other `idtyp` and any token with an `scp` claim. Also verify the
audience is the Agent 365 OBS resource (`api://9b975845-388f-4429-889e-eab1ef63949c`) and
that the token is not expired.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Runtime
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