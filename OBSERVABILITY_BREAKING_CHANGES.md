# Observability Breaking Changes - Past Month Summary

## Executive Summary

This document summarizes breaking changes made to the Observability SDK in the past month (November 18, 2024 - December 18, 2024) that may impact consumers of the SDK.

---

## Breaking Changes

### 1. **Builder Constructor Made Public** (PR #143)
**Merged:** December 17, 2024  
**Impact:** High  
**Breaking Change:** Constructor parameter order changed

#### What Changed
The `Builder` class constructor was changed from `internal` to `public`, and parameter order was modified:

**Before:**
```csharp
internal Builder(IServiceCollection services, bool useOpenTelemetryBuilder, Agent365ExporterType agent365ExporterType, IConfiguration? configuration)
```

**After:**
```csharp
public Builder(IServiceCollection services, IConfiguration? configuration, bool useOpenTelemetryBuilder = true, Agent365ExporterType agent365ExporterType = Agent365ExporterType.Agent365Exporter)
```

#### Migration Required
If you were using reflection or internal access to create a `Builder` instance, update your code:

```csharp
// OLD (won't compile)
var builder = new Builder(services, true, Agent365ExporterType.Agent365Exporter, configuration);

// NEW
var builder = new Builder(services, configuration, useOpenTelemetryBuilder: true, agent365ExporterType: Agent365ExporterType.Agent365Exporter);
```

**Reason for Change:** To support scenarios using legacy .NET middleware that cannot use `AddA365Tracing()` builder extensions. This allows clients to create a Builder using their service collection and configuration, then invoke `builder.Build()` to setup A365 tracing.

---

### 2. **API Method Renamed: AddTracing → AddA365Tracing** (PR #84)
**Merged:** November 17, 2024  
**Impact:** High  
**Breaking Change:** Method name change

#### What Changed
The main entry point method for adding tracing was renamed and signature changed:

**Before:**
```csharp
services.AddTracing(configure: b => { }, useOpenTelemetryBuilder: true, agent365ExporterType: Agent365ExporterType.Agent365Exporter)
```

**After:**
```csharp
builder.AddA365Tracing(configure: b => { }, useOpenTelemetryBuilder: true, agent365ExporterType: Agent365ExporterType.Agent365Exporter)
```

#### Migration Required
Update all calls to `AddTracing()`:

```csharp
// OLD - Using ServiceCollection
var services = new ServiceCollection();
services.AddTracing(useOpenTelemetryBuilder: true);

// NEW - Using HostApplicationBuilder
var builder = new HostApplicationBuilder();
builder.AddA365Tracing(useOpenTelemetryBuilder: true);
```

**Reason for Change:** Better integration with `IHostApplicationBuilder` and improved .NET SDK naming patterns. Also adds support for configuration-driven behavior.

---

### 3. **Agent365ExporterOptions - New Required DomainResolver Property** (PR #138)
**Merged:** December 11, 2024  
**Impact:** Medium  
**Breaking Change:** New property with default value (non-breaking for most users)

#### What Changed
A new `DomainResolver` delegate property was added to `Agent365ExporterOptions`:

```csharp
public delegate string TenantDomainResolver(string tenantId);

public sealed class Agent365ExporterOptions
{
    public Agent365ExporterOptions()
    {
        this.DomainResolver = tenantId => new PowerPlatformApiDiscovery(this.ClusterCategory).GetTenantIslandClusterEndpoint(tenantId);
    }
    
    // New property
    public TenantDomainResolver DomainResolver { get; set; }
}
```

#### Migration Required
**Most users:** No migration needed. The default constructor sets a default resolver.

**Custom domain scenarios:** If you were relying on `A365_OBSERVABILITY_DOMAIN_OVERRIDE` environment variable for multi-tenant scenarios, you can now provide a custom resolver:

```csharp
var options = new Agent365ExporterOptions
{
    TokenResolver = (agentId, tenantId) => GetToken(agentId, tenantId),
    DomainResolver = tenantId => GetCustomDomain(tenantId) // New: Per-tenant domain resolution
};
```

