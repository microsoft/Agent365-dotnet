# OBS Token Caches - Token Expiration and Invalidation

## Overview

Agent 365 OBS export is S2S-only. Exporters always send traces to
`/observabilityService/tenants/{tenantId}/otlp/agents/{agentId}/traces?api-version=1`
and require app-only OBS tokens for the exporting agent identity.

`ServiceTokenCache` is a reference implementation of `IExporterTokenCache<string>` that provides secure token caching with built-in expiration and invalidation features for observability exporters.
`AgenticTokenCache` stores app-only tokens resolved by `ObservabilityTokenResolver`; the
former delegated `AgenticTokenStruct` registration is obsolete and no longer performs OBO
or TurnContext token exchange for OBS. `RegisterObservability` is idempotent:
first registration wins and repeated calls do not replace the resolver or clear a cached
token. Use `RefreshObservabilityToken` to replace the resolver used by future refreshes.

Resolvers must validate the token they return: accept `idtyp=app`, or, when `idtyp` is
absent, a non-empty `roles` array or a non-empty `oid` equal to `sub`; reject any other
`idtyp` and reject any token containing `scp`. They must also verify the OBS audience
(`api://9b975845-388f-4429-889e-eab1ef63949c`) and token lifetime.

## Features

### 🔐 Security Features

- **Automatic Token Expiration**: Tokens expire after a configurable time period (default: 1 hour)
- **Automatic Cleanup**: Expired tokens are automatically removed on access
- **Manual Invalidation**: Support for explicit token removal (individual or all)
- **Thread-Safe Operations**: All operations are thread-safe using `ConcurrentDictionary`

### ⚙️ Configuration

- **Default Expiration**: Configurable default expiration time for all tokens
- **Per-Token Expiration**: Ability to override expiration on a per-token basis
- **Validation**: Comprehensive input validation with descriptive error messages

## Usage

### Basic Usage with Default Settings

```csharp
using Microsoft.Agents.A365.Observability.Hosting.Caching;

// Create cache with default 1-hour expiration
var cache = new ServiceTokenCache();

// Register a token
cache.RegisterObservability(
    agentId: "my-agent", 
    tenantId: "my-tenant",
    token: "observability-token-xyz",
    observabilityScopes: new[] { "https://example.com/.default" }
);

// Retrieve the token (returns null if expired or not found)
var token = cache.GetObservabilityToken("my-agent", "my-tenant");
```

### App-only Resolver Cache

```csharp
var cache = new AgenticTokenCache();

ObservabilityTokenResolver resolver = async (agentId, tenantId, scopes) =>
{
    var token = await AcquireAppOnlyObsTokenAsync(agentId, tenantId, scopes);
    ValidateAppOnlyObsToken(token);
    return token;
};

await cache.RefreshObservabilityToken("my-agent", "my-tenant", resolver, scopes);
var token = await cache.GetObservabilityToken("my-agent", "my-tenant");
```

`RefreshObservabilityToken` returns the cached token without calling the resolver while the
cached token is still usable. It propagates acquisition failures and clears stale cached
token state when the resolver fails or returns an empty token. Call it from the exporter's
`TokenResolver` or catch errors on the request path; the exporter will fail the batch
without attempting a delegated fallback. JWT tokens are refreshed near `exp`; opaque tokens
without an `exp` claim use a one-hour fallback max age from acquisition. The three-argument
`RefreshObservabilityToken` overload passes the default app-only OBS scope
`api://9b975845-388f-4429-889e-eab1ef63949c/.default` to the resolver unless
`A365_OBSERVABILITY_SCOPE_OVERRIDE` is set.

Concurrent refreshes for the same agent and tenant are serialized, so callers share one
acquisition. The automatic cleanup and `RemoveExpiredTokens` clear expired token values but
keep the resolver registration, so the next `GetObservabilityToken` call acquires a new token.

### Custom Default Expiration

```csharp
// Create cache with custom default expiration (30 minutes)
var cache = new ServiceTokenCache(TimeSpan.FromMinutes(30));

cache.RegisterObservability(
    agentId: "my-agent", 
    tenantId: "my-tenant",
    token: "observability-token-xyz",
    observabilityScopes: new[] { "https://example.com/.default" }
);
```

### Per-Token Custom Expiration

```csharp
var cache = new ServiceTokenCache();

// Register a token with custom expiration (5 minutes)
cache.RegisterObservability(
    agentId: "my-agent", 
    tenantId: "my-tenant",
    token: "short-lived-token",
    observabilityScopes: new[] { "https://example.com/.default" },
    expiresIn: TimeSpan.FromMinutes(5)
);
```

### Manual Token Invalidation

