<!--
================================================================================
SYNC IMPACT REPORT
================================================================================
Version change: 0.0.0 → 1.0.0 (Initial ratification)

Modified principles: N/A (initial version)

Added sections:
  - Core Principles (6 principles)
  - Architecture Constraints
  - Development Workflow
  - Governance

Removed sections: N/A (initial version)

Templates requiring updates:
  - .specify/templates/plan-template.md: ✅ Compatible (Constitution Check section exists)
  - .specify/templates/spec-template.md: ✅ Compatible (No principle-specific references)
  - .specify/templates/tasks-template.md: ✅ Compatible (No principle-specific references)

Follow-up TODOs: None
================================================================================
-->

# Microsoft Agent 365 SDK Constitution

## Core Principles

### I. Core + Extensions Architecture
Every capability MUST follow the Core + Extensions pattern. Core packages provide framework-agnostic base functionality. Extension packages integrate with specific frameworks (Semantic Kernel, Agent Framework, Azure AI Foundry, OpenAI). This separation ensures loose coupling, independent versioning, and allows consumers to adopt only what they need. New features MUST NOT add framework-specific code to core packages.

### II. Multi-Tenant First
All components MUST support multi-tenant scenarios from the ground up. Tenant context extraction via `TenantContextHelper` MUST be used consistently for all tenant/worker ID retrieval. Hard-coded tenant identifiers are forbidden. Tenant context MUST propagate through OpenTelemetry baggage in distributed tracing scenarios.

### III. Strict Code Quality (NON-NEGOTIABLE)
- All compiler warnings MUST be treated as errors (`TreatWarningsAsErrors=true`)
- Nullable reference types MUST be enabled (`Nullable=enable`)
- XML documentation MUST be generated for all public APIs
- Every C# source file MUST include the Microsoft copyright header
- The term "Kairo" MUST NOT appear in any code (legacy terminology)

### IV. Standardized Error Handling
All service operations that can fail MUST use the `OperationResult` pattern. Return `OperationResult.Success` for success cases. Return `OperationResult.Failed(OperationError)` with appropriate HTTP status codes for failures. Exceptions MUST NOT be thrown for expected business logic failures.

### V. Disposable Scope Pattern
All tracing scopes (`InvokeAgentScope`, `InferenceScope`, `ExecuteToolScope`) MUST implement `IDisposable`. Consumers MUST use `using` statements or blocks to ensure proper span lifecycle management. Spans are automatically ended on dispose. New scope types MUST follow this pattern.

### VI. Test Coverage Required
Every package MUST have a corresponding test project in `src/Tests/`. Tests MUST cover happy path scenarios, error conditions, edge cases, and framework-specific extension behavior. Pre-commit validation MUST pass: solution builds without warnings and all tests pass.

## Architecture Constraints

### Target Frameworks
- **Primary packages**: `net8.0`
- **Runtime/Hosting packages**: `netstandard2.0` (for broader ASP.NET Core compatibility)
- **Test projects**: `net8.0`

### Package Management
- All NuGet package versions MUST be defined in `src/Directory.Packages.props` (centralized)
- Versions MUST NOT be specified directly in `.csproj` files
- Semantic versioning is managed by Nerdbank.GitVersioning (nbgv)

### Dependency Principles
- Core packages SHOULD have minimal external dependencies
- Extension packages depend on their target framework
- Circular dependencies between packages are forbidden
- Composition SHOULD be preferred over inheritance

### Package Naming Convention
All packages MUST follow: `Microsoft.Agents.A365.<Module>[.Extensions.<Framework>]`

## Development Workflow

### Pre-Commit Validation
Before committing any changes, developers MUST verify:
1. Solution builds: `dotnet build src/Microsoft.Agents.A365.Sdk.sln`
2. All tests pass: `dotnet test src/Microsoft.Agents.A365.Sdk.sln`
3. No new compiler warnings (treated as errors)

### Design Patterns
| Pattern | When to Use | Example |
|---------|-------------|---------|
| Builder | Complex configuration | `Builder.WithAgentFramework().Build()` |
| Disposable | Resource/span lifecycle | `using var scope = InvokeAgentScope.Start(...)` |
| Result | Operation outcomes | `OperationResult.Success`, `OperationResult.Failed(...)` |
| Extension Methods | Framework integration | `app.OnAgenticEmailNotification(...)` |

### Interface-First Design
- Interfaces MUST be defined for all services (e.g., `IMcpToolServerConfigurationService`)
- Implementations SHOULD be internal where possible
- Registration via dependency injection with appropriate lifetimes

## Governance

This constitution governs all development on the Microsoft Agent 365 SDK. All pull requests and code reviews MUST verify compliance with these principles. Complexity MUST be justified against the Core + Extensions architecture.

**Amendment Process**:
1. Propose changes via documented PR
2. Obtain team approval
3. Update constitution version per semantic versioning
4. Update dependent templates if principle changes affect them

**Versioning Policy**:
- MAJOR: Backward-incompatible governance/principle removals or redefinitions
- MINOR: New principle/section added or materially expanded guidance
- PATCH: Clarifications, wording, typo fixes, non-semantic refinements

**Compliance**: Use `CLAUDE.md` for runtime development guidance and build commands.

**Version**: 1.0.0 | **Ratified**: 2026-01-23 | **Last Amended**: 2026-01-23
