# Migrating Observability to Microsoft.OpenTelemetry

The `Microsoft.Agents.A365.Observability.*` source packages and their dedicated tests have been removed from this repository. Builds of this revision no longer produce those packages. Notifications, Runtime, and Tooling remain here; this change does not add a telemetry dependency to them.

Observability development and releases are maintained in the [Microsoft OpenTelemetry .NET distro](https://github.com/microsoft/opentelemetry-distro-dotnet/tree/main).

## Authoritative documentation and packages

- [Microsoft.OpenTelemetry on NuGet](https://www.nuget.org/packages/Microsoft.OpenTelemetry): the application onboarding package.
- [Official Agent365 migration guide](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/main/docs/agent365-migration.md): API differences, middleware, framework instrumentation, and troubleshooting.
- [Agent365 getting started](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/main/docs/agent365-getting-started.md): exporter configuration, credentials, permissions, and deployment requirements.
- [Migration validation checklist](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/main/skills/microsoft-opentelemetry-migration/references/validation-checklist.md).
- [Customization guide](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/main/docs/customization.md).

[`Microsoft.OpenTelemetry.A365.Contracts`](https://www.nuget.org/packages/Microsoft.OpenTelemetry.A365.Contracts) is a shared transitive dependency brought in by the distro. **Do not install it as a standalone replacement.** Some compatible types retain `Microsoft.Agents.A365.Observability.*` namespaces even though their assemblies now come from the distro. Namespace names are not package identities.

Use the documentation matching the distro version you select. This is a package and configuration migration, not an automatic redirect or a guarantee of binary or behavioral compatibility.

## 1. Replace packages

Remove the following references wherever present, including central version declarations if no other projects need them:

| Retired package |
|---|
| `Microsoft.Agents.A365.Observability.Runtime` |
| `Microsoft.Agents.A365.Observability.Hosting` |
| `Microsoft.Agents.A365.Observability.Extensions.AgentFramework` |
| `Microsoft.Agents.A365.Observability.Extensions.SemanticKernel` |
| `Microsoft.Agents.A365.Observability.Extensions.OpenAI` |

For example, from a consuming application's project directory:

```powershell
dotnet remove package Microsoft.Agents.A365.Observability.Runtime
dotnet add package Microsoft.OpenTelemetry
```

Repeat removal for each installed retired package, and select and pin a supported distro version using your application's package-management policy. With central package management, put the version in `Directory.Packages.props` and reference the package without a version in the project.

Keep Notifications, Runtime, Tooling, and the AI framework/client packages your application uses. Remove redundant direct OpenTelemetry exporter/instrumentation references only after checking whether your custom configuration still needs them. Do not retain both old and distro telemetry registrations: duplicate providers/processors can duplicate spans or conflict. Also remove project references to copies of the old source.

## 2. Replace initialization and review API differences

Replace the old `Builder`, `AddA365Tracing()`, framework `WithAgentFramework()`/`WithSemanticKernel()`/`WithOpenAI()` setup, and standalone exporter registration with the distro's `UseMicrosoftOpenTelemetry()`.

For example, this helper configures Agent365 export using an application-supplied app-only token resolver:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenTelemetry;
using OpenTelemetry.Resources;

static void ConfigureAgent365Telemetry(
    WebApplicationBuilder builder,
    Func<string, string, Task<string?>> resolveAppOnlyToken)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("MyAgent"))
        .UseMicrosoftOpenTelemetry(options =>
        {
            options.Exporters = ExportTarget.Agent365;
            options.Agent365.UseS2SEndpoint = true;
            options.Agent365.TokenResolver = (agentId, tenantId) =>
                resolveAppOnlyToken(agentId, tenantId);
        });
}
```

Call the helper before building the host and supply your existing approved token acquisition implementation. Preserve your service name, version, resource attributes, sampling, filtering, and privacy settings. For non-hosted applications, follow the distro's standalone initialization guidance and keep the telemetry provider alive until application shutdown so pending telemetry can flush.

The old OpenAI `ChatToolCallExtensions.Trace()` convenience extension is not included in the distro. Replace it with direct `ExecuteToolScope.Start()` and response recording as described in the official migration guide. Recompile manual scope, DTO, middleware, and hosting integrations against the selected distro version; do not assume all old extension methods are available. For internal ETW scenarios, consult the distro's ETW documentation and availability rather than treating Contracts as an ETW replacement.

## 3. Preserve authentication and routing intentionally

This repository's latest standalone exporter uses an S2S-only endpoint and app-only tokens. The distro documentation also describes a DI/agentic token-cache onboarding path and a configurable `UseS2SEndpoint` option. **These paths are not interchangeable.** The example above selects S2S explicitly; do not accidentally switch to a delegated flow or rely on a different default while migrating.

Review the selected distro release's authentication behavior with your deployment owners. Keep tenant/agent-aware, app-only token acquisition and cache/refresh per identity. Validate every token before use: accept `idtyp=app`, or, only when `idtyp` is absent, a non-empty `roles` array or a non-empty `oid` equal to `sub`; reject any other `idtyp` and any token containing `scp`. Also verify audience `api://9b975845-388f-4429-889e-eab1ef63949c` and lifetime, and ensure the application's identity and tenant match the exporting agent and tenant. An `Agent365.Observability.OtelWrite` grant is not a universal prerequisite; eligible Agent365-registered instances may use roleless app tokens when service policy permits. Follow deployment-specific permission/admin-consent requirements rather than assuming every app token is authorized. Never hardcode tokens, reuse incoming delegated request tokens, or fall back to OBO. Use the distro's documented `/.default` authentication-scope helper rather than copying old scope strings.

The distro's DI agentic token cache and `RegisterObservability()` lifecycle support on-behalf-of (OBO) authentication, **not S2S**. S2S migrations must use the manual token resolver and `UseS2SEndpoint = true`; see the [official S2S example](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/main/examples/Microsoft.OpenTelemetry.Agent365.S2S.Demo/README.md). Do not remove the application's credential registration or token acquisition merely because initialization has changed.

Agent365 export requires its own identity, authentication, and endpoint routing. Installing generic OpenTelemetry packages or selecting `ExportTarget.Otlp` does not preserve that behavior. Use `ExportTarget.Agent365` for that destination; configure OTLP or Azure Monitor separately when those are intentional destinations.

## 4. Preserve context and framework instrumentation

The distro subscribes to supported Agent Framework, Semantic Kernel, and OpenAI activity sources, but the frameworks must still emit their activities. Preserve any framework-specific telemetry enablement. The upstream guide documents the OpenAI `OpenAI.Experimental.EnableOpenTelemetry` switch and explicit `.AddSource()` registration for custom Agent Framework/application source names.

Identity context must be set **before** creating spans. The distro does not automatically register all baggage middleware:

- Register `BaggageTurnMiddleware` in the agent adapter pipeline when using turn-based context.
- Register `UseObservabilityRequestContext()` before the agent HTTP pipeline when resolving HTTP-level tenant/agent identity.
- For manual processing, scope a `BaggageBuilder` with the correct tenant and agent IDs around the operation and dispose it afterward to restore context.

See the official migration guide for signatures and middleware ordering. Spans without required tenant/agent identity may be dropped by the Agent365 exporter. Test concurrent tenants to ensure context and tokens cannot leak between requests.

Review exporter differences as well: Agent365 exports traces, not all metrics/logs; Agent365-only mode can suppress infrastructure instrumentation. For offline buffering on Unix, the distro requires a customer-provisioned private storage directory or an explicit decision to disable offline storage. Review permissions, sensitive-data retention, and replay/backlog handling before rollout.

## 5. Validate the consuming application

1. Restore and build without references to retired packages, including transitive dependencies.
2. Exercise an agent invocation, inference, tool call, and output. Compare parent/child relationships, resource attributes, tenant/agent identity, and expected payloads with the previous deployment.
3. Confirm exporter authentication, endpoint routing, and receipt in the intended backend; locally visible spans alone do not prove Agent365 delivery.
4. Verify custom source subscriptions, privacy/sensitive-content controls, error reporting, graceful shutdown, and any offline replay behavior.
5. Test at least two tenant/agent identities and token refresh/failure cases before production rollout.

Use Console export only for controlled development with non-sensitive data, not production. Follow the distro's [migration validation checklist](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/main/skills/microsoft-opentelemetry-migration/references/validation-checklist.md) where applicable to your selected authentication flow; its `RegisterObservability()` checks apply to OBO, not S2S.

## Maintainer checklist: existing NuGet packages

Repository removal does **not** deprecate, unlist, delete, or redirect already-published packages. An authorized package owner must perform the following on [nuget.org](https://www.nuget.org/), following [NuGet's deprecation instructions](https://learn.microsoft.com/en-us/nuget/nuget-org/deprecate-packages):

1. After this guide is available on the target branch, open **Manage package > Deprecation** for each of the five retired IDs above.
2. Select **all versions**, including stable and preview releases, and choose **Legacy**.
3. Set the alternative package to **Microsoft.OpenTelemetry**. If selecting an alternative version, verify that it is published and suitable for consumers.
4. Use this deprecation message:

   > This package is no longer maintained in Agent365-dotnet. Migrate to Microsoft.OpenTelemetry: https://www.nuget.org/packages/Microsoft.OpenTelemetry. Package and API changes are required; see https://github.com/microsoft/Agent365-dotnet/blob/main/docs/observability-migration.md and https://github.com/microsoft/opentelemetry-distro-dotnet/tree/main. This is not an automatic or drop-in replacement.

5. Verify the notice and alternative recommendation on each package page. NuGet clients can show the alternative; the custom message is displayed on nuget.org, not all clients.
6. Optionally **unlist** retired versions to hide them from search. Unlisting and deprecation are separate actions; neither prevents restores by exact version. Preserve existing published versions for reproducible restores.

There is **no automatic dependency redirect**. Consumers must change package references and migrate their applications. Do not publish empty replacement packages under the retired IDs.

## Release controls: repository versus external systems

**In this revision:** the solution and traversal build exclude all Observability projects; dedicated tests and exclusive dependencies are removed. CI and `build\build.ps1 -Pack` use new per-run output directories and reject retired Observability filenames before advertising/uploading the output. CI uploads only that run's directory and fails if it contains no packages. The dependency diagram no longer includes retired packages.

**Required external actions:** update any private publishing/signing/release pipelines to consume only the current run's verified output, not a recursive or root-level wildcard over historical `NuGetPackages` directories. Retire queued releases and cached packages from previous runs. Exclude all retired IDs in the publishing allowlist, and disable old-branch/tag release jobs or permissions that could still publish them. Existing commits, branches, artifacts, and external pipelines are not rewritten by this change.

The checked-in CI produces artifacts; it does not push packages to nuget.org. Package owners must separately apply the NuGet deprecations above and enforce external release controls. No registry settings are changed by this repository update.
