# Microsoft Agents A365 SDK - SemanticKernel Tooling

This library provides integration between Microsoft Agents A365 and SemanticKernel, enabling you to add MCP (Model Context Protocol) tool servers to your SemanticKernel agents.

## Installation

Add the necessary project references or NuGet packages to your project.

## Setup and Configuration

### Adding MCP Services to Dependency Injection

The library provides an extension method `AddMcpServices()` to register all required services with the dependency injection container.

#### Required Namespace

```csharp
using Microsoft.Agents.A365.Tooling.SemanticKernel.Extensions;
```

#### Service Registration

The extension method registers the following services:
- `IMcpServerConfigurationService` → `McpServerConfigurationService` (from Common project)
- `IMcpToolRegistrationService` → `McpToolRegistrationService` (from SemanticKernel project)

## Usage Examples

### 1. ASP.NET Core Application

In your `Program.cs` file:

```csharp
using Microsoft.Agents.A365.Tooling.SemanticKernel.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add framework services
builder.Services.AddControllers();
builder.Services.AddLogging();

// Add MCP services
builder.Services.AddMcpServices();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.MapControllers();

app.Run();
```

### 2. Console Application with Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Tooling.SemanticKernel.Extensions;
using Microsoft.Agents.A365.Tooling.Common.Services;
using Microsoft.Agents.A365.Tooling.SemanticKernel.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Add logging (required dependency)
        services.AddLogging();
        
        // Add MCP services
        services.AddMcpServices();
        
        // Add your application services
        services.AddScoped<MyApplication>();
    })
    .Build();

// Use the services
var app = host.Services.GetRequiredService<MyApplication>();
await app.RunAsync();

public class MyApplication
{
    private readonly IMcpToolRegistrationService _mcpToolRegistrationService;
    private readonly IMcpServerConfigurationService _mcpServerConfigurationService;
    
    public MyApplication(
        IMcpToolRegistrationService mcpToolRegistrationService,
        IMcpServerConfigurationService mcpServerConfigurationService)
    {
        _mcpToolRegistrationService = mcpToolRegistrationService;
        _mcpServerConfigurationService = mcpServerConfigurationService;
    }
    
    public async Task RunAsync()
    {
        // Get MCP server configurations
        var servers = await _mcpServerConfigurationService
            .ListMCPToolServersFromToolingGatewayAsync("userId", "authToken");
            
        // Create and configure kernel with MCP tools
        var kernel = new Kernel();
        var updatedKernel = _mcpToolRegistrationService
            .AddMCPToolServerToAgent(kernel, "userId", "authToken");
            
        // Use the kernel with MCP tools...
    }
}
```

### 3. Manual Service Collection Setup

If you need to manually create and configure a service collection:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Tooling.SemanticKernel.Extensions;

var services = new ServiceCollection();

// Add logging (required dependency)
services.AddLogging(builder => builder.AddConsole());

// Add MCP services
services.AddMcpServices();

// Build the service provider
var serviceProvider = services.BuildServiceProvider();

// Resolve and use services
var mcpConfigService = serviceProvider.GetRequiredService<IMcpServerConfigurationService>();
var mcpToolService = serviceProvider.GetRequiredService<IMcpToolRegistrationService>();

// Use the services...
var servers = await mcpConfigService.ListMCPToolServersFromToolingGatewayAsync(
    "agentUserId",
    "authToken");
```

### 4. Class Library Integration

If you're building a class library that uses these services:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Tooling.SemanticKernel.Extensions;

public static class MyLibraryExtensions
{
    public static IServiceCollection AddMyLibrary(this IServiceCollection services)
    {
        // Add MCP services
        services.AddMcpServices();
        
        // Add your library-specific services
        services.AddScoped<IMyService, MyService>();
        
        return services;
    }
}

// Usage in consuming application:
// services.AddMyLibrary();
```

## Service Interfaces

### IMcpServerConfigurationService

Responsible for managing MCP server configurations.

```csharp
public interface IMcpServerConfigurationService
{
    Task<List<MCPServerConfig>> ListMCPToolServersFromToolingGatewayAsync(
        string agentUserId, 
        string authToken);
}
```

### IMcpToolRegistrationService

Responsible for registering MCP tools with SemanticKernel.

```csharp
public interface IMcpToolRegistrationService
{
    Kernel AddMCPToolServerToAgent(
        Kernel kernel, 
        string agentUserId, 
        string authToken);
}
```

## Configuration

### Environment-Based Configuration

The services automatically detect the environment using these environment variables:
- `ASPNETCORE_ENVIRONMENT`
- `DOTNET_ENVIRONMENT`

In **Development** mode:
- Reads MCP server configurations from `ToolingManifest.json`
- Disables SSL certificate validation (for development only)

In **Production** mode:
- Fetches MCP server configurations from the Tooling Gateway endpoint

### ToolingManifest.json Format

For development scenarios, create a `ToolingManifest.json` file in your project output directory:

```json
{
  "mcpServers": [
    {
      "mcpServerName": "mailMCPServer",
      "mcpServerUniqueName": "mcp_MailTools"
    },
    {
      "mcpServerName": "sharePointMCPServer",
      "mcpServerUniqueName": "mcp_SharePointTools"
    }
  ]
}
```

**Important Notes:**
- The `mcpServerUniqueName` field should contain only the server name (e.g., `mcp_MailTools`), not the full URL
- The library automatically constructs the full URL based on:
  - Current environment (Development/Test/Production)
  - Environment ID passed to the service
  - Base URL for the current environment

**URL Construction:**
The library builds full URLs like:
```
{BaseURL}/{ServerName}
```

**Environment-Based Base URLs:**
- **Development**: `https://localhost:8080/agents`
- **Test**: `https://test.agent365.svc.cloud.dev.microsoft/agents`
- **Staging**: `https://staging.agent365.svc.cloud.microsoft/agents`
- **Production**: `https://agent365.svc.cloud.microsoft/agents`

**Example:**
For environment ID `Default-5369a35c-46a5-4677-8ff9-2e65587654e7` and server name `mcp_MailTools` in Test environment:
```
https://test.agent365.svc.cloud.dev.microsoft/agents/servers/mcp_MailTools
```

## Important Notes

1. **Required Dependencies**: Ensure you have logging configured since the MCP services depend on `ILogger<T>`
2. **Service Lifetimes**: Services are registered with `Scoped` lifetime
3. **SSL in Development**: SSL certificate validation is disabled in development mode for local testing
4. **Authentication**: Services use Bearer token authentication for MCP server communication

## Troubleshooting

### Common Issues

1. **Missing Logging Configuration**: Ensure you've added logging services before calling `AddMcpServices()`
2. **ToolingManifest.json Not Found**: In development mode, ensure the manifest file is in the correct location and set to copy to output directory
3. **Authentication Errors**: Verify that the provided auth token is valid and has the necessary permissions

### Logging

The services log important information including:
- MCP server discovery and configuration
- SSL certificate validation warnings
- Tool registration success/failure

Enable appropriate log levels to see these messages during development and troubleshooting.
