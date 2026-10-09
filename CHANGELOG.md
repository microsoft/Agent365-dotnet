# Changelog

All notable changes to the Microsoft Kairo SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0]

### Breaking Changes

- **New permission required: `Agent365.Observability.OtelWrite`** — The observability exporter now requires this scope as both a delegated and application permission on your agent blueprint. See [Upgrade Instructions](#upgrade-instructions-observability-permission-for-existing-agents) below.

---

### Upgrade Instructions: Observability Permission for Existing Agents

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

---

## [Unreleased]

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

### Added
- **Microsoft.Agents.A365.Tooling** - Microsoft Defender for AI real-time protection client
  - `DefenderRtpClient.EvaluateHookContextAsync` sends an agent-hooks/0.1 context to the Defender
    prevention endpoint (`POST .../v1/protection/evaluate`) at the four points Defender evaluates
    (`input`, `pre_tool_call`, `post_tool_call`, `output`) and returns its verdict (`deny` and
    `transform` block). A copy of the context is fitted to Defender's request validation; the host's context is
    not modified. One deadline (`A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS`) covers token acquisition and the
    request, and the endpoint and token authority must be absolute HTTPS URLs.
  - Calls carry the agent identity's own app-only token for the Defender API
    (`api://86a21212-634e-4553-b3d6-e477e4c9d9ec`, role `RealtimeProtection.Evaluate.All`), resolved by a
    `DefenderRtpTokenResolver`, cached per agent and tenant and refreshed in the background before it expires;
    `DefenderRtpTokenResolvers.FromAgenticConnection` uses the agent's connection (`IAgenticTokenProvider`), the
    same authority as Observability S2S export.
  - Every call sends a unique `x-ms-correlation-id`. Failures follow `A365_DEFENDER_RTP_FAIL_MODE` (`open` or
    `closed`; any other value is rejected), and a `400` reports the failed validation rule. Each string is clamped
    to `A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS`, and the copy carries at most four times that in all: the content
    under decision first, the oldest history dropped first. When the content under decision, or at a tool point the
    called tool's declaration, had to be cut, Defender's deny stands and its allow follows the fail mode
    (`DefenderRtpEvaluationResult.Truncated`). Lone surrogates are sent as U+FFFD.
- **Microsoft.Agents.A365.Tooling.Extensions.AgentHooks** (new, preview) - `A365DefenderInterceptor`, an
  agent-hooks interceptor (`ResponsibleAI.AgentHooks` 0.1.0-beta.1) for Defender, and
  `A365AgentHooks.CreateProtectionEmitter` (`parallel/strictest`). When no agent identity is resolved, or resolving
  it fails, the interceptor follows the fail mode.
- **Microsoft.Agents.A365.Tooling** - Microsoft Purview data loss prevention (DLP) and audit client
  - `PurviewDlpClient.EvaluateAsync` sends a text to the Microsoft Graph `processContent` API
    (`POST {base}/me/dataSecurityAndGovernance/processContent`) as `uploadText` (content entering the agent) or
    `downloadText` (its reply): one conversation entry with the agent, the session (`correlationId`) and its
    sequence, scoped to the agent's application (the blueprint id by default). A policy action whose
    `restrictionAction` is `block`, or whose `action` is `blockAccess`, blocks; other actions allow and are counted.
    Each call sends a new `client-request-id`, which also identifies its content entry and is returned as
    `PurviewDlpEvaluationResult.CorrelationId`.
  - Tokens come from a `PurviewDlpTokenResolver`: `PurviewDlpTokenResolvers.FromAgenticUser` uses the agent's
    connection (`IAgenticTokenProvider.GetAgenticUserTokenAsync`) for the agentic user's delegated Microsoft Graph
    token (`Content.Process.User`), evaluates as `/me`, and caches the token per tenant, agent, agentic user and scope
    until it expires (refreshed in the background before it does; failures never cached); `FromAccessTokenProvider`
    takes a host-supplied token, never cached, for `/me` or for a given user (`/users/{id}`).
  - Configured with `ENABLE_A365_PURVIEW_DLP`, `A365_PURVIEW_DLP_GRAPH_BASE_URL` (default
    `https://graph.microsoft.com/v1.0`, absolute HTTPS only), `A365_PURVIEW_DLP_AUTHENTICATION_SCOPE`,
    `A365_PURVIEW_DLP_FAIL_MODE` (`open` or `closed`), `A365_PURVIEW_DLP_TIMEOUT_MILLISECONDS` (one deadline for token
    acquisition and the request), `A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS` (default 100000) and
    `A365_PURVIEW_DLP_RESPONSE_MODE` (`audit` or `enforce`); any other value is rejected. Failures, and processing
    errors Graph reports inline, follow the fail mode, recording only the exception type or status, never a response
    body. Longer text is cut and sent with `isTruncated`: Purview's block stands and its allow follows the fail mode.
    Redirects are not followed, and lone surrogates are sent as U+FFFD.
- **Microsoft.Agents.A365.Tooling.Extensions.AgentHooks** - `A365PurviewInterceptor` (`AddA365Purview`), an
  agent-hooks interceptor for Purview DLP: the user's message at `input` is evaluated as `uploadText` and a block
  denies it (`purview:block`); the reply at `output` is evaluated as `downloadText`, for audit in the background by
  default or waiting for the verdict with `A365_PURVIEW_DLP_RESPONSE_MODE=enforce`. Structured content is read only up
  to `A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS`. Missing verdicts follow the fail mode (`purview:unverified` or
  `runtime_error:purview_unverified`). `A365AgentHooks.CreateProtectionEmitter` takes optional `PurviewDlpOptions`,
  so Defender and Purview compose on one emitter whose interceptor timeout exceeds the longer of their timeouts
  (Purview's counts only when Purview DLP is enabled).
- **Microsoft.Agents.A365.Tooling** - V1/V2 per-audience token support for MCP servers
  - `MCPServerConfig` extended with `audience`, `scope`, `publisher`, and `Headers` fields
  - `IMcpTokenProvider` interface for pluggable OAuth token acquisition
  - `AgenticMcpTokenProvider` — acquires per-audience tokens via the agentic OBO flow, with request-scoped token caching to avoid redundant exchanges
  - `McpToolServerConfigurationService.ListToolServersWithTokensAsync` — attaches per-server `Authorization` headers before tool connections are established; deduplicates token exchanges by scope across servers
  - `Utility.ResolveTokenScopeForServer` — resolves the correct OAuth scope for each server: when `audience` is present and not the ATG audience (V2), uses `{audience}/{scope}` if `scope` is set, otherwise `{audience}/.default`; when `audience` is absent or identifies ATG (V1), falls back to the shared ATG scope from configuration — `scope` alone (without a non-ATG audience) is intentionally ignored
  - `Constants.Authentication.AtgAppId` — shared ATG Application ID constant for V1 scope resolution
  - All three framework extensions (Semantic Kernel, Agent Framework, Azure AI Foundry) updated to use per-audience token provider, so V2 servers receive their own audience-scoped tokens
- **Microsoft.Kairo.Sdk.DevTools.Analyzer.SemanticKernel** - Comprehensive Roslyn analyzer package for enforcing Agent365 governance patterns
  - 6 diagnostic analyzers (A365SK0001-A365SK0006) for multi-tenant governance enforcement
  - `KernelDirectAccessAnalyzer` - Prevents direct Kernel injection, enforces IKernelProvider pattern
  - `KernelRetrievalBeforeBuildAnalyzer` - Ensures proper DI container lifecycle management
  - `TenantWorkerIdAccessAnalyzer` - Enforces centralized tenant context access via TenantContextHelper
  - `ChatCompletionServiceRegistrationAnalyzer` - Ensures governance-approved service registration
  - `GovernanceEnforcementInEndpointsAnalyzer` - Validates API endpoints have governance enforcement
  - `UnsafePluginImportAnalyzer` - Prevents plugin import exceptions through safe import patterns
  - Automated code fix providers for most analyzers with IDE integration
  - Centralized constants system eliminating hardcoded strings
  - Build-time governance enforcement with real-time IDE feedback
  - Comprehensive test suite with integration testing and metadata validation
- **Agent365Sdk.AspNetCore** - ASP.NET Core helpers for governance
  - `TenantContextHelper` for centralized tenant/worker ID extraction
- **Agent365Sdk.SemanticKernel** - Semantic Kernel governance providers
  - `IKernelProvider` interface for tenant-aware kernel access
  - `KernelProvider` implementation with governance compliance
  - `IGovernanceDelegateFactory` for standardized governance patterns

### Changed
- **Microsoft.Agents.A365.Tooling** — Tooling gateway endpoint updated to `/agents/v2/{id}/mcpServers`


## [1.0.0] - 2025-01-16

### Added
- Initial release of Microsoft Kairo SDK
- OpenTelemetry integration for comprehensive telemetry and tracing
- `Kairo` extension methods for `IHostApplicationBuilder` configuration
- `KairoSpanProcessor` for custom span processing with agent-specific metadata
- Specialized tracing scopes:
  - `InvokeAgentScope` for tracking AI agent invocations
  - `ExecuteToolScope` for tracking tool executions
  - `KairoOpenTelemetryScope` base class for extensible tracing
- Support for Azure Monitor integration via connection string configuration
- Built-in instrumentation for:
  - HTTP client requests
  - ASP.NET Core applications
  - Azure AI Inference operations
  - Microsoft Semantic Kernel operations
- Comprehensive telemetry constants and standardized attribute keys
- Agent and conversation tracking with telemetry metadata
- Tool execution monitoring with detailed trace information

### Dependencies
- .NET 8.0 target framework
- OpenTelemetry 1.12.0
- Azure Monitor OpenTelemetry Exporter 1.4.0
- OpenTelemetry instrumentation packages for HTTP, ASP.NET Core, and Runtime

### Documentation
- Complete README with installation and usage instructions
- Code examples for common scenarios
- API documentation via XML comments

## [1.0.0-preview] - 2025-01-15

### Added
- Preview release with core functionality
- Basic OpenTelemetry setup and configuration
- Initial tracing scope implementations