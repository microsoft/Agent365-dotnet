// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using OpenAI;
using OpenAI.Chat;
using OpenAIMultiturn.Functions;


namespace OpenAIMultiturn.Agents;

public class Agent365Agent
{
    private readonly OpenAIClient _openAIClient; // ❌ VIOLATION A365OAI0002: Direct OpenAIClient field
    private readonly ChatClient _chatClient; // ❌ VIOLATION A365OAI0001: Direct ChatClient field
    private readonly string _modelName;

    private const string AgentName = "Agent365Agent";
    private const string TermsAndConditionsNotAcceptedInstructions = "The user has not accepted the terms and conditions. You must ask the user to accept the terms and conditions before you can help them with any tasks. You may use the 'accept_terms_and_conditions' function to accept the terms and conditions on behalf of the user. If the user tries to perform any action before accepting the terms and conditions, you must use the 'terms_and_conditions_not_accepted' function to inform them that they must accept the terms and conditions to proceed.";
    private const string TermsAndConditionsAcceptedInstructions = "You may ask follow up questions until you have enough information to answer the user's question. You can help with weather information using the get_weather and get_forecast functions.";
    private string AgentInstructions() => $@"
        You are a friendly assistant that helps office workers with their daily tasks.
        {(MyAgent.TermsAndConditionsAccepted ? TermsAndConditionsAcceptedInstructions : TermsAndConditionsNotAcceptedInstructions)}

        Respond in JSON format with the following JSON schema:
        
        {{
            ""contentType"": ""'Text'"",
            ""content"": ""{{The content of the responsein plain text}}""
        }}
        ";

