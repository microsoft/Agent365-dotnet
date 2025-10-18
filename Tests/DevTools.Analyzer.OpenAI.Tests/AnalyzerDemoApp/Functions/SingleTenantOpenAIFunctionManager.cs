// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using OpenAI;
using OpenAI.Chat;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenAIMultiturn.Functions;

/// <summary>
/// ❌ VIOLATION DEMO: Single-tenant function manager using direct OpenAI client access
/// This demonstrates the "wrong" pattern for multi-tenant applications but is perfectly
/// valid for single-tenant apps.
/// </summary>
public class SingleTenantOpenAIFunctionManager
{
    private readonly OpenAIClient _openAIClient; // ❌ VIOLATION A365OAI0002: Direct OpenAIClient field
    private readonly ChatClient _chatClient;     // ❌ VIOLATION A365OAI0001: Direct ChatClient field
    private readonly Dictionary<string, System.Func<Task<string>>> _functionExecutors;

    /// <summary>
    /// ❌ VIOLATION DEMO: Constructor takes direct OpenAI clients instead of providers
    /// </summary>
    public SingleTenantOpenAIFunctionManager(OpenAIClient openAIClient, ChatClient chatClient) // ❌ VIOLATIONS A365OAI0001/0002: Direct client parameters
    {
        _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient)); // ❌ Using violation parameter
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient)); // ❌ Using violation parameter
        
        // ✅ Functionality: Same function registration as the compliant sample
        _functionExecutors = new Dictionary<string, System.Func<Task<string>>>
        {
            ["accept_terms_and_conditions"] = TermsAndConditionsNotAcceptedFunctions.ExecuteAcceptTermsAndConditionsAsync,
            ["reject_terms_and_conditions"] = TermsAndConditionsAcceptedFunctions.ExecuteRejectTermsAndConditionsAsync,
            ["terms_and_conditions_not_accepted"] = TermsAndConditionsNotAcceptedFunctions.ExecuteTermsAndConditionsNotAcceptedAsync
        };
    }

    /// <summary>
    /// Get all available function tools based on current state - SAME logic as compliant sample
    /// </summary>
    public List<ChatTool> GetAvailableTools()
    {
        var tools = new List<ChatTool>();

        if (MyAgent.TermsAndConditionsAccepted)
        {
            // Add A365 tools when terms are accepted - user can reject terms
            tools.Add(TermsAndConditionsAcceptedFunctions.RejectTermsAndConditionsFunction);
        }
        else
        {
            // Add terms acceptance tools when terms are not accepted
            tools.Add(TermsAndConditionsNotAcceptedFunctions.AcceptTermsAndConditionsFunction);
            tools.Add(TermsAndConditionsNotAcceptedFunctions.TermsAndConditionsNotAcceptedFunction);
        }

        return tools;
    }

    /// <summary>
    /// Execute a function call by name - SAME logic as compliant sample
    /// </summary>
    public async Task<string> ExecuteFunctionAsync(string functionName)
    {
        if (_functionExecutors.TryGetValue(functionName, out var executor))
        {
            return await executor();
        }

        return $"Unknown function: {functionName}";
    }

    /// <summary>
    /// ❌ VIOLATION DEMO: Method that uses direct OpenAI client access
    /// This shows why the analyzers flag direct client usage - no tenant isolation
    /// </summary>
    public async Task<string> ExecuteFunctionWithDirectClientAsync(string functionName, string userMessage)
    {
        // ❌ VIOLATION A365OAI0001: Direct usage of ChatClient field
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage("You are helping execute a function call"),
            ChatMessage.CreateUserMessage(userMessage)
        };

        var response = await _chatClient.CompleteChatAsync(messages); // ❌ Direct client usage
        return response.Value.Content[0].Text;
    }
}