**Reason for Change:** Domain override via `A365_OBSERVABILITY_DOMAIN_OVERRIDE` environment variable does not support multi-tenant scenarios. This enables tenant-specific domain overrides.

**Note:** Environment variable `A365_OBSERVABILITY_DOMAIN_OVERRIDE` still works but takes precedence over the `DomainResolver` delegate.

---

### 4. **IHostBuilder Extension Added** (PR #142)
**Merged:** December 17, 2024  
**Impact:** Low (Additive - Not Breaking)  
**Breaking Change:** None - New functionality

#### What Changed
Added support for generic host scenarios with `IHostBuilder` overload:

```csharp
public static IHostBuilder AddA365Tracing(
    this IHostBuilder builder,
    Action<Builder>? configure = null,
    bool useOpenTelemetryBuilder = true,
    Agent365ExporterType agent365ExporterType = Agent365ExporterType.Agent365Exporter)
```

**No migration required** - This is an additive change providing more flexibility for host configuration.

---

### 5. **Observability Core Package Refactored into Hosting** (PR #99)
**Merged:** November 19, 2024  
**Impact:** High  
**Breaking Change:** Namespace and assembly changes

#### What Changed
Components were moved from `Microsoft.Agents.A365.Observability` (Core package) to `Microsoft.Agents.A365.Observability.Hosting` package:

**Moved Classes and Namespaces:**
- `AgenticTokenCache` moved from `Microsoft.Agents.A365.Observability.Caching` to `Microsoft.Agents.A365.Observability.Hosting.Caching`
- `AgenticTokenStruct` moved from `Microsoft.Agents.A365.Observability.Caching` to `Microsoft.Agents.A365.Observability.Hosting.Caching`
- `IExporterTokenCache<T>` moved from `Microsoft.Agents.A365.Observability.Caching` to `Microsoft.Agents.A365.Observability.Hosting.Caching`
- `ServiceTokenCache` moved from `Microsoft.Agents.A365.Observability.Caching` to `Microsoft.Agents.A365.Observability.Hosting.Caching`
- `ObservabilityBaggageMiddleware` moved from `Microsoft.Agents.A365.Observability.Services` to `Microsoft.Agents.A365.Observability.Hosting.Middleware`
- `BaggageBuilderExtensions` moved from `Microsoft.Agents.A365.Observability.Common` to `Microsoft.Agents.A365.Observability.Hosting.Extensions`
- `InvokeAgentScopeExtensions` moved from `Microsoft.Agents.A365.Observability.Common` to `Microsoft.Agents.A365.Observability.Hosting.Extensions`
- `TurnContextExtensions` moved from `Microsoft.Agents.A365.Observability.Common` to `Microsoft.Agents.A365.Observability.Hosting.Extensions`

**Core package (`Microsoft.Agents.A365.Observability`) removed entirely**

#### Migration Required

1. **Update NuGet package references:**
```xml
<!-- OLD -->
<PackageReference Include="Microsoft.Agents.A365.Observability" Version="x.x.x" />

<!-- NEW -->
<PackageReference Include="Microsoft.Agents.A365.Observability.Hosting" Version="x.x.x" />
```

2. **Update using statements:**
```csharp
// OLD
using Microsoft.Agents.A365.Observability.Caching;
using Microsoft.Agents.A365.Observability.Services;
using Microsoft.Agents.A365.Observability.Common;

// NEW
using Microsoft.Agents.A365.Observability.Hosting.Caching;
using Microsoft.Agents.A365.Observability.Hosting.Middleware;
using Microsoft.Agents.A365.Observability.Hosting.Extensions;
```

3. **Update DI registration calls:**
```csharp
// OLD
using Microsoft.Agents.A365.Observability;
services.AddAgenticTracingExporter();
services.AddServiceTracingExporter();

// NEW
using Microsoft.Agents.A365.Observability.Hosting;
services.AddAgenticTracingExporter();
services.AddServiceTracingExporter();
```

**Reason for Change:** The Hosting package relies on the Agents SDK package, which was becoming a requirement for all Observability components. Moving hosting-specific components (like token caching, middleware) to a separate package allows the Runtime package to remain lightweight for scenarios that don't need Agents SDK dependencies.

---

