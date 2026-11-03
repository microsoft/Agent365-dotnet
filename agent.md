# Tool discovery guidance

- Do not use `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, or `IsDevScenario()` alone to choose manifest loading or block tooling-service discovery.
- Discover MCP servers from `ToolingManifest.json` when it exists, regardless of environment. Preserve the search order: application base directory, current working directory, then entry assembly directory.
- Read `A365_USE_TOOLING_SERVICE` through `IConfiguration` (including environment-variable configuration). When it is `true`, bypass the manifest and use the tooling service even in Development. Otherwise, an existing manifest remains the default, preserving local development behavior; if no manifest exists, use the tooling service.
- Log a warning whenever loading servers from the manifest, identifying the file and how to opt into tooling-service discovery.
- Keep regression coverage for manifests present and absent, the service override, and warning logs. Do not silently fall back to the service for an empty or malformed manifest.
