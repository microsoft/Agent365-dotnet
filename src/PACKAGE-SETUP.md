# Microsoft Agent 365 SDK - Package Setup Guide

## Maintained packages

This repository produces six .NET 8 packages:

- `Microsoft.Agents.A365.Runtime`
- `Microsoft.Agents.A365.Notifications`
- `Microsoft.Agents.A365.Tooling`
- `Microsoft.Agents.A365.Tooling.Extensions.AgentFramework`
- `Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel`
- `Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry`

Observability packages have been retired here. Applications should migrate to `Microsoft.OpenTelemetry` using the [migration guide](../docs/observability-migration.md), which also documents owner-operated NuGet deprecation and external publishing controls.

## Build and package

Use the SDK selected by `src/global.json` and PowerShell for the build script. Run these commands from the repository root:

```powershell
dotnet restore src\Microsoft.Agents.A365.Sdk.sln
dotnet build src\Microsoft.Agents.A365.Sdk.sln --configuration Release --no-restore
dotnet test src\Microsoft.Agents.A365.Sdk.sln --configuration Release --no-build
dotnet format src\Microsoft.Agents.A365.Sdk.sln --verify-no-changes

# Traversal build, tests, and packages:
.\build\build.ps1 -Restore -Test -Pack
```

The build script creates a fresh `NuGetPackages\<run-id>` directory and prints its absolute path. It rejects retired Observability filenames. Do not gather release artifacts from the parent directory or previous runs.

To pack the solution directly into an isolated directory:

```powershell
$output = Join-Path $env:TEMP "agent365-packages-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $output | Out-Null
dotnet pack src\Microsoft.Agents.A365.Sdk.sln --configuration Release --no-build --output $output
```

## Package validation

Inspect the fresh output before signing or publishing:

1. Verify the six package IDs above and their git-derived versions in the `.nuspec` metadata.
2. Verify each package's assembly and XML documentation under `lib/net8.0`, README, license metadata, and symbols where enabled.
3. Check package dependencies and assemblies for references to retired Observability packages.
4. Reject any `Microsoft.Agents.A365.Observability.*` package, including symbol packages.

Tests and traversal projects are not distributable packages. Do not hardcode a package version or edit project versions to perform this retirement. Nerdbank.GitVersioning uses git history and the repository's version configuration.

## CI artifacts and external publishing

`.github/workflows/ci.yml` restores, builds, tests, and packs the solution. Its pack step creates a unique temporary directory, rejects retired filenames, and uploads only the current run's output. Upload fails if there are no packages.

The checked-in workflow does **not** publish to nuget.org. Authorized maintainers must update external signing/publishing pipelines separately. Limit those pipelines to the six maintained IDs and current-run artifacts. Disable publishing from old branches/tags and remove queued or cached release artifacts that could reintroduce retired packages. Do not use a broad recursive wildcard over old package directories.

Deprecation and optional unlisting of already-published packages are separate package-owner actions on nuget.org. Existing exact-version restores remain available; there is no automatic replacement redirect.

## Dependencies and project maintenance

All dependency versions belong in `src/Directory.Packages.props`; `.csproj` references must omit versions. Preserve central transitive/security pins needed by remaining projects even if no direct reference uses them.

Traversal projects discover source projects within their modules, but `src/Microsoft.Agents.A365.Sdk.sln` must still be updated explicitly when projects change. Regenerate `DEPENDENCIES.md` with `.\generate-dependencies.ps1` after dependency changes.

See [build internals](../build/BUILD.md) and [architecture](../docs/design.md) for additional details.