### 6. **Configuration-Based Feature Flags** (PR #84)
**Merged:** November 17, 2024  
**Impact:** Medium  
**Breaking Change:** Environment variable behavior changed

#### What Changed
The `EnableAgent365Exporter` check now uses `IConfiguration` instead of directly reading from environment variables:

**Before:**
```csharp
private bool IsAgent365ExporterEnabled()
{
    string enabledEnv = Environment.GetEnvironmentVariable("EnableAgent365Exporter");
    return string.IsNullOrEmpty(enabledEnv) ? false : enabledEnv.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase);
}
```

**After:**
```csharp
private bool IsAgent365ExporterEnabled()
{
    if (Configuration != null && Configuration["EnableAgent365Exporter"] != null)
    {
        string enabledEnv = Configuration["EnableAgent365Exporter"]!;
        return enabledEnv.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }
    return false;
}
```

#### Migration Required
**Most users:** No migration needed. Environment variables still work through `IConfiguration`.

**Custom configuration:** If you were setting environment variables programmatically, ensure they're set before the `IConfiguration` is built, or provide them through appsettings.json:

```json
{
  "EnableAgent365Exporter": "true",
  "EnableOtlpExporter": "true"
}
```

**Reason for Change:** Support for configuration-driven behavior in addition to environment variables, following .NET best practices.

---

### 7. **OTLP Exporter Now Configuration-Driven** (PR #84)
**Merged:** November 17, 2024  
**Impact:** Low  
**Breaking Change:** Conditional behavior change

#### What Changed
OTLP exporter in SemanticKernel and AgentFramework extensions is now conditionally enabled via configuration:

**Before:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(SemanticKernelTelemetryConstants.SemanticKernelSourceWildcard)
        .AddProcessor(new SemanticKernelSpanProcessor()))
    .UseOtlpExporter();  // Always enabled
```

**After:**
```csharp
var telmConfig = builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(SemanticKernelTelemetryConstants.SemanticKernelSourceWildcard)
        .AddProcessor(new SemanticKernelSpanProcessor()));

if (builder.Configuration != null
    && !string.IsNullOrEmpty(builder.Configuration["EnableOtlpExporter"])
    && bool.TryParse(builder.Configuration["EnableOtlpExporter"], out bool enabled) && enabled)
{
    telmConfig.UseOtlpExporter();  // Conditionally enabled
}
```

#### Migration Required
If you relied on OTLP exporter being always enabled, set the configuration:

```json
{
  "EnableOtlpExporter": "true"
}
```

Or via environment variable:
```bash
export EnableOtlpExporter=true
```

**Reason for Change:** Support for Aspire desktop log viewer and more flexible exporter configuration.

---

## Summary of Impacts

| Change | Impact Level | Migration Effort | Affected Users |
|--------|--------------|------------------|----------------|
| Builder constructor parameter order | High | Low | Users creating Builder instances directly |
| AddTracing → AddA365Tracing | High | Medium | All SDK consumers |
| DomainResolver property | Medium | Low | Multi-tenant deployments with custom domains |
| Core package → Hosting package | High | Medium | Users of caching, middleware, or extension classes |
| Configuration-based flags | Medium | Low | Users with custom configuration setups |
| OTLP exporter conditional | Low | Low | Users expecting OTLP always enabled |
| IHostBuilder overload (additive) | Low | None | None - new functionality |

---

## Testing Recommendations

After migrating:

1. **Verify tracing initialization** - Ensure `AddA365Tracing()` is called correctly
2. **Test multi-tenant scenarios** - If using custom domains, test DomainResolver
3. **Check configuration** - Verify `EnableAgent365Exporter` and `EnableOtlpExporter` settings
4. **Test with Hosting features** - If using token caching or middleware, verify correct namespaces
5. **Run integration tests** - Ensure spans are exported correctly to Agent365 backend

---

## Support and Resources

- **Documentation:** [Microsoft Agents 365 Observability](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/observability?tabs=dotnet)
- **Issues:** [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues)
- **Samples:** [Agent365-Samples](https://github.com/microsoft/Agent365-Samples)

---

**Generated:** December 18, 2024  
**Analysis Period:** November 18, 2024 - December 18, 2024
