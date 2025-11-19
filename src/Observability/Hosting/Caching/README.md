# ServiceTokenCache - Token Expiration and Invalidation

## Overview

`ServiceTokenCache` is a reference implementation of `IExporterTokenCache<string>` that provides secure token caching with built-in expiration and invalidation features for observability exporters.

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
- **Invalidate on logout**: Call `InvalidateToken` when a user logs out
- **Clear cache on security events**: Use `InvalidateAll()` in response to security events

```csharp
// On user logout
public void OnUserLogout(string agentId, string tenantId)
{
    cache.InvalidateToken(agentId, tenantId);
}

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

If you're upgrading from a previous version without expiration support, no code changes are required. The default behavior maintains backward compatibility:

```csharp
// Old code - still works with default 1-hour expiration
var cache = new ServiceTokenCache();
cache.RegisterObservability("agent", "tenant", "token", scopes);
var token = cache.GetObservabilityToken("agent", "tenant");
```

To opt-in to custom expiration:

```csharp
// New code - with custom expiration
var cache = new ServiceTokenCache(TimeSpan.FromMinutes(30));
cache.RegisterObservability("agent", "tenant", "token", scopes, TimeSpan.FromMinutes(10));
```

## See Also

- [IExporterTokenCache Interface](../Core/Caching/IExporterTokenCache.cs)
- [AgenticTokenCache](../Core/Caching/AgenticTokenCache.cs) - Alternative implementation for agentic scenarios
- [Observability SDK Documentation](../README.md)
