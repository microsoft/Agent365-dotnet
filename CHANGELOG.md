# Changelog

All notable changes to the Microsoft Kairo SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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