```csharp
var cache = new ServiceTokenCache();

cache.RegisterObservability("agent1", "tenant1", "token1", scopes);
cache.RegisterObservability("agent2", "tenant2", "token2", scopes);

// Invalidate a specific token
bool removed = cache.InvalidateToken("agent1", "tenant1");
// removed = true if token was found and removed

// Invalidate all tokens
cache.InvalidateAll();
```

### Periodic Cleanup of Expired Tokens

```csharp
var cache = new ServiceTokenCache();

// Register some tokens
cache.RegisterObservability("agent1", "tenant1", "token1", scopes);
cache.RegisterObservability("agent2", "tenant2", "token2", scopes);

// ... wait for some to expire ...

// Remove all expired tokens and get count
int expiredCount = cache.RemoveExpiredTokens();
Console.WriteLine($"Removed {expiredCount} expired tokens");
```

## Dependency Injection

The recommended way to use `ServiceTokenCache` is through dependency injection:

```csharp
using Microsoft.Agents.A365.Observability.Hosting;
using Microsoft.Agents.A365.Observability.Hosting.Caching;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Add service tracing exporter (automatically registers ServiceTokenCache)
services.AddServiceTracingExporter(clusterCategory: "production");

var serviceProvider = services.BuildServiceProvider();

// Get the cache instance
var cache = serviceProvider.GetRequiredService<IExporterTokenCache<string>>();
```

## Best Practices

### 1. Choose Appropriate Expiration Times

```csharp
// For short-lived operations (testing, dev)
var cache = new ServiceTokenCache(TimeSpan.FromMinutes(5));

// For production (default)
var cache = new ServiceTokenCache(TimeSpan.FromHours(1));

// For long-lived tokens
var cache = new ServiceTokenCache(TimeSpan.FromHours(24));
```

### 2. Handle Token Expiration Gracefully

```csharp
var token = cache.GetObservabilityToken(agentId, tenantId);
if (token == null)
{
    // Token expired or not found - refresh and re-register
    var newToken = await AcquireNewTokenAsync(agentId, tenantId);
    cache.RegisterObservability(agentId, tenantId, newToken, scopes);
    token = newToken;
}
```

### 3. Periodic Cleanup in Background Services

```csharp
public class TokenCleanupService : BackgroundService
{
    private readonly IExporterTokenCache<string> _cache;
    
    public TokenCleanupService(IExporterTokenCache<string> cache)
    {
        _cache = cache;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Run cleanup every 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            
            if (_cache is ServiceTokenCache serviceCache)
            {
                var removed = serviceCache.RemoveExpiredTokens();
                if (removed > 0)
                {
                    Console.WriteLine($"Cleaned up {removed} expired tokens");
                }
            }
        }
    }
}
```

### 4. Security Considerations

- **Never log tokens**: Avoid logging the actual token values
- **Use appropriate expiration**: Match expiration time with your security requirements
- **Keep workload auth separate**: OBS export tokens are app-only and independent of MCP/Graph/OBO tokens
- **Clear cache on security events**: Use `InvalidateAll()` in response to security events

```csharp
// On security breach detection
public void OnSecurityBreach()
{
    cache.InvalidateAll();
}
```

## Error Handling

The cache validates all inputs and throws `ArgumentException` for invalid parameters:

```csharp
try
{
    cache.RegisterObservability(null, "tenant", "token", scopes);
}
catch (ArgumentException ex)
{
    // "Value cannot be null or whitespace. (Parameter 'agentId')"
    Console.WriteLine(ex.Message);
}

try
{
    cache.RegisterObservability("agent", "tenant", "token", Array.Empty<string>());
}
catch (ArgumentException ex)
{
    // "Observability scopes cannot be null or empty. (Parameter 'observabilityScopes')"
    Console.WriteLine(ex.Message);
}
```

## Thread Safety

All operations are thread-safe. You can safely use the same cache instance across multiple threads:

```csharp
var cache = new ServiceTokenCache();

// Safe to call from multiple threads concurrently
Parallel.For(0, 100, i =>
{
    cache.RegisterObservability(
        $"agent-{i}", 
        $"tenant-{i}", 
        $"token-{i}", 
        scopes
    );
});
```

## Migration from Previous Version

If you're upgrading from a delegated OBS token flow, replace `AgenticTokenStruct` and
`RegisterObservability(..., AgenticTokenStruct, ...)` with an app-only
`ObservabilityTokenResolver`:

```csharp
await cache.RefreshObservabilityToken("agent", "tenant", resolver, scopes);
var token = await cache.GetObservabilityToken("agent", "tenant");
```

## See Also

- [IExporterTokenCache Interface](IExporterTokenCache.cs)
- [AgenticTokenCache](AgenticTokenCache.cs) - App-only resolver cache
- [Observability SDK Documentation](../README.md)
