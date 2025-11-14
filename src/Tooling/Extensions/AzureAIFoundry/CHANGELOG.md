# Changelog

All notable changes to the Microsoft Agent 365 SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Microsoft.Agents.A365.Tooling.AzureFoundry** - Azure Foundry integration tooling for MCP server management
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


## [1.0.0] - 2025-01-16

### Added
- Initial release of Microsoft Agents 365 SDK
- OpenTelemetry integration for comprehensive telemetry and tracing
- `Agent 365` extension methods for `IHostApplicationBuilder` configuration
- `A365SpanProcessor` for custom span processing with agent-specific metadata
- Specialized tracing scopes:
  - `InvokeAgentScope` for tracking AI agent invocations
  - `ExecuteToolScope` for tracking tool executions
  - `A365OpenTelemetryScope` base class for extensible tracing
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
- Azure.AI.Agents.Persistent SDK for Azure Foundry integration
- ModelContextProtocol.Core for MCP client functionality
- Microsoft.Agents.Authentication.Msal for authentication
- Microsoft.Agents.Hosting.AspNetCore for ASP.NET Core integration
- OpenTelemetry
- Azure Monitor OpenTelemetry Exporter
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