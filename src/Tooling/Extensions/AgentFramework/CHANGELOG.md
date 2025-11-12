# Changelog

All notable changes to the Microsoft Agent 365 SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Microsoft.Agents.A365.Tooling.Extensions.AgentFramework** - Agent Framework integration tooling for MCP server management
  - `IMcpToolRegistrationService` interface for managing MCP tool server registrations
  - `McpToolRegistrationService` implementation for Agent Framework scenarios
  - Support for both simple auth token and full authentication context workflows
  - MCP tool discovery and validation with live server connectivity testing
  - Authentication handlers for secure MCP server communication:
    - `BearerTokenHandler` for Bearer token authentication
    - `HttpLoggingHandler` for HTTP request/response logging
  - Integration with Microsoft.Extensions.AI for agent tool management
  - Environment-aware SSL certificate validation for development scenarios
  - Comprehensive error handling and logging for MCP server operations
  - Support for MCP tool definitions and AIAgent integration


## [1.0.0] - 2025-01-16

### Added
- Initial release of Microsoft Agent 365 SDK
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
- Azure.AI.OpenAI for Azure OpenAI client integration
- Microsoft.Extensions.AI for AI abstraction layer
- Microsoft.Extensions.AI.OpenAI for OpenAI integration
- Microsoft.Extensions.AI.AzureAIInference for Azure AI integration
- Microsoft.Agents.AI for agent functionality
- ModelContextProtocol.Core for MCP client functionality
- Microsoft.Agents.Authentication.Msal for authentication
- Microsoft.Agents.Hosting.AspNetCore for hosting integration