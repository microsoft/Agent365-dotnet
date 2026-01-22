# Microsoft.Agents.A365.Runtime - Design Documentation

## Overview

The `Microsoft.Agents.A365.Runtime` package provides foundational utilities shared across the Microsoft Agent 365 SDK. It includes tenant context extraction, standardized operation results, and authorization services for multi-tenant agent applications.

## Architecture

```
Microsoft.Agents.A365.Runtime
├── Public API
│   ├── TenantContextHelper      # Tenant/worker context extraction
│   ├── OperationResult          # Success/failure result pattern
│   ├── OperationError           # Error details with status codes
│   ├── AgenticAuthorizationService  # Authorization handling
│   └── UserAgentHelper          # User agent utilities
└── Internal
    └── Utility                  # Performance-optimized helpers
```

## Key Components

### TenantContextHelper

**Source**: [TenantContextHelper.cs](../TenantContextHelper.cs)

Static helper class for extracting tenant and worker context from ASP.NET Core `HttpContext`. Implements a precedence-based lookup strategy.

**Lookup Precedence:**
1. User claims (most secure) - `tenant_id`, `worker_id`
2. Request headers (API fallback) - `X-Tenant-Id`, `X-Worker-Id`
3. HttpContext.Items (middleware-set) - `TenantId`, `WorkerId`

```csharp
// Extract tenant ID from HttpContext
string? tenantId = TenantContextHelper.GetTenantId(httpContext);
string? workerId = TenantContextHelper.GetWorkerId(httpContext);
```

**Constants:**

| Constant | Value | Purpose |
|----------|-------|---------|
| `TenantClaimName` | `tenant_id` | Claim name for tenant ID |
| `WorkerClaimName` | `worker_id` | Claim name for worker ID |
| `TenantHeaderName` | `X-Tenant-Id` | Header name for tenant ID |
| `WorkerHeaderName` | `X-Worker-Id` | Header name for worker ID |
| `TenantItemKey` | `TenantId` | HttpContext.Items key for tenant |
| `WorkerItemKey` | `WorkerId` | HttpContext.Items key for worker |

### OperationResult

**Source**: [OperationResult.cs](../OperationResult.cs)

Represents the result of an operation, providing a standardized way to handle success and failure states without exceptions.

```csharp
// Successful operation
return OperationResult.Success;

// Failed operation with errors
return OperationResult.Failed(
    new OperationError("Resource not found", HttpStatusCode.NotFound),
    new OperationError("Validation failed", HttpStatusCode.BadRequest)
);

// Check result
if (!result.Succeeded)
{
    foreach (var error in result.Errors)
    {
        logger.LogError("{Status}: {Message}", error.StatusCode, error.Message);
    }
}
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Succeeded` | `bool` | Whether the operation succeeded |
| `Errors` | `IEnumerable<OperationError>` | Collection of errors if failed |

**Static Members:**

| Member | Description |
|--------|-------------|
| `Success` | Singleton successful result |
| `Failed(params OperationError[])` | Create failed result with errors |

### OperationError

Represents an error that occurred during an operation.

```csharp
var error = new OperationError("Invalid input", HttpStatusCode.BadRequest);
Console.WriteLine($"{error.StatusCode}: {error.Message}");
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Message` | `string` | Error description |
| `StatusCode` | `HttpStatusCode` | HTTP status code for the error |

### AgenticAuthorizationService

Handles authorization for agentic operations, providing integration with the Microsoft 365 Agents SDK authentication system.

## Design Patterns

### Result Pattern

The `OperationResult` class implements the Result pattern, providing an alternative to throwing exceptions for expected failure cases:

```csharp
public async Task<OperationResult> ProcessRequestAsync(Request request)
{
    if (string.IsNullOrEmpty(request.Id))
    {
        return OperationResult.Failed(
            new OperationError("Request ID is required", HttpStatusCode.BadRequest));
    }

    try
    {
        await DoWorkAsync(request);
        return OperationResult.Success;
    }
    catch (HttpRequestException ex)
    {
        return OperationResult.Failed(
            new OperationError(ex.Message, HttpStatusCode.ServiceUnavailable));
    }
}
```

**Benefits:**
- Explicit handling of failure cases
- No exception overhead for expected failures
- Clear API contract for callers
- Easy aggregation of multiple errors

### Precedence-Based Resolution

`TenantContextHelper` implements a precedence-based lookup for tenant context:

```csharp
public static string? GetTenantId(HttpContext? context)
{
    // 1. Claims - most secure, from authenticated user
    var fromClaims = context.User?.FindFirst(TenantClaimName)?.Value;
    if (!string.IsNullOrWhiteSpace(fromClaims)) return fromClaims;

    // 2. Headers - fallback for API scenarios
    if (context.Request.Headers.TryGetValue(TenantHeaderName, out var header))
        if (!string.IsNullOrWhiteSpace(header.FirstOrDefault()))
            return header.FirstOrDefault();

    // 3. Items - middleware-set values
    if (context.Items.TryGetValue(TenantItemKey, out var item))
        if (!string.IsNullOrWhiteSpace(item?.ToString()))
            return item.ToString();

    return null;
}
```

## File Structure

```
src/Runtime/Core/
├── TenantContextHelper.cs       # Tenant context extraction
├── OperationResult.cs           # Operation result pattern
├── OperationError.cs            # Error details
├── AgenticAuthorizationService.cs  # Authorization service
├── UserAgentHelper.cs           # User agent utilities
├── Utility.cs                   # Internal helpers
├── Agent365SdkUserAgentConfiguration.cs  # User agent config
├── Microsoft.Agents.A365.Runtime.csproj
└── docs/
    └── design.md                # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.AspNetCore.Http.Abstractions` | HttpContext access |
| `Microsoft.Agents.Authentication.Msal` | MSAL authentication |
| `Microsoft.Agents.Hosting.AspNetCore` | Agent hosting |

## Usage Examples

### Multi-Tenant Request Processing

```csharp
public class TenantAwareMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = TenantContextHelper.GetTenantId(context);

        if (string.IsNullOrEmpty(tenantId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Tenant ID required");
            return;
        }

        // Set for downstream middleware
        context.Items[TenantContextHelper.TenantItemKey] = tenantId;

        await _next(context);
    }
}
```

### Service with Operation Results

```csharp
public class DataService
{
    public async Task<OperationResult> SaveDataAsync(Data data)
    {
        var validation = ValidateData(data);
        if (!validation.Succeeded)
            return validation;

        try
        {
            await _repository.SaveAsync(data);
            return OperationResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save data");
            return OperationResult.Failed(
                new OperationError("Failed to save data", HttpStatusCode.InternalServerError));
        }
    }

    private OperationResult ValidateData(Data data)
    {
        var errors = new List<OperationError>();

        if (string.IsNullOrEmpty(data.Name))
            errors.Add(new OperationError("Name is required", HttpStatusCode.BadRequest));

        if (data.Value < 0)
            errors.Add(new OperationError("Value must be non-negative", HttpStatusCode.BadRequest));

        return errors.Count > 0
            ? OperationResult.Failed(errors.ToArray())
            : OperationResult.Success;
    }
}
```

## External Resources

- [Microsoft Agent 365 Documentation](https://learn.microsoft.com/en-us/dotnet/api/agent365-sdk-dotnet/agent365-overview)
- [ASP.NET Core Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/)
