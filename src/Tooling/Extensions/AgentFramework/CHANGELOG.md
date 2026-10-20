# Changelog

All notable changes to the Microsoft Agent 365 SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-10-20

### Breaking Changes
- **Microsoft.Agents.A365.Tooling.Extensions.AgentFramework** - `authToken` no longer authenticates MCP servers
  - When `UserAuthorization` and `authHandlerName` aren't supplied, `authToken` is used only for MCP server discovery through the tooling gateway; MCP servers authenticate only with tokens from the `BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN` environment variables, and servers without one are skipped and logged, so the call can silently return no MCP tools
  - Affects `AddToolServersToAgent` and `GetMcpToolsAsync` when called without `UserAuthorization` / `authHandlerName`
  - **Migration:** pass `UserAuthorization` and `authHandlerName` so tokens are acquired through the agentic OBO flow, or set the `BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN` environment variables

### Changed
- **Microsoft.Agents.A365.Tooling.Extensions.AgentFramework** - MCP token provider chaining
  - `AddToolServersToAgent` and `GetMcpToolsAsync` now try tokens from environment variables (`BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN`) first and fall back to the agentic OBO flow when `UserAuthorization` and `authHandlerName` are supplied
  - MCP servers whose token acquisition fails are skipped and logged as a warning instead of failing registration for all servers

## [1.0.0] - 2026-04-30

### Changed
- **Microsoft.Agents.A365.Tooling.Extensions.AgentFramework** - V2 per-audience token support
  - `McpToolRegistrationService.AddToolServersToAgent` and `GetMcpToolsAsync` now instantiate `AgenticMcpTokenProvider` and use the V2-aware `EnumerateToolsFromServersAsync` overload, so each V2 MCP server receives its own audience-scoped Bearer token instead of the shared ATG token

### Added
- Initial release of Microsoft.Agents.A365.Tooling.Extensions.AgentFramework
- Agent Framework integration tooling for MCP server management
  - `IMcpToolRegistrationService` interface for managing MCP tool server registrations
  - `McpToolRegistrationService` implementation for Agent Framework scenarios
  - `AddToolServersToAgent` — discovers MCP servers, acquires per-server tokens, and returns a new `AIAgent` with all tools (existing + MCP) loaded
  - `GetMcpToolsAsync` — returns discovered MCP tools as `IList<AITool>` for manual agent composition
  - `SendChatHistoryAsync` — forwards conversation history to the real-time threat-protection endpoint; accepts `IEnumerable<ChatMessage>` or `ChatMessageStore`, with and without explicit `ToolOptions`
  - `AddMcpServices()` extension method for one-line DI registration
  - Support for both simple auth token and full `UserAuthorization` + `authHandlerName` context workflows
  - Environment-aware configuration: dev mode reads tokens from `BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN` environment variables; production uses agentic OBO flow
  - Comprehensive error handling and structured logging for MCP server operations

### Dependencies
- .NET 8.0 target framework
- Microsoft.Extensions.AI for `IChatClient`, `AIAgent`, and `AITool` abstractions
- Azure.AI.OpenAI for Azure OpenAI client integration
- Microsoft.Agents.AI for `AIAgent` creation
- ModelContextProtocol.Core for MCP client functionality
- Microsoft.Agents.Authentication.Msal for authentication
- Microsoft.Agents.Hosting.AspNetCore for hosting integration