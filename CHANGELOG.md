# Changelog

All notable changes to the Microsoft Agent 365 SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-10-20

### Breaking Changes

- **OBS exports always use `/observabilityService`** — `Microsoft.Agents.A365.Observability.Runtime`
  now sends every Agent 365 telemetry export to the S2S OTLP route
  `/observabilityService/tenants/{tenantId}/otlp/agents/{agentId}/traces?api-version=1`.
  `Agent365ExporterOptions.UseS2SEndpoint` is obsolete and ignored, even when `false`;
  there is no fallback to `/observability`.
- **OBS export requires a configured app-only resolver** — `TokenResolver` and
  `ContextualTokenResolver` must return the final OBS app-only token for the exporting
  agent and tenant. Missing resolvers fail exporter construction; empty tokens or resolver
  failures fail the export batch before sending a request. Resolvers are invoked once per
  tenant/agent identity group in each export batch, so they should cache tokens per agent and
  tenant and refresh them near expiry. Workload OBO/MCP/Graph auth is
  unchanged. `EnvironmentUtils.GetObservabilityAuthenticationScope()` now returns the OBS
  `/.default` scope for app-only S2S export instead of the delegated
  `Agent365.Observability.OtelWrite` scope.
- **Delegated hosting OBS token acquisition is removed** —
  `AgenticTokenCache.RegisterObservability(..., AgenticTokenStruct, ...)` is obsolete with
  `error: true`, and `AgenticTokenStruct` construction is obsolete with `error: true`.
  Use `ObservabilityTokenResolver` and `AgenticTokenCache.RefreshObservabilityToken(...)`
  for app-only token acquisition. `AddAgenticTracingExporter` now registers
  `IExporterTokenCache<ObservabilityTokenResolver>` instead of
  `IExporterTokenCache<AgenticTokenStruct>`.
- **Structured gen-AI message payloads are plain JSON arrays** — `InputMessages` and
  `OutputMessages` now serialize as plain JSON arrays of messages, per the OpenTelemetry gen-AI
  semantic conventions, instead of a `{"version": ..., "messages": [...]}` wrapper. The public
  `MessageConstants` class and the `InputMessages.Version` / `OutputMessages.Version` properties
  were removed. `OutputMessage.FinishReason` is now non-nullable and defaults to `"stop"`.
- **`IA365EtwLogger` interface changes** — Added `LogApplyGuardrail(...)` and an optional
  `Exception? error` parameter on every existing `Log*` method. Custom implementations of
  `IA365EtwLogger` must be updated, and callers must be recompiled.
- **Tool-call arguments attribute renamed** — The `gen_ai.tool.arguments` span attribute is now
  `gen_ai.tool.call.arguments` (`OpenTelemetryConstants.GenAiToolArgumentsKey`), aligning with the
  OpenTelemetry gen-AI semantic conventions. Update any queries or dashboards that rely on the old
  attribute name.
- **`authToken` no longer authenticates MCP servers** — In the Semantic Kernel, Agent Framework,
  and Azure AI Foundry Tooling extensions, when `UserAuthorization` and `authHandlerName` aren't
  supplied, the caller's `authToken` is used only for MCP server discovery through the tooling
  gateway. MCP servers then authenticate only with tokens from the `BEARER_TOKEN_<SERVERNAME>` /
  `BEARER_TOKEN` environment variables. Servers without one are skipped and logged, so the
  "simple auth token" workflow can silently return no MCP tools. This affects Azure AI Foundry's
  sync `AddToolServersToAgent` and `GetMcpToolDefinitionsAndResourcesAsync(agentInstanceId,
  authToken, turnContext)`, which always take this path, and any other call that passes `null`
  authorization context. **Migration:** pass `UserAuthorization` and `authHandlerName` so tokens
  are acquired through the agentic OBO flow, or set the `BEARER_TOKEN_<SERVERNAME>` /
  `BEARER_TOKEN` environment variables.

### Added

- **Microsoft.Agents.A365.Observability.Runtime**
  - `ApplyGuardrailScope` for tracing security guardrail evaluations, with the `GuardrailDetails`
    and `GuardrailFinding` contracts and the `GuardrailDecisionType`, `GuardrailRiskSeverity`, and
    `GuardrailTargetType` types, plus the matching ETW data builder.
  - `AsyncContextualTokenResolver` (`Agent365ExporterOptions.ContextualTokenResolver`), with
    `TokenResolverContext` and `AgentIdentity`, so token resolvers receive the agent ID, tenant ID,
    and optional agentic user ID. When set, it takes precedence over `TokenResolver`.
  - OpenTelemetry-compliant span status (`Unset` / `Error`) and the `error.type` attribute on the
    ETW DTO logging path (`SpanStatus`, `SpanStatusCode`, `SpanStatusBuilder`).
  - ETW event payloads now include the `telemetry.sdk.name`, `telemetry.sdk.version`, and
    `telemetry.sdk.language` attributes, matching the OpenTelemetry span pipeline.
- **Microsoft.Agents.A365.Tooling** — `ToolOptions.McpClientInitializationTimeoutSeconds`
  (1–600 seconds) to configure the MCP client initialization timeout. When `null` (the default),
  the MCP SDK default is used.

### Changed

