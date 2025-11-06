# Microsoft Agents A365 SDK - AgentFramework Tooling

This library provides integration between Microsoft Agents A365 and Microsoft Extensions AI (AIAgent), enabling you to add MCP (Model Context Protocol) tool servers to your AI agents.

## Installation

Add the necessary project references or NuGet packages to your project.

## Setup and Configuration

### Manual Service Registration

Since the library doesn't currently include built-in extension methods, you'll need to register the services manually in your dependency injection container.

#### Required Namespaces

```csharp
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
```

#### Service Registration

Register the following services manually:
- `IMcpServerConfigurationService` → `McpServerConfigurationService` (from Core project)
- `IMcpToolRegistrationService` → `McpToolRegistrationService` (from AgentFramework project)

## Usage Examples

### 1. ASP.NET Core Application

In your `Program.cs` file:

```csharp
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Services;
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

// Register MCP services manually
builder.Services.AddScoped<IMcpServerConfigurationService, McpServerConfigurationService>();
builder.Services.AddScoped<IMcpToolRegistrationService, McpToolRegistrationService>();

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
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Services;
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
        
        // Register MCP services manually
        services.AddScoped<IMcpServerConfigurationService, McpServerConfigurationService>();
        services.AddScoped<IMcpToolRegistrationService, McpToolRegistrationService>();
        
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
        
        // Create chat client from Azure OpenAI client
        var chatClient = _azureOpenAIClient.GetChatClient("your-deployment-name").AsIChatClient();
        
        // Add MCP tools to create an AIAgent
        var agent = await _mcpToolRegistrationService.AddToolServersToAgent(
            chatClient,
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

// Get chat client from Azure OpenAI client
var chatClient = azureClient.GetChatClient("your-deployment-name").AsIChatClient();

// Define initial tools (if any)
var initialTools = new List<AITool>();

// Get the MCP tool registration service (from DI or create manually)
var mcpService = serviceProvider.GetRequiredService<IMcpToolRegistrationService>();

// Add MCP tools to create an AIAgent
var agent = await mcpService.AddToolServersToAgent(
    chatClient,
    agentInstructions: "You are a helpful assistant with access to external tools.",
    initialTools: initialTools,
    agentUserId: "user123",
    authToken: "your-auth-token");

Console.WriteLine("AIAgent created with MCP tools integrated");
```

### 4. Simple Usage with Nullable Parameters

```csharp
// The service supports nullable parameters for flexibility

// With just MCP tools (no initial tools or instructions)
var agent = await mcpService.AddToolServersToAgent(
    chatClient,
    agentInstructions: null,           // No specific instructions
    initialTools: null,               // No initial tools
    agentUserId: "user123",
    authToken: "your-auth-token");

// With instructions but no initial tools
var agent = await mcpService.AddToolServersToAgent(
    chatClient,
    agentInstructions: "You are a helpful assistant.",
    initialTools: null,               // No initial tools
    agentUserId: "user123",
    authToken: "your-auth-token");
```

### 5. Manual Service Instantiation

If you're not using dependency injection, you can create services manually:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Services;

// Create logger
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<McpToolRegistrationService>();

// Create configuration service
var configService = new McpServerConfigurationService(/* required dependencies */);

// Create MCP tool registration service
var mcpService = new McpToolRegistrationService(logger, configService);

// Use the service
var chatClient = azureClient.GetChatClient("your-deployment").AsIChatClient();
var agent = await mcpService.AddToolServersToAgent(
    chatClient,
    "You are a helpful assistant",
    new List<AITool>(),
    "userId",
    "envId", 
    "authToken");
```

## Service Interfaces

### IMcpServerConfigurationService

Responsible for managing MCP server configurations.

```csharp
public interface IMcpServerConfigurationService
{
    Task<List<MCPServerConfig>> ListToolServers(
        string agentUserId,
        string authToken);
}
```

### IMcpToolRegistrationService

Responsible for registering MCP tools with Microsoft.Extensions.AI.

```csharp
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Creates a new AIAgent from the provided IChatClient with MCP tools added to existing tools.
    /// Returns the new agent instance configured with existing tools plus MCP tools.
    /// </summary>
    Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        string? authToken = null);
}
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
  - Base URL for the current environment

