# Microsoft Agent 365 SDK - AzureFoundry Tooling

This library provides integration between Microsoft Agent 365 and Azure AI Foundry, enabling you to add MCP (Model Context Protocol) tool servers to your Azure Foundry agents using the Persistent Agents client.

## Installation

Add the necessary project references or NuGet packages to your project.

## Setup and Configuration

### Adding MCP Services to Dependency Injection

The library provides an extension method `AddMcpServices()` to register all required services with the dependency injection container.

#### Required Namespace

```csharp
using Microsoft.Agents.A365.Tooling.AzureFoundry.Extensions;
```

#### Service Registration

The extension method registers the following services:
- `IMcpServerConfigurationService` → `McpServerConfigurationService` (from Common project)
- `IMcpToolRegistrationService` → `McpToolRegistrationService` (from AzureFoundry project)

## Usage Examples

### 1. ASP.NET Core Application

In your `Program.cs` file:

```csharp
using Microsoft.Agents.A365.Tooling.AzureFoundry.Extensions;

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
using Microsoft.Agents.A365.Tooling.AzureFoundry.Extensions;
using Microsoft.Agents.A365.Tooling.Common.Services;
using Microsoft.Agents.A365.Tooling.AzureFoundry.Services;
using Azure.AI.Agents.Persistent;

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
            .ListToolServers("userId", "envId", "authToken");
            
        // Create Azure Foundry agent client and configure with MCP tools
        var agentClient = new PersistentAgentsClient(/* your configuration */);
        _mcpToolRegistrationService
            .AddToolServersToAgent(agentClient, "userId", "envId", "authToken");
            
        // Use the agent client with MCP tools...
    }
}
```

### 3. Manual Service Collection Setup

If you need to manually create and configure a service collection:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Tooling.AzureFoundry.Extensions;

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
var servers = await mcpConfigService.ListToolServers(
    "agentInstance",
    "authToken");
```

### 4. Class Library Integration

If you're building a class library that uses these services:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Tooling.AzureFoundry.Extensions;

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
    Task<List<MCPServerConfig>> ListToolServers(
        string agentInstance,
        string authToken);
}
```

### IMcpToolRegistrationService

Responsible for registering MCP tools with Azure Foundry Persistent Agents.

```csharp
public interface IMcpToolRegistrationService
{
    void AddToolServersToAgent(
        PersistentAgentsClient agentClient,
        string agentInstanceId,
        string? authToken = null);

    void AddToolServersToAgent(
        PersistentAgentsClient agentClient,
        UserAuthorization userAuthorization,
        ITurnContext turnContext,
        string? authToken = null);

    Task<(IList<MCPToolDefinition> ToolDefinitions, ToolResources? ToolResources)> GetMcpToolDefinitionsAndResourcesAsync(
        string agenticAppId,
        string authToken);
}
```

### 5. Azure Foundry Agent Integration

Complete example of setting up an Azure Foundry agent with MCP tools:

