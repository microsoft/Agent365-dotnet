// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;  // ❌ Need this for FirstOrDefault in violations
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenAIMultiturn;
using OpenAI;                    // ❌ VIOLATION: Direct OpenAI client access 
using OpenAI.Chat;               // ❌ VIOLATION: Direct ChatClient access - also needed for ChatTool violations
using System.ClientModel;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.AddHttpClient();

// ❌ VIOLATION A365OAI0002: Direct OpenAIClient registration - should use IOpenAIClientProvider
builder.Services.AddSingleton<OpenAIClient>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var apiKey = config.GetSection("AIServices:OpenAI:ApiKey").Value ?? "ollama-demo-key";
    var baseUrl = config.GetSection("AIServices:AzureOpenAI:Endpoint").Value ?? "http://localhost:11434/v1/";
    
    var options = new OpenAIClientOptions();
    options.Endpoint = new Uri(baseUrl);
    return new OpenAIClient(new ApiKeyCredential(apiKey), options);
});

// ❌ VIOLATION A365OAI0001: Direct ChatClient registration - should use governance-approved providers
builder.Services.AddSingleton<ChatClient>(provider =>
{
    // ❌ VIOLATION A365OAI0002: GetRequiredService<OpenAIClient> - direct access to OpenAI client
    var openAIClient = provider.GetRequiredService<OpenAIClient>();
    var config = provider.GetRequiredService<IConfiguration>();
    var modelId = config.GetSection("AIServices:AzureOpenAI:DeploymentName").Value ?? "llama3.2:3b";
    return openAIClient.GetChatClient(modelId);
});

// ❌ VIOLATION DEMO: Simplified configuration without proper governance

// ❌ VIOLATION A365OAI0006: Direct ChatTool creation instead of using IOpenAIFunctionProvider
var weatherTool = ChatTool.CreateFunctionTool("get_weather", "Gets weather information");
var forecastTool = ChatTool.CreateFunctionTool("get_forecast", "Gets weather forecast");

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<MyAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// No MCP tool services needed - we use direct OpenAI function calling

// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "OpenAI Analyzer Demo - Contains Deliberate Violations");

// ❌ VIOLATION A365OAI0004: Route with direct tenant/worker ID access
app.MapPost("/api/violations", async (HttpRequest request, HttpResponse response, CancellationToken cancellationToken) =>
{
    // ❌ VIOLATION A365OAI0004: Direct access to tenant_id claim
    var tenantId = request.HttpContext.User.FindFirst("tenant_id")?.Value;
    
    // ❌ VIOLATION A365OAI0004: Direct access to worker_id header
    var workerId = request.Headers["X-Worker-Id"].FirstOrDefault();
    
    // ❌ VIOLATION A365OAI0004: Direct access to worker_id from Items
    var workerFromItems = request.HttpContext.Items["worker_id"]?.ToString();
    
    // ❌ VIOLATION A365OAI0002: Direct OpenAIClient access in route handler
    var openAIClient = request.HttpContext.RequestServices.GetRequiredService<OpenAIClient>();
    
    // ❌ VIOLATION A365OAI0001: Direct ChatClient access in route handler
    var chatClient = request.HttpContext.RequestServices.GetRequiredService<ChatClient>();
    
    // ❌ VIOLATION A365OAI0009: Hardcoded tenant/worker IDs (examples of what not to do)
    var hardcodedClient = request.HttpContext.RequestServices.GetService<IChatClientProvider>()?.GetChatClient("tenant1", "worker1");
    
    await response.WriteAsync($"Processing message for tenant: {tenantId}, worker: {workerId}");
});

// This receives incoming messages from Azure Bot Service or other SDK Agents
var incomingRoute = app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

if (!app.Environment.IsDevelopment())
{
    incomingRoute.RequireAuthorization();
}
else
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

// ✅ COMPLIANT EXAMPLE: This is how it should be done with providers
// services.AddSingleton<IChatClientProvider>(provider => 
//     new ChatClientProvider((tenantId, workerId) => CreateChatClient(tenantId, workerId)));
// services.AddSingleton<IOpenAIFunctionProvider>(provider =>
//     new OpenAIFunctionProvider((tenantId, workerId) => CreateFunctionContext(tenantId, workerId)));

// ❌ VIOLATION A365OAI0010: Static collection that could store cross-tenant data
public static class ChatHistoryService
{
    private static readonly Dictionary<string, List<string>> _conversationHistory = new();
    private static readonly ConcurrentDictionary<string, object> _userSessions = new();
    
    public static void AddMessage(string conversation, string message)
    {
        // This pattern is dangerous for multi-tenant applications
        if (!_conversationHistory.ContainsKey(conversation))
            _conversationHistory[conversation] = new List<string>();
        _conversationHistory[conversation].Add(message);
    }
}

// Mock interface for demo purposes
public interface IChatClientProvider
{
    object GetChatClient(string tenantId, string workerId);
}