**URL Construction:**
The library builds full URLs like:
```
{BaseURL}/agents/servers/{ServerName}
```

**Environment-Based Base URLs:**
- **Development**: `https://localhost:8080/agents/servers`
- **Test**: `https://test.agent365.svc.cloud.dev.microsoft/agents/servers`
- **Staging**: `https://staging.agent365.svc.cloud.microsoft/agents/servers`
- **Production**: `https://agent365.svc.cloud.microsoft/agents/servers`

**Example:**
For server name `mcp_MailTools` in Test environment:
```
https://test.agent365.svc.cloud.dev.microsoft/agents/servers/mcp_MailTools
```

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

1. **Service Registration**: Always register MCP services in your DI container using the shown patterns
2. **Error Handling**: Implement proper error handling around MCP tool registration
3. **Logging**: Enable appropriate logging levels to monitor MCP server interactions
4. **Authentication**: Use full authentication context when available for better security
5. **Testing**: Test MCP server connectivity in development environments
6. **IChatClient Usage**: Always use `IChatClient` interface, not concrete client implementations

## Troubleshooting

### Common Issues

1. **MCP Server Connectivity**: Ensure MCP servers are accessible from your application
2. **Authentication**: Verify authentication tokens and permissions
3. **Tool Registration**: Check logs for tool registration failures
4. **Agent Configuration**: Ensure agents are properly configured before adding tools
5. **Missing Dependencies**: Ensure all required packages are installed and services are registered

### Debugging

Enable detailed logging to troubleshoot issues:

```csharp
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Debug);
});
```

### Common Integration Patterns

#### Using with Existing Tools

```csharp
// If you already have tools defined
var existingTools = new List<AITool>
{
    AIFunctionFactory.Create((string city) => $"Weather in {city}"),
    // ... other tools
};

// MCP tools will be added to these existing tools
var agent = await mcpService.AddToolServersToAgent(
    chatClient,
    "You are a helpful assistant with weather and MCP tools.",
    existingTools,
    "userId",
    "envId",
    "authToken");
```

#### Error Handling Example

```csharp
try
{
    var agent = await mcpService.AddToolServersToAgent(
        chatClient,
        instructions,
        tools,
        userId,
        envId,
        authToken);
    
    // Use the agent
    var response = await agent.CompleteAsync("Hello!");
}
catch (InvalidOperationException ex)
{
    // Handle MCP server connection issues
    logger.LogError(ex, "Failed to connect to MCP servers");
}
catch (ArgumentException ex)
{
    // Handle configuration issues
    logger.LogError(ex, "Invalid MCP configuration");
}
```

## Important Notes

1. **Required Dependencies**: 
   - Ensure you have logging configured since the MCP services depend on `ILogger<T>`
   - Azure.AI.OpenAI is required for `AzureOpenAIClient`
   - Microsoft.Extensions.AI is required for `IChatClient` and `AIAgent`
2. **Service Lifetimes**: Services are typically registered with `Scoped` lifetime
3. **SSL in Development**: SSL certificate validation is disabled in development mode for local testing
4. **Authentication**: Services use Bearer token authentication for MCP server communication
5. **Immutable Agents**: `AIAgent` instances are immutable; the service creates new instances with tools
6. **Tool Limitations**: Tool names are limited to 64 characters (server name + tool name + separator)

## Migration Notes

This library creates `AIAgent` instances using Microsoft.Extensions.AI patterns. If you're migrating from other agent frameworks:

1. Replace direct client dependencies with `IChatClient`
2. Use the service to create agents with MCP tools rather than manual tool registration
3. Handle the async nature of agent creation
4. Update error handling to catch the specific exceptions thrown by this library