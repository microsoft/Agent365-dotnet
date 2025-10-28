# Microsoft Agents A365 SDK - AgentFramework Tooling

This library provides integration between Microsoft Agents A365 and Microsoft Extensions AI (AIAgent), enabling you to add MCP (Model Context Protocol) tool servers to your AI agents.

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
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add framework services
builder.Services.AddControllers();
builder.Services.AddLogging();

// Add Azure OpenAI client
builder.Services.AddSingleton<AzureOpenAIClient>(serviceProvider =>
{
    var endpoint = new Uri("https://your-resource.openai.azure.com");
    var credential = new DefaultAzureCredential();
    return new AzureOpenAIClient(endpoint, credential);
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
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure.Identity;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Add logging (required dependency)
        services.AddLogging();
        
        // Add Azure OpenAI client
        services.AddSingleton<AzureOpenAIClient>(serviceProvider =>
        {
            var endpoint = new Uri("https://your-resource.openai.azure.com");
            var credential = new DefaultAzureCredential();
            return new AzureOpenAIClient(endpoint, credential);
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
    private readonly AzureOpenAIClient _azureOpenAIClient;
    
    public MyApplication(
        IMcpToolRegistrationService mcpToolRegistrationService,
        IMcpServerConfigurationService mcpServerConfigurationService,
        AzureOpenAIClient azureOpenAIClient)
    {
        _mcpToolRegistrationService = mcpToolRegistrationService;
        _mcpServerConfigurationService = mcpServerConfigurationService;
        _azureOpenAIClient = azureOpenAIClient;
    }
    
    public async Task RunAsync()
    {
        // Define initial tools (if any)
        var initialTools = new List<AITool>();

        // Get MCP server configurations
        var servers = await _mcpServerConfigurationService
            .ListToolServers("userId", "envId", "authToken");
        
        // Add MCP tools to get combined tools
        await _mcpToolRegistrationService.AddToolServersToAgent(
            chatClient,  // IChatClient instead of AzureOpenAIClient
            "You are a helpful assistant.",
            initialTools,
            "userId", 
            "envId", 
            "authToken");
        
        Console.WriteLine($"Agent configured with {servers.Count()} MCP tool servers");
    }
}
```

### 3. Using with Microsoft.Extensions.AI

```csharp
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

// Create your Azure OpenAI client
var endpoint = new Uri("https://your-resource.openai.azure.com");
var credential = new DefaultAzureCredential();
var azureClient = new AzureOpenAIClient(endpoint, credential);

// Define initial tools (if any)
var initialTools = new List<AITool>();

// Get the MCP tool registration service (from DI or create manually)
var mcpService = serviceProvider.GetRequiredService<IMcpToolRegistrationService>();

// Add MCP tools to get combined tools collection
await mcpService.AddToolServersToAgent(
    chatClient,  // IChatClient - get this from azureClient.GetChatClient(deploymentName).AsIChatClient()
    agentInstructions: "You are a helpful assistant with access to external tools.",
    initialTools: initialTools,
    agentUserId: "user123",
    environmentId: "prod",
    authToken: "your-auth-token");

// The method internally creates an AIAgent with combined tools
Console.WriteLine("Agent created with MCP tools integrated");
```

### 4. Simple Usage with Nullable Parameters

```csharp
// The service supports nullable parameters for flexibility

// With just MCP tools (no initial tools or instructions)
await mcpService.AddToolServersToAgent(
    azureClient,
    agentInstructions: null,           // No specific instructions
    initialTools: null,               // No initial tools
    agentUserId: "user123",
    environmentId: "prod",
    authToken: "your-auth-token");

// With instructions but no initial tools
await mcpService.AddToolServersToAgent(
    azureClient,
    agentInstructions: "You are a helpful assistant.",
    initialTools: null,               // No initial tools
    agentUserId: "user123",
    environmentId: "prod",
    authToken: "your-auth-token");
```

### 5. Getting Combined Tools Collection

```csharp
// If you want to create the AIAgent yourself, you can get just the tools
// Note: This would require modifying the service to return tools instead of void

// Create your own agent with combined tools
var combinedTools = new List<AITool>();
combinedTools.AddRange(initialTools ?? Enumerable.Empty<AITool>());
// Add MCP tools to the collection...

var agent = azureClient.CreateAIAgent(
    instructions: "You are a helpful assistant",
    tools: combinedTools);
```

## Key Features

### MCP Tool Server Integration

- **Automatic Discovery**: Discovers available MCP tool servers based on configuration
- **Live Validation**: Tests connectivity to MCP servers before integration
- **Tool Conversion**: Converts MCP tools to Microsoft.Extensions.AI AITool format
- **Error Handling**: Robust error handling for server connectivity issues

### Authentication Support

- **Bearer Token**: Simple authentication using bearer tokens
- **Secure Communication**: HTTPS communication with MCP servers
- **Environment-aware SSL**: Development-friendly SSL certificate handling

### Microsoft.Extensions.AI Integration

- **Native Integration**: Seamlessly integrates with Microsoft.Extensions.AI
- **AIAgent Support**: Creates AIAgent instances with combined tools
- **AITool Conversion**: Converts MCP tools to native AITool instances

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