# GitHub Copilot Instructions for Agent365-dotnet

The Microsoft Agent 365 SDK (C#/.NET) extends the Microsoft 365 Agents SDK with enterprise capabilities across four modules: **Observability**, **Notifications**, **Runtime**, and **Tooling**. Packages publish to NuGet under the `Microsoft.Agents.A365.*` prefix.

## Build, Test, and Lint

All commands run from the repository root unless noted. Requires the .NET 8.0.100 SDK (pinned in `src/global.json`).

```bash
# Build / test the whole solution
dotnet build src/Microsoft.Agents.A365.Sdk.sln
dotnet test  src/Microsoft.Agents.A365.Sdk.sln

# Build script (traversal build via src/dirs.proj) — preferred for full runs
./build/build.ps1                                   # Release build
./build/build.ps1 -Clean -Restore -Test             # clean rebuild + tests
./build/build.ps1 -Pack                              # produce NuGet packages

# Run all tests in ONE project
dotnet test src/Tests/Microsoft.Agents.A365.Runtime.Tests/Microsoft.Agents.A365.Runtime.Tests.csproj

# Run a SINGLE test or test class (xUnit primary; some MSTest)
dotnet test src/Microsoft.Agents.A365.Sdk.sln --filter "FullyQualifiedName~TenantContextHelperTests"
dotnet test src/Microsoft.Agents.A365.Sdk.sln --filter "Name=Extract_Returns_TenantId"

# Format check (enforced as a pre-commit hook)
dotnet format src/Microsoft.Agents.A365.Sdk.sln --verify-no-changes
```

CI (`.github/workflows/ci.yml`) builds and tests in **Release** with `--no-restore`/`--no-build`, then packs. Some Observability/Tooling tests read `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, and `AZURE_OPENAI_DEPLOYMENT` from the environment.

Pre-commit hooks (`.pre-commit-config.yaml`, install with `pip install pre-commit && pre-commit install`) run gitleaks (secret scanning), whitespace/EOL fixers, and `dotnet format`.

## Architecture

The SDK follows a consistent **Core + Extensions** pattern. Each module has a `Core`/`Runtime` package with base functionality and per-framework extension packages (SemanticKernel, AgentFramework, AzureAIFoundry, OpenAI). Source lives under `src/<Module>/`:

- **Runtime** (`src/Runtime/`) — multi-tenant context extraction (`TenantContextHelper` pulls tenant/worker IDs from `HttpContext` claims/headers/items) and the result pattern (`OperationResult` / `OperationError`).
- **Observability** (`src/Observability/`) — OpenTelemetry distributed tracing. Configured via a fluent `Builder` API; tracing uses disposable scope classes (`InvokeAgentScope`, `InferenceScope`, `ExecuteToolScope`) that auto-end spans on dispose. `BaggageMiddleware` seeds tenant/agent context into OTel baggage. A custom `Agent365Exporter` (gated by the `EnableAgent365Exporter` env var) exports spans.
- **Notifications** (`src/Notification/`) — event routing for M365 (Teams, email, Office) via `AgentNotification`; sub-channels like `agents:email`, `agents:word`, `agents:excel`, `agents:powerpoint`.
- **Tooling** (`src/Tooling/`) — Model Context Protocol (MCP) server discovery and tool registration via `IMcpToolServerConfigurationService`, with framework-specific registration extensions.

Build is a **traversal build**: `src/dirs.proj` (Microsoft.Build.Traversal) references each module's `dirs.proj`. Projects are auto-discovered, but the solution file is still maintained manually (see rules below). Versioning is automatic via Nerdbank.GitVersioning (nbgv) from git history — never hardcode versions.

Detailed design docs: `docs/design.md`; build internals: `build/BUILD.md`.

## Key Conventions

- **Central package management**: ALL NuGet versions live in `src/Directory.Packages.props`. Add a version there, then reference the package without a `Version` attribute in the `.csproj`. Never put versions in project files.
- **Common build props** (`src/Directory.Build.props`): `TreatWarningsAsErrors=true`, `Nullable=enable`, and `GenerateDocumentationFile=true` are global. Warnings — including invalid XML-doc `cref`s (CS1574) — fail the build. Write valid XML doc comments.
- **Target frameworks**: most packages target `net8.0`; some Runtime/Hosting packages target `netstandard2.0` (no implicit usings; `LangVersion` 8.0). Guard framework-specific code accordingly.
- **Observability export config — coordinated change required**: these three must stay in sync. If you change ONE, verify the other two:
  | Constant | Location |
  |---|---|
  | `ProdObservabilityScope` (via `GetObservabilityAuthenticationScope()`) | `src/Observability/Runtime/Common/EnvironmentUtils.cs` |
  | `DefaultEndpointHost` | `src/Observability/Runtime/Tracing/Exporters/Agent365ExporterOptions.cs` |
  | Export URL path (`BuildEndpointPath()`) | `src/Observability/Runtime/Tracing/Exporters/Agent365ExporterCore.cs` |
  Snapshot tests in `src/Tests/Microsoft.Agents.A365.Observability.Runtime.Tests/Tracing/Exporters/ExportConfigConsistencyTests.cs` catch accidental drift but not intentional-but-incomplete updates — confirm values are correct for the target environment.
- **Tests** mirror source under `src/Tests/` (e.g. `Microsoft.Agents.A365.Runtime.Tests`). xUnit is primary (some MSTest), with Moq for mocking and FluentAssertions for assertions.

## Coding agent rules
- Before committing changes, ensure that the solution `src/Microsoft.Agents.A365.Sdk.sln` builds: `dotnet build src/Microsoft.Agents.A365.Sdk.sln`
- Before committing changes, ensure that all tests pass: `dotnet test src/Microsoft.Agents.A365.Sdk.sln`
- When adding a new project, renaming a project, or removing a project (`.csproj` file), ensure that the solution file `src/Microsoft.Agents.A365.Sdk.sln` is updated accordingly.
- Ensure that each file added or modified has the correct copyright header:
  ```csharp
  // Copyright (c) Microsoft Corporation.
  // Licensed under the MIT License.
  ```

## Code Review Rules

### Rule 1: Check for "Kairo" Keyword
- **Description**: Scan code for any occurrence of the keyword "Kairo"
- **Action**: If "Kairo" is found in any code file:
  - Flag it for review
  - Suggest removal or replacement with appropriate terminology
  - Check if it's a legacy reference that needs to be updated
- **Files to check**: All `.cs`, `.csx` files in the repository

### Rule 2: Verify Copyright Headers
- **Description**: Ensure all C# files have proper Microsoft copyright headers
- **Action**: If a `.cs` file is missing a copyright header:
  - Add the Microsoft copyright header at the top of the file
  - The header should be placed before any using statements or code
  - Maintain proper formatting and spacing

#### Required Copyright Header Format
```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
```

### Implementation Guidelines

#### When Reviewing Code:
1. **Kairo Check**:
   - Search for case-insensitive matches of "Kairo"
   - Review context to determine if it's:
     - A class name
     - A namespace
     - A variable name
     - A comment reference
     - A using statement
     - A string literal
   - Suggest appropriate alternatives based on the context

2. **Header Check**:
   - Verify the first non-empty lines of C# files
   - If missing, prepend the copyright header
   - Ensure there's a blank line after the header before other content
   - Do not add headers to:
     - Auto-generated files (marked with `<auto-generated>` or `// <auto-generated />`)
     - Designer files (`.Designer.cs`)
     - Files with `#pragma warning disable` at the top for generated code

#### Example of Proper File Structure:
```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MyNamespace
{
    /// <summary>
    /// Class documentation
    /// </summary>
    public class MyClass
    {
        // Rest of the code...
    }
}
```

#### Example with File-Scoped Namespace (C# 10+):
```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace MyNamespace;

/// <summary>
/// Class documentation
/// </summary>
public class MyClass
{
    // Rest of the code...
}
```

#### Example with Top-Level Statements (C# 9+):
```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

var builder = WebApplication.CreateBuilder(args);

// Rest of the code...
```

### Auto-fix Behavior
When Copilot detects violations:
- **Kairo keyword**: Suggest inline replacement or flag for manual review
- **Missing header**: Automatically suggest adding the copyright header

### Exclusions
- Test files in `Tests/`, `test/`, or files ending with `.Tests.cs`, `.Test.cs` may have relaxed header requirements (but headers are still recommended)
- Auto-generated files (`.g.cs`, `.designer.cs`, files with auto-generated markers)
- Third-party code or vendored dependencies should not be modified
- Project files (`.csproj`, `.sln`), configuration files (`.json`, `.xml`, `.yaml`, `.md`) do not require copyright headers
- Build output directories (`bin/`, `obj/`)
- AssemblyInfo.cs files that are auto-generated

