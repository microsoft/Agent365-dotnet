# Microsoft Agents A365 SDK - AgentFramework Tooling

This library provides integration between Microsoft Agents A365 and Microsoft Agent Framework (Semantic Kernel Agents), enabling you to add MCP (Model Context Protocol) tool servers to your Agent Framework agents.

## Installation

Add the necessary project references or NuGet packages to your project.

## Setup and Configuration

### Adding MCP Services to Dependency Injection

The library provides an extension method `AddMcpServices()` to register all required services with the dependency injection container.

#### Required Namespace

```csharp
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Extensions;
```

#### Service Registration

The extension method registers the following services:
- `IMcpServerConfigurationService` → `McpServerConfigurationService` (from Common project)
- `IMcpToolRegistrationService` → `McpToolRegistrationService` (from AgentFramework project)

## Usage Examples

### 1. ASP.NET Core Application

In your `Program.cs` file:

```csharp
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

var builder = WebApplication.CreateBuilder(args);

// Add framework services
builder.Services.AddControllers();
builder.Services.AddLogging();

// Add Semantic Kernel services
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
    return KernelBuilder.Create()
        .AddOpenAITextCompletion("gpt-4", "your-api-key")
        .Build();
});

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
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Extensions;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Add logging (required dependency)
        services.AddLogging();
        
        // Add Semantic Kernel
        services.AddSingleton<Kernel>(serviceProvider =>
        {
            return KernelBuilder.Create()
                .AddOpenAITextCompletion("gpt-4", "your-api-key")
                .Build();
        });
        
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
    private readonly Kernel _kernel;
    
    public MyApplication(
        IMcpToolRegistrationService mcpToolRegistrationService,
        IMcpServerConfigurationService mcpServerConfigurationService,
        Kernel kernel)
    {
        _mcpToolRegistrationService = mcpToolRegistrationService;
        _mcpServerConfigurationService = mcpServerConfigurationService;
        _kernel = kernel;
    }
    
    public async Task RunAsync()
    {
        // Create an agent
        var agent = new Agent(_kernel)
        {
            Name = "MyAgent",
            Instructions = "You are a helpful assistant."
        };

        // Get MCP server configurations
        var servers = await _mcpServerConfigurationService
            .ListToolServers("userId", "envId", "authToken");
        
        // Add MCP tools to the agent
        _mcpToolRegistrationService.AddToolServersToAgent(
            agent, 
            "userId", 
            "envId", 
            "authToken");
        
        Console.WriteLine($"Agent configured with {servers.Count()} MCP tool servers");
    }
}
```

### 3. Using with Existing Agent Framework Agents

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

// Create your kernel
var kernel = KernelBuilder.Create()
    .AddOpenAITextCompletion("gpt-4", "your-api-key")
    .Build();

// Create an agent
var agent = new Agent(kernel)
{
    Name = "Assistant",
    Instructions = "You are a helpful assistant with access to external tools."
};

// Get the MCP tool registration service (from DI or create manually)
var mcpService = serviceProvider.GetRequiredService<IMcpToolRegistrationService>();

// Add MCP tools to your agent
mcpService.AddToolServersToAgent(
    agent,
    agentUserId: "user123",
    environmentId: "prod",
    authToken: "your-auth-token");

// Now your agent has access to all configured MCP tools
var response = await agent.InvokeAsync("Help me with my tasks");
```

### 4. Advanced Usage with Authentication Context

```csharp
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder;

// When you have full authentication context
var userAuthorization = new UserAuthorization(/* your auth details */);
var turnContext = /* your turn context */;

mcpService.AddToolServersToAgent(
    agent,
    agentUserId: "user123",
    environmentId: "prod",
    userAuthorization: userAuthorization,
    turnContext: turnContext);
```

### 5. Getting Tool Definitions and Functions Separately

```csharp
// Get MCP tool definitions and Kernel functions without modifying an agent
var (toolDefinitions, functions) = await mcpService
    .GetMcpToolDefinitionsAndFunctionsAsync(
        "userId", 
        "envId", 
        "authToken");

// Use the functions as needed
foreach (var function in functions)
{
    kernel.Plugins.AddFromFunctions("MCPTools", new[] { function });
}
```

## Key Features

### MCP Tool Server Integration

- **Automatic Discovery**: Discovers available MCP tool servers based on configuration
- **Live Validation**: Tests connectivity to MCP servers before integration
- **Function Conversion**: Converts MCP tools to Semantic Kernel functions
- **Error Handling**: Robust error handling for server connectivity issues

### Authentication Support

- **Bearer Token**: Simple authentication using bearer tokens
- **Full Context**: Support for complete authentication context with user authorization
- **Token Acquisition**: Automatic token acquisition when authentication context is available

### Agent Framework Integration

- **Native Integration**: Seamlessly integrates with Microsoft.SemanticKernel.Agents
- **Plugin System**: Uses Semantic Kernel's plugin system for tool registration
- **Kernel Functions**: Converts MCP tools to native Kernel functions

## Configuration

### MCP Server Configuration

Configure your MCP servers through the configuration service. The exact configuration format depends on your specific setup and requirements.

### Logging

The library uses Microsoft.Extensions.Logging for comprehensive logging:

```csharp
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Information);
});
```

## Error Handling

The library provides comprehensive error handling:

- **Connection Failures**: Graceful handling of MCP server connection issues
- **Authentication Errors**: Clear error messages for authentication problems
- **Tool Discovery Failures**: Continues processing other servers if one fails
- **Validation Errors**: Detailed validation of MCP tool definitions

## Best Practices

1. **Service Registration**: Always register MCP services in your DI container
2. **Error Handling**: Implement proper error handling around MCP tool registration
3. **Logging**: Enable appropriate logging levels to monitor MCP server interactions
4. **Authentication**: Use full authentication context when available for better security
5. **Testing**: Test MCP server connectivity in development environments

## Troubleshooting

### Common Issues

1. **MCP Server Connectivity**: Ensure MCP servers are accessible from your application
2. **Authentication**: Verify authentication tokens and permissions
3. **Tool Registration**: Check logs for tool registration failures
4. **Agent Configuration**: Ensure agents are properly configured before adding tools

### Debugging

Enable detailed logging to troubleshoot issues:

```csharp
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Debug);
});
```