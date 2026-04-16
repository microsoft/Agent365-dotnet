# Changelog

All notable changes to the Microsoft Agent 365 SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel** - V2 per-audience token support
  - `McpToolRegistrationService.AddToolServersToAgentAsync` now instantiates `AgenticMcpTokenProvider` and uses the V2-aware `EnumerateToolsFromServersAsync` overload, so each V2 MCP server receives its own audience-scoped Bearer token instead of the shared ATG token
  - OBO token acquisition is deferred until after the dev-mode check; in `Development` environments the `DevMcpTokenProvider` supplies tokens from environment variables (`BEARER_TOKEN_<SERVERNAME>` / `BEARER_TOKEN`) without requiring a working auth setup

### Added
- **Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel** - Semantic Kernel integration tooling for MCP server management
  - `IMcpToolRegistrationService` interface for managing MCP tool server registrations
  - `McpToolRegistrationService` implementation for Semantic Kernel scenarios
  - Support for both simple auth token and full authentication context workflows
  - MCP tool discovery and registration as Kernel plugins
  - Authentication handlers for secure MCP server communication
  - Environment-aware SSL certificate validation for development scenarios
  - Comprehensive error handling and logging for MCP server operations


## [1.0.0] - 2025-01-16

### Added
- Initial release of Microsoft Agent 365 SDK
- Semantic Kernel plugin integration via MCP tool discovery
- `McpToolRegistrationService` for registering MCP tools as Kernel plugins
- Support for `AddToolServersToAgentAsync` with `UserAuthorization` and turn context
- Tool name length enforcement (64-character limit per Semantic Kernel requirement)
- Chat history forwarding via `SendChatHistoryAsync` with `ChatHistory` conversion
- Integration with `Microsoft.SemanticKernel` and `Microsoft.SemanticKernel.ChatCompletion`

### Dependencies
- .NET 8.0 target framework
- Microsoft.SemanticKernel for Semantic Kernel orchestration
- ModelContextProtocol.Core for MCP client functionality
- Microsoft.Agents.Authentication.Msal for authentication
- Microsoft.Agents.Hosting.AspNetCore for hosting integration
