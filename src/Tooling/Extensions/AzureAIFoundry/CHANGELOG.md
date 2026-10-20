# Changelog

All notable changes to the Microsoft Agent 365 SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-10-20

### Breaking Changes
- **Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry** - `authToken` no longer authenticates MCP servers
  - When `UserAuthorization` and `authHandlerName` aren't supplied, `authToken` is used only for MCP server discovery through the tooling gateway; MCP servers authenticate only with tokens from the `BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN` environment variables, and servers without one are skipped and logged, so the call can silently return no MCP tools
  - Affects the sync `AddToolServersToAgent` and `GetMcpToolDefinitionsAndResourcesAsync(agentInstanceId, authToken, turnContext)`, which always take this path, and `AddToolServersToAgentAsync` / `GetMcpToolDefinitionsAndResourcesAsync` when called without `UserAuthorization` / `authHandlerName`
  - **Migration:** pass `UserAuthorization` and `authHandlerName` so tokens are acquired through the agentic OBO flow, or set the `BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN` environment variables

### Changed
- **Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry** - MCP token provider chaining
  - `AddToolServersToAgentAsync` now tries tokens from environment variables (`BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN`) first and falls back to the agentic OBO flow when `UserAuthorization` and `authHandlerName` are supplied
  - MCP servers whose token acquisition fails are skipped and logged as a warning instead of failing registration for all servers

## [1.0.0] - 2026-04-30

### Changed
- **Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry** - V2 per-audience token support
  - `McpToolRegistrationService.AddToolServersToAgentAsync` now instantiates `AgenticMcpTokenProvider` and uses the V2-aware `GetMcpToolDefinitionsAndResourcesAsync` overload; each V2 MCP server's `MCPToolResource` is populated with its audience-scoped Bearer token instead of the shared ATG token

### Added
- Initial release of Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry
- Azure AI Foundry integration tooling for MCP server management
  - `IMcpToolRegistrationService` interface for managing MCP tool server registrations
  - `McpToolRegistrationService` implementation for Foundry (Persistent Agents) scenarios
  - Support for both simple auth token and full authentication context workflows
  - MCP tool discovery and validation with live server connectivity testing
  - Authentication handlers for secure MCP server communication:
    - `BearerTokenHandler` for Bearer token authentication
    - `HttpLoggingHandler` for HTTP request/response logging
  - Integration with Azure.AI.Agents.Persistent for agent tool management
  - Environment-aware SSL certificate validation for development scenarios
  - Comprehensive error handling and logging for MCP server operations
  - Support for MCP tool definitions and tool resources management

### Dependencies
- .NET 8.0 target framework
- Azure.AI.Agents.Persistent SDK for Azure AI Foundry integration
- ModelContextProtocol.Core for MCP client functionality
- Microsoft.Agents.Authentication.Msal for authentication
- Microsoft.Agents.Hosting.AspNetCore for ASP.NET Core integration