```csharp
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Tooling.AzureFoundry.Extensions;
using Microsoft.Agents.A365.Tooling.AzureFoundry.Services;

public class FoundryAgentExample
{
    private readonly IMcpToolRegistrationService _mcpToolRegistrationService;
    private readonly ILogger<FoundryAgentExample> _logger;

    public FoundryAgentExample(
        IMcpToolRegistrationService mcpToolRegistrationService,
        ILogger<FoundryAgentExample> logger)
    {
        _mcpToolRegistrationService = mcpToolRegistrationService;
        _logger = logger;
    }

    public async Task<string> ProcessRequestAsync(string userMessage, string authToken, UserAuthorization userAuthorization, ITurnContext turnContext)
    {
        // Create Azure AI Project client
        var projectEndpoint = Environment.GetEnvironmentVariable("PROJECT_ENDPOINT");
        var projectClient = new AIProjectClient(new Uri(projectEndpoint), new DefaultAzureCredential());
        var agentClient = projectClient.GetPersistentAgentsClient();

        try
        {
            // Create agent with MCP tools
            var agent = agentClient.Administration.CreateAgent(
                model: "gpt-4o",
                name: "mcp-enabled-agent",
                instructions: "You are a helpful assistant with access to MCP tools.");

            // Add MCP tool servers to the agent (agenticAppId extracted from turnContext)
            _mcpToolRegistrationService.AddToolServersToAgent(
                agentClient,
                userAuthorization,
                turnContext,
                authToken);

            // Create thread and send message
            var thread = await agentClient.Threads.CreateThreadAsync();
            await agentClient.Messages.CreateMessageAsync(thread.Id, MessageRole.User, userMessage);

            // Run the agent
            var run = await agentClient.Runs.CreateRunAsync(thread.Id, agent.Id);

            // Wait for completion and handle tool calls
            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction)
            {
                await Task.Delay(500);
                run = agentClient.Runs.GetRun(thread.Id, run.Id);

                if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolOutputsAction submitAction)
                {
                    var toolOutputs = new List<ToolOutput>();
                    foreach (var toolCall in submitAction.ToolCalls)
                    {
                        // Tool execution is handled automatically by MCP infrastructure
                        _logger.LogInformation($"Executing tool: {toolCall.Id}");
                    }
                }
            }

            // Get the response
            var messages = agentClient.Messages.GetMessagesAsync(thread.Id, order: ListSortOrder.Ascending);
            var response = string.Empty;
            
            await foreach (var message in messages)
            {
                if (message.Role == MessageRole.Assistant)
                {
                    foreach (var content in message.ContentItems)
                    {
                        if (content is MessageTextContent textContent)
                        {
                            response = textContent.Text;
                            break;
                        }
                    }
                }
            }

            // Cleanup
            agentClient.Threads.DeleteThread(thread.Id);
            agentClient.Administration.DeleteAgent(agent.Id);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request with Foundry agent");
            throw;
        }
    }
}

// Service registration in Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddMcpServices();
builder.Services.AddScoped<FoundryAgentExample>();

var app = builder.Build();
```

## Azure Foundry Configuration

### Prerequisites

Before using this library, ensure you have:

1. **Azure AI Foundry Project**: Create a project in Azure AI Foundry
2. **Project Endpoint**: Set the `PROJECT_ENDPOINT` environment variable
3. **Authentication**: Configure Azure credentials (DefaultAzureCredential)
4. **Model Deployment**: Deploy the required model (e.g., gpt-4o) in your project

```csharp
// Example environment configuration
Environment.SetEnvironmentVariable("PROJECT_ENDPOINT", "https://your-project.cognitiveservices.azure.com/");
```

### Agent Configuration

The `IMcpToolRegistrationService` integrates with Azure Foundry's `PersistentAgentsClient` to:

1. **Discover MCP Tools**: Automatically finds and validates configured MCP servers
2. **Create Tool Definitions**: Generates `MCPToolDefinition` objects for each server
3. **Configure Tool Resources**: Sets up `ToolResources` with proper authentication headers
4. **Update Agent**: Applies the MCP tools to your Persistent Agent

**Key Features:**
- **Automatic Tool Discovery**: No manual tool configuration required
- **Authentication Handling**: Automatic Bearer token header
- **Error Resilience**: Graceful handling of server connection issues
- **Governance Compliance**: Built-in approval controls and validation

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

## Important Notes

1. **Required Dependencies**: 
   - Ensure you have logging configured since the MCP services depend on `ILogger<T>`
   - Azure AI Projects SDK is required for `PersistentAgentsClient`
   - Azure.Identity is required for authentication
2. **Service Lifetimes**: Services are registered with `Scoped` lifetime
3. **SSL in Development**: SSL certificate validation is disabled in development mode for local testing
4. **Authentication**: Services use Bearer token authentication for MCP server communication
5. **Agent Lifecycle**: MCP tools must be added before creating agent runs. Tools cannot be added to existing agents after creation.
6. **Tool Resources**: The service manages tool resources automatically, including authentication headers and approval settings

## Troubleshooting

### Common Issues

1. **Missing Logging Configuration**: Ensure you've added logging services before calling `AddMcpServices()`
2. **ToolingManifest.json Not Found**: In development mode, ensure the manifest file is in the correct location and set to copy to output directory
3. **Authentication Errors**: Verify that the provided auth token is valid and has the necessary permissions
4. **Project Endpoint Not Set**: Ensure `PROJECT_ENDPOINT` environment variable is configured
5. **Agent Creation Issues**: Verify your Azure AI Foundry project has the required model deployed
6. **Tool Server Connection**: Check network connectivity and MCP server availability

### Logging

The services log important information including:
- MCP server discovery and configuration
- SSL certificate validation warnings
- Tool registration success/failure

Enable appropriate log levels to see these messages during development and troubleshooting.