    /// <summary>
    /// Initializes a new instance of the <see cref="Agent365Agent"/> class.
    /// ❌ VIOLATION DEMO: Using direct OpenAI clients instead of IOpenAIClientProvider
    /// ✅ FUNCTIONALITY: Same features as compliant sample but with direct function execution
    /// </summary>
    /// <param name="openAIClient">Direct OpenAI client - VIOLATION for demo.</param>
    /// <param name="chatClient">Direct Chat client - VIOLATION for demo.</param>
    /// <param name="modelName">The name of the OpenAI model to use.</param>
    public Agent365Agent(OpenAIClient openAIClient, ChatClient chatClient, string modelName = "gpt-4") // ❌ VIOLATIONS A365OAI0001/0002: Direct client parameters
    {
        _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient)); // ❌ Using violation parameter
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient)); // ❌ Using violation parameter
        _modelName = modelName;
    }

    /// <summary>
    /// Invokes the agent with the given input and returns the response.
    /// ✅ FUNCTIONALITY: Direct function calling - simpler and more efficient than function manager
    /// ❌ VIOLATIONS: Uses direct client access patterns (single-tenant approach)
    /// </summary>
    /// <param name="input">A message to process.</param>
    /// <param name="chatHistory">The chat history to maintain context.</param>
    /// <returns>An instance of <see cref="Agent365AgentResponse"/></returns>
    public async Task<Agent365AgentResponse> InvokeAgentAsync(string input, List<ChatMessage> chatHistory)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);

        // ❌ VIOLATION A365OAI0001: Using direct ChatClient field instead of IOpenAIClientProvider
        var chatClient = _chatClient;

        // Add user message to chat history
        chatHistory.Add(ChatMessage.CreateUserMessage(input));

        // Add system message if it's the first message
        if (chatHistory.Count == 1)
        {
            chatHistory.Insert(0, ChatMessage.CreateSystemMessage(AgentInstructions()));
        }

        // ✅ FUNCTIONALITY: Get available tools for current state (direct approach)
        var availableTools = GetAvailableTools();

        var chatCompletionOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };
        
        // ✅ FUNCTIONALITY: Add available tools to the chat completion
        foreach (var tool in availableTools)
        {
            chatCompletionOptions.Tools.Add(tool);
        }

        var response = await chatClient.CompleteChatAsync(chatHistory, chatCompletionOptions);
        var assistantMessage = response.Value.Content[0];
        bool hadFunctionCalls = false;
        string lastFunctionResult = "";

        // ✅ FUNCTIONALITY: Handle function calls with direct execution - much simpler than function manager
        if (response.Value.ToolCalls?.Count > 0)
        {
            hadFunctionCalls = true;
            Console.WriteLine($"🔧 FUNCTION CALLS DETECTED: {response.Value.ToolCalls.Count} tool calls");
            chatHistory.Add(ChatMessage.CreateAssistantMessage(response.Value.ToolCalls));

            foreach (var toolCall in response.Value.ToolCalls)
            {
                if (toolCall is ChatToolCall functionCall)
                {
                    Console.WriteLine($"🎯 EXECUTING FUNCTION: {functionCall.FunctionName}");
                    Console.WriteLine($"📝 Function Arguments: {functionCall.FunctionArguments}");
                    
                    // ✅ DIRECT FUNCTION EXECUTION: No manager abstraction needed
                    var functionResult = ExecuteFunction(functionCall.FunctionName, functionCall.FunctionArguments?.ToString() ?? "");
                    Console.WriteLine($"✅ FUNCTION RESULT: {functionResult}");
                    
                    // Store the last function result for fallback
                    lastFunctionResult = functionResult;
                    
                    chatHistory.Add(ChatMessage.CreateToolMessage(toolCall.Id, functionResult));
                }
            }

            // ✅ FUNCTIONALITY: Get final response after function execution
            Console.WriteLine($"🔄 GETTING FINAL RESPONSE AFTER FUNCTION EXECUTION...");
            
            // Add a helpful system message to guide the response after function calls
            chatHistory.Add(ChatMessage.CreateSystemMessage("Please provide a helpful response to the user based on the function results above. Be conversational and helpful."));
            
            // Don't force JSON format for follow-up response - let OpenAI respond naturally
            var finalOptions = new ChatCompletionOptions();
            foreach (var tool in GetAvailableTools())
            {
                finalOptions.Tools.Add(tool);
            }
            
            response = await chatClient.CompleteChatAsync(chatHistory, finalOptions);
            assistantMessage = response.Value.Content[0];
            Console.WriteLine($"✅ GOT FINAL RESPONSE FROM OPENAI");
            Console.WriteLine($"🔍 FINAL RESPONSE CONTENT: '{assistantMessage.Text}'");
            Console.WriteLine($"📊 RESPONSE LENGTH: {assistantMessage.Text?.Length ?? 0} characters");
            Console.WriteLine($"📈 TOTAL CONTENT PARTS: {response.Value.Content.Count}");
        }

        chatHistory.Add(ChatMessage.CreateAssistantMessage(assistantMessage.Text));

        // Handle response based on whether we had function calls
        if (hadFunctionCalls)
        {
            // After function calls, OpenAI returns natural language response
            Console.WriteLine($"🗣️ RETURNING NATURAL LANGUAGE RESPONSE AFTER FUNCTION CALL");
            
            // Handle empty responses from OpenAI after function calls
            var finalContent = assistantMessage.Text;
            if (string.IsNullOrWhiteSpace(finalContent))
            {
                Console.WriteLine($"⚠️ EMPTY RESPONSE FROM OPENAI - CREATING SMART FALLBACK WITH FUNCTION RESULT");
                
                // Use the actual function result in the fallback
                if (!string.IsNullOrWhiteSpace(lastFunctionResult))
                {
                    finalContent = lastFunctionResult;
                }
                else
                {
                    finalContent = "I've processed your request using the available tools.";
                }
            }
            
            return new Agent365AgentResponse
            {
                Content = finalContent,
                ContentType = Agent365AgentResponseContentType.Text
            };
        }

        // Parse the JSON response for initial requests
        Console.WriteLine($"🔍 RAW ASSISTANT RESPONSE: {assistantMessage.Text}");
        try
        {
            var jsonNode = JsonNode.Parse(assistantMessage.Text);
            Console.WriteLine($"📋 PARSED JSON SUCCESSFULLY");
            
            var content = jsonNode!["content"]!.ToString();
            var contentTypeStr = jsonNode["contentType"]!.ToString();
            
            Console.WriteLine($"📝 CONTENT: {content}");
            Console.WriteLine($"🏷️ CONTENT TYPE: {contentTypeStr}");
            
            return new Agent365AgentResponse
            {
                Content = content,
                ContentType = Enum.Parse<Agent365AgentResponseContentType>(contentTypeStr, true)
            };
        }
        catch (Exception je)
        {
            Console.WriteLine($"❌ JSON PARSING ERROR: {je.Message}");
            Console.WriteLine($"🔄 RETRYING WITH ERROR MESSAGE...");
            // Retry with error message
            return await InvokeAgentAsync($"That response did not match the expected format. Please try again. Error: {je.Message}", chatHistory);
        }
    }

    /// <summary>
    /// Get available tools based on current state - direct implementation without function manager
    /// </summary>
    private List<ChatTool> GetAvailableTools()
    {
        var tools = new List<ChatTool>();

        if (MyAgent.TermsAndConditionsAccepted)
        {
            // Add A365 tools when terms are accepted - user can reject terms
            tools.Add(TermsAndConditionsAcceptedFunctions.RejectTermsAndConditionsFunction);
            
            // Add weather functions when terms are accepted
            tools.Add(WeatherFunctions.GetWeatherFunction);
            tools.Add(WeatherFunctions.GetForecastFunction);
            
            Console.WriteLine("🌤️ Weather functions available (terms accepted)");
        }
        else
        {
            // Add terms acceptance tools when terms are not accepted
            tools.Add(TermsAndConditionsNotAcceptedFunctions.AcceptTermsAndConditionsFunction);
            tools.Add(TermsAndConditionsNotAcceptedFunctions.TermsAndConditionsNotAcceptedFunction);
            
            Console.WriteLine("📋 Only terms functions available (terms not accepted)");
        }

        Console.WriteLine($"🔧 Total tools available: {tools.Count}");
        return tools;
    }

    /// <summary>
    /// Execute function call directly - simpler than function manager abstraction
    /// </summary>
    private string ExecuteFunction(string functionName, string arguments = "")
    {
        Console.WriteLine($"🎯 ExecuteFunction called: {functionName}");
        Console.WriteLine($"📝 Arguments: {arguments}");
        
        return functionName switch
        {
            "accept_terms_and_conditions" => ExecuteAcceptTermsAndConditions(),
            "reject_terms_and_conditions" => ExecuteRejectTermsAndConditions(),
            "terms_and_conditions_not_accepted" => ExecuteTermsAndConditionsNotAccepted(),
            "get_weather" => ExecuteGetWeather(arguments),
            "get_forecast" => ExecuteGetForecast(arguments),
            _ => $"Unknown function: {functionName}"
        };
    }

    private string ExecuteAcceptTermsAndConditions()
    {
        MyAgent.TermsAndConditionsAccepted = true;
        return "Terms and conditions accepted. Thank you.";
    }

    private string ExecuteRejectTermsAndConditions()
    {
        MyAgent.TermsAndConditionsAccepted = false;
        return "Terms and conditions rejected. You can accept later to proceed.";
    }

    private string ExecuteTermsAndConditionsNotAccepted()
    {
        return "You must accept the terms and conditions to proceed.";
    }

    private string ExecuteGetWeather(string arguments)
    {
        Console.WriteLine($"🌤️ ExecuteGetWeather called with: {arguments}");
        
        try
        {
            var args = JsonDocument.Parse(arguments);
            var location = args.RootElement.GetProperty("location").GetString() ?? "Unknown";
            var unit = "fahrenheit";
            
            if (args.RootElement.TryGetProperty("unit", out var unitProperty))
            {
                unit = unitProperty.GetString() ?? "fahrenheit";
            }

            // Mock weather data with some variety
            var random = new Random();
            var baseTemp = unit == "celsius" ? 22 : 72;
            var tempVariation = random.Next(-5, 6);
            var actualTemp = baseTemp + tempVariation;
            
            var conditions = new[] { "Sunny", "Partly Cloudy", "Cloudy", "Light Rain" };
            var condition = conditions[random.Next(conditions.Length)];
            
            var temperature = unit == "celsius" ? $"{actualTemp}°C" : $"{actualTemp}°F";
            var humidity = $"{random.Next(40, 80)}%";
            var windSpeed = $"{random.Next(5, 20)} mph";

            var result = $"Current weather in {location}: {temperature}, {condition}. Humidity: {humidity}, Wind: {windSpeed}";
            Console.WriteLine($"✅ Weather generated for {location}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Weather function error: {ex.Message}");
            return "Sorry, I couldn't get the weather information right now.";
        }
    }

    private string ExecuteGetForecast(string arguments)
    {
        Console.WriteLine($"📅 ExecuteGetForecast called with: {arguments}");
        
        try
        {
            var args = JsonDocument.Parse(arguments);
            var location = args.RootElement.GetProperty("location").GetString() ?? "Unknown";
            var days = 3;
            
            if (args.RootElement.TryGetProperty("days", out var daysProperty))
            {
                // Handle both string and number types for days
                if (daysProperty.ValueKind == JsonValueKind.String)
                {
                    if (int.TryParse(daysProperty.GetString(), out var parsedDays))
                    {
                        days = parsedDays;
                    }
                }
                else if (daysProperty.ValueKind == JsonValueKind.Number)
                {
                    days = daysProperty.GetInt32();
                }
            }

            // Ensure days is within valid range
            days = Math.Max(1, Math.Min(7, days));

            // Mock forecast data
            var forecast = $"{days}-day forecast for {location}:\n";
            for (int i = 0; i < days; i++)
            {
                var dayName = i == 0 ? "Today" : i == 1 ? "Tomorrow" : $"Day {i + 1}";
                var temp = 72 + (i % 3) * 3; // Vary temperature slightly
                var condition = i % 2 == 0 ? "Sunny" : "Partly Cloudy";
                forecast += $"{dayName}: {temp}°F, {condition}\n";
            }

            Console.WriteLine($"✅ Forecast generated for {location}, {days} days");
            return forecast.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Forecast function error: {ex.Message}");
            return "Sorry, I couldn't get the forecast information right now.";
        }
    }
}
