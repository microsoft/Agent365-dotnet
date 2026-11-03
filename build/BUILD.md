# Centralized Build and Package Management

The SDK uses central dependency versions and traversal builds for Notifications, Runtime, and Tooling. Observability is no longer built here; see the [migration guide](../docs/observability-migration.md).

## Build files

| File | Purpose |
|---|---|
| `src/global.json` | .NET SDK selection and traversal SDK version |
| `src/Directory.Packages.props` | Central NuGet dependency versions and transitive pins |
| `src/Directory.Build.props` | Shared compiler and package settings |
| `src/dirs.proj` | Source traversal through the three maintained modules |
| `src/tests.proj` | Test traversal |
| `src/Microsoft.Agents.A365.Sdk.sln` | Explicit solution project list |
| `build/build.ps1` | PowerShell build/test/pack entry point |
| `build/build.cmd` | Windows wrapper for the PowerShell script |

## Build commands

Run from the repository root:

```powershell
.\build\build.ps1 -Restore
.\build\build.ps1 -Test
.\build\build.ps1 -Pack
.\build\build.ps1 -Clean -Restore -Test -Pack
.\build\build.ps1 -Configuration Debug -Test
```

The script switches to `src` to use the traversal projects and SDK configuration. `-Restore` explicitly restores dependencies; otherwise the build uses existing assets. Configuration defaults to Release. Any failed build, test, or pack command fails the script.

Direct solution commands are also supported:

```powershell
dotnet restore src\Microsoft.Agents.A365.Sdk.sln
dotnet build src\Microsoft.Agents.A365.Sdk.sln --configuration Release --no-restore
dotnet test src\Microsoft.Agents.A365.Sdk.sln --configuration Release --no-build
dotnet format src\Microsoft.Agents.A365.Sdk.sln --verify-no-changes
```

For direct traversal operations, use `dotnet build src\dirs.proj` and `dotnet test src\tests.proj`. Individual module traversal files discover projects with wildcards while excluding build-output directories.

## Central package management

Add dependency versions to `src/Directory.Packages.props`:

```xml
<PackageVersion Include="MyPackage" Version="1.0.0" />
```

Reference the package without a version in the project:

```xml
<PackageReference Include="MyPackage" />
```

Central versions must satisfy all projects' requirements; dependency resolution can still fail on incompatible constraints. Preserve transitive/security pins used by remaining modules. Package release versions are derived by Nerdbank.GitVersioning, not dependency-version declarations.

## Adding or removing projects

Update the relevant traversal inputs and **also** update `src/Microsoft.Agents.A365.Sdk.sln`. Automatic traversal discovery does not maintain the solution file. Add corresponding tests under `src/Tests/`, and regenerate `DEPENDENCIES.md` using `.\generate-dependencies.ps1` when package relationships change.

## Package isolation and release controls

`-Pack` creates a new `NuGetPackages\<run-id>` directory for every invocation and prints that exact path. It rejects retired `Microsoft.Agents.A365.Observability.*` filenames. Old package files are not included or silently deleted.

CI similarly packs into a new temporary directory and uploads only that run's output, failing if no packages were produced. See [package setup](../src/PACKAGE-SETUP.md) for package IDs and content checks.

External signing/publishing jobs must consume only this verified run-specific directory and allow only maintained IDs. These repository safeguards do not disable old-branch pipelines or prevent publishing previously cached artifacts; release owners must update those systems separately. No checked-in workflow pushes to nuget.org.

## Troubleshooting

If a project is missing, verify both the solution entry and module traversal patterns. For dependency failures, inspect restore errors and central versions; do not disable audit or warnings-as-errors in repository configuration to hide errors. For compiler failures, address nullable and XML documentation diagnostics, which are warnings-as-errors.
