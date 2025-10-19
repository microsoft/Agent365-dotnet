// ✅ COMPLIANT EXAMPLE: Proper Agent construction using providers
using System;
using System.Threading.Tasks;

namespace AnalyzerDemoApp;

// Mock interfaces for demo purposes
public interface IChatClientProvider
{
    object GetChatClient(string tenantId, string workerId);
}

public interface IOpenAIFunctionProvider
{
    object[] GetAvailableTools(string tenantId, string workerId);
    Task<string> ExecuteFunctionAsync(string tenantId, string workerId, string functionName, string parameters);
}

/// <summary>
/// Example of proper Agent construction following governance rules.
/// This demonstrates the correct pattern for multi-tenant scenarios.
/// </summary>
public class CompliantAgent
{
    // ✅ COMPLIANT A365OAI0011: Using providers instead of direct clients
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IOpenAIFunctionProvider _functionProvider;

    public CompliantAgent(
        IChatClientProvider chatClientProvider,  // ✅ Provider injection
        IOpenAIFunctionProvider functionProvider) // ✅ Provider injection
    {
        _chatClientProvider = chatClientProvider;
        _functionProvider = functionProvider;
    }

    public async Task ProcessMessageCorrectly(string tenantId, string workerId)
    {
        if (tenantId == null || workerId == null)
        {
            Console.WriteLine("Tenant context not found");
            return;
        }

        // ✅ COMPLIANT A365OAI0001: Use provider instead of direct ChatClient
        var chatClient = _chatClientProvider.GetChatClient(tenantId, workerId);
        
        // ✅ COMPLIANT A365OAI0006: Use function provider instead of direct tool creation
        var availableTools = _functionProvider.GetAvailableTools(tenantId, workerId);
        
        // ✅ COMPLIANT: No hardcoded tenant/worker IDs
        var functionResult = await _functionProvider.ExecuteFunctionAsync(
            tenantId, workerId, "weather_function", "{\"location\":\"Seattle\"}");
        
        Console.WriteLine($"Response: {functionResult}");
    }
}