- **Microsoft.Agents.A365.Observability.Runtime** — Extra attributes passed to the DTO data
  builders can now set any key that the builder hasn't already set, instead of being limited to a
  fixed allowlist. Builder-set keys still can't be overridden.
- **Microsoft.Agents.A365.Tooling** — MCP token acquisition now chains token providers: tokens
  from environment variables (`BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN`) are tried first, with
  fallback to the agentic OBO flow when authorization context is supplied. MCP servers whose token
  acquisition fails are skipped and logged as a warning instead of failing tool registration for
  all servers. Applies to the Semantic Kernel, Agent Framework, and Azure AI Foundry extensions.
  See the `authToken` breaking change above.

### Fixed

- **Microsoft.Agents.A365.Observability.Runtime** — The `service.name` baggage value (set via
  `BaggageBuilder.OperationSource`) is now copied onto span attributes.
- **Microsoft.Agents.A365.Observability.Hosting** — The channel link (sub-channel) baggage value
  now falls back to `productContext` in the activity's `ChannelData` when
  `ChannelId.SubChannel` isn't set.

### Security

- Pinned the transitive `Microsoft.Bcl.Memory` dependency to 10.0.4 to address CVE-2026-26127
  (GHSA-73j8-2gch-69rq).

## [1.0.0] - 2026-04-30

General availability (GA) release of the Microsoft Agent 365 SDK.

### Added

- **Microsoft.Agents.A365.Tooling** - V1/V2 per-audience token support for MCP servers
  - `MCPServerConfig` extended with `audience`, `scope`, `publisher`, and `Headers` fields
  - `IMcpTokenProvider` interface for pluggable OAuth token acquisition
  - `AgenticMcpTokenProvider` — acquires per-audience tokens via the agentic OBO flow, with request-scoped token caching to avoid redundant exchanges
  - `McpToolServerConfigurationService.ListToolServersWithTokensAsync` — attaches per-server `Authorization` headers before tool connections are established; deduplicates token exchanges by scope across servers
  - `Utility.ResolveTokenScopeForServer` — resolves the correct OAuth scope for each server: when `audience` is present and not the ATG audience (V2), uses `{audience}/{scope}` if `scope` is set, otherwise `{audience}/.default`; when `audience` is absent or identifies ATG (V1), falls back to the shared ATG scope from configuration — `scope` alone (without a non-ATG audience) is intentionally ignored
  - `Constants.Authentication.AtgAppId` — shared ATG Application ID constant for V1 scope resolution
  - All three framework extensions (Semantic Kernel, Agent Framework, Azure AI Foundry) updated to use per-audience token provider, so V2 servers receive their own audience-scoped tokens

### Changed

- **Microsoft.Agents.A365.Tooling** — Tooling gateway endpoint updated to `/agents/v2/{id}/mcpServers`

## [0.3.0] - 2026-04-14

### Breaking Changes

- **New permission required: `Agent365.Observability.OtelWrite`** — The observability exporter now requires this scope as both a delegated and application permission on your agent blueprint. See [Upgrade Instructions](#upgrade-instructions-observability-permission-for-existing-agents-030-through-1x) below.

> **Note:** Starting with [2.0.0](#200---2026-10-20), observability export uses app-only (S2S) authentication only, and the delegated `Agent365.Observability.OtelWrite` permission is no longer used for export. The upgrade instructions below apply to versions 0.3.0 through 1.x.

### Changed

- **Microsoft.Agents.A365.Observability.Runtime** — Observability exporter URL paths now include an `/otlp/` segment.

### Upgrade Instructions: Observability Permission for Existing Agents (0.3.0 through 1.x)

Existing agent blueprints need `Agent365.Observability.OtelWrite` granted as both a **delegated permission** and an **application permission**. Choose either option below.

#### Option A — Agent 365 CLI (requires both config files)

Requires `a365.config.json` and `a365.generated.config.json` in your config directory, a Global Administrator account, and [Agent 365 CLI v1.1.139-preview](https://www.nuget.org/packages/Microsoft.Agents.A365.DevTools.Cli/1.1.139-preview) or later.

```
a365 setup admin --config-dir "<path-to-config-dir>"
```

This grants all missing permissions including the new Observability scopes.

#### Option B — Entra Portal (no config files required)

Requires Global Administrator access to the blueprint app registration.

1. Go to **Entra portal** > **App registrations** > select your Blueprint app
2. Go to **API permissions** > **Add a permission** > **APIs my organization uses** > search for `9b975845-388f-4429-889e-eab1ef63949c`
3. Select **Delegated permissions** > check `Agent365.Observability.OtelWrite` > **Add permissions**
4. Repeat step 2–3, this time select **Application permissions** > check `Agent365.Observability.OtelWrite` > **Add permissions**
5. Click **Grant admin consent** and confirm

Both `Agent365.Observability.OtelWrite` (Delegated) and `Agent365.Observability.OtelWrite` (Application) should show **Granted** status.

> **Note:** If your agent is autonomous, you only need the **Application permission**. The delegated permission is required for agents that authenticate via a user session.

## [0.1.0] - 2025-11-18

### Added

- Initial public preview of the Microsoft Agent 365 SDK packages: Observability, Notifications, Runtime, and Tooling.
