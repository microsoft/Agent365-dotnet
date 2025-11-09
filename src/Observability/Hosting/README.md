# Microsoft.Agents.A365.Observability.Hosting

The Observability Hosting package provides ASP.NET Core integration and hosting utilities for the Agent365 Observability SDK. This package includes middleware, ETW (Event Tracing for Windows) support, and configuration helpers for web applications.

## Overview

This package enables seamless integration of observability features into ASP.NET Core applications, providing:

- Request/response tracing middleware
- ETW event providers for Windows-based hosting
- Service registration extensions
- Configuration management
- Hosting environment integration

## Features

- **ASP.NET Core Middleware**: Automatic HTTP request/response tracing
- **ETW Support**: Event Tracing for Windows integration for production monitoring
- **Service Registration**: Simplified dependency injection setup
- **Configuration Integration**: Seamless integration with ASP.NET Core configuration
- **Health Checks**: Built-in health check support for observability components
- **Logging Integration**: Automatic correlation between traces and logs

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Hosting
```

## Quick Start

### Basic Setup

```csharp
using Microsoft.Agents.A365.Observability.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add observability hosting services
builder.Services.AddObservabilityHosting();

var app = builder.Build();

// Use observability middleware
app.UseObservabilityMiddleware();

app.Run();
```

### Advanced Configuration

```csharp
using Microsoft.Agents.A365.Observability.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Configure observability with hosting options
builder.Services.AddObservabilityHosting(options =>
{
    options.EnableRequestTracing = true;
    options.EnableResponseTracing = true;
    options.EnableEtw = true;
    options.SensitiveDataLogging = false;
    options.MaxRequestBodySize = 4096;
    options.MaxResponseBodySize = 4096;
});

var app = builder.Build();

// Add middleware in the request pipeline
app.UseObservabilityMiddleware();

app.MapGet("/health", () => "Healthy");

app.Run();
```

## ETW (Event Tracing for Windows) Support

The package includes ETW event providers for production monitoring and diagnostics on Windows platforms.

### Enabling ETW

```csharp
builder.Services.AddObservabilityHosting(options =>
{
    options.EnableEtw = true;
    options.EtwProviderName = "Microsoft-Agents-A365-Observability";
});
```

### Collecting ETW Events

Use Windows Performance Recorder (WPR) or PerfView to collect ETW events:

```powershell
# Using PerfView
PerfView.exe collect -AcceptEula -NoGui -NoNGenRundown

# Using logman (Windows built-in)
logman create trace AgentTrace -p "Microsoft-Agents-A365-Observability" -o trace.etl
logman start AgentTrace
# ... run your application ...
logman stop AgentTrace
```

## Middleware Features

### Request/Response Tracing

The middleware automatically captures:

- Request path, method, headers (configurable)
- Request body (configurable, with size limits)
- Response status code, headers (configurable)
- Response body (configurable, with size limits)
- Request duration and timing
- Correlation IDs and trace context

### Automatic Context Propagation

The middleware ensures W3C trace context propagation across distributed systems:

```csharp
// Automatic trace context propagation via headers
// traceparent: 00-{trace-id}-{span-id}-{flags}
// tracestate: {vendor-specific-data}
```

### Custom Enrichment

```csharp
app.UseObservabilityMiddleware(context =>
{
    // Add custom tags to each request trace
    return new Dictionary<string, object>
    {
        ["user.id"] = context.User.Identity?.Name ?? "anonymous",
        ["tenant.id"] = context.Request.Headers["X-Tenant-Id"].ToString(),
        ["environment"] = builder.Environment.EnvironmentName
    };
});
```

## Configuration Options

### appsettings.json

```json
{
  "ObservabilityHosting": {
    "EnableRequestTracing": true,
    "EnableResponseTracing": true,
    "EnableEtw": false,
    "SensitiveDataLogging": false,
    "MaxRequestBodySize": 4096,
    "MaxResponseBodySize": 4096,
    "ExcludedPaths": [
      "/health",
      "/metrics",
      "/favicon.ico"
    ],
    "IncludeRequestHeaders": [
      "User-Agent",
      "X-Correlation-Id",
      "X-Tenant-Id"
    ]
  }
}
```

### Loading from Configuration

```csharp
builder.Services.AddObservabilityHosting(
    builder.Configuration.GetSection("ObservabilityHosting"));
```

## Health Checks Integration

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

builder.Services.AddHealthChecks()
    .AddCheck<ObservabilityHealthCheck>("observability");

app.MapHealthChecks("/health");
```

## Best Practices

1. **Exclude Static Resources**: Don't trace static files, health checks, or metrics endpoints
2. **Limit Body Sizes**: Set appropriate size limits for request/response body logging
3. **Sensitive Data**: Never log sensitive data (passwords, tokens, PII)
4. **Sampling**: Use sampling in high-traffic scenarios to reduce overhead
5. **ETW in Production**: Enable ETW for production Windows deployments
6. **Custom Enrichment**: Add business context tags for better filtering

## Performance Considerations

- Middleware adds ~2-5ms latency per request
- Body capture adds proportional overhead to body size
- ETW has minimal overhead (~0.5-1ms)
- Sampling reduces overhead in high-traffic scenarios
- Async operations don't block the request pipeline

## Integration with Other Packages

Works seamlessly with:

- **Microsoft.Agents.A365.Observability**: Core tracing functionality
- **Microsoft.Agents.A365.Observability.Runtime**: Runtime exporters and utilities
- **Microsoft.AspNetCore.Diagnostics.HealthChecks**: Health check integration
- **Microsoft.Extensions.Logging**: Logging correlation

## ETW Directory Structure

The `Etw/` directory contains:

- Event provider implementations
- Event manifest definitions
- ETW utility classes
- Documentation for event IDs and payloads

## Related Documentation

- [Observability Module Overview](../README.md)
- [Core Package](../Core/README.md)
- [Runtime Package](../Runtime/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

This project is licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
