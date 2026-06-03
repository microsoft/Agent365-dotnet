// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Extensions.OpenAI;
using OpenAI.Chat;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Extensions.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="OpenAISpanProcessor"/> running against real Azure OpenAI spans.
/// The tests wire up the full OTel pipeline: OpenAI SDK → TracerProvider → OpenAISpanProcessor → captured spans.
/// This lets us assert on the span attributes after the processor runs and test any mapping logic.
/// Requires: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT env vars.
/// </summary>
[TestClass]
public class OpenAISpanProcessorTests
{
    private static readonly JsonSerializerOptions JsonPrint = new() { WriteIndented = true };

    private static string? Endpoint => Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
    private static string? ApiKey => Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
    private static string? Deployment => Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

    private static bool HasCredentials =>
        !string.IsNullOrEmpty(Endpoint) &&
        !string.IsNullOrEmpty(ApiKey) &&
        !string.IsNullOrEmpty(Deployment);

    private List<Activity> _exportedActivities = new();
    private TracerProvider? _tracerProvider;

    [TestInitialize]
    public void Setup()
    {
        AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);

        _exportedActivities = new List<Activity>();

        // Wire up the full pipeline: OpenAI source → OpenAISpanProcessor → capture exporter
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("OpenAI.*")
            .AddProcessor(new OpenAISpanProcessor())
            .AddProcessor(new SimpleActivityExportProcessor(new ActivityCapturingExporter(_exportedActivities)))
            .Build();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _tracerProvider?.Dispose();
    }

    [TestMethod]
    public async Task SimpleChat_ProcessorReceivesSpanWithExpectedAttributes()
    {
        SkipIfNoCredentials();

        var chatClient = CreateChatClient();
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful assistant. Reply in one sentence."),
            new UserChatMessage("What is the capital of France?")
        };

        await chatClient.CompleteChatAsync(messages);

        // Force flush to ensure processor has run
        _tracerProvider!.ForceFlush();

        _exportedActivities.Should().HaveCount(1);
        var activity = _exportedActivities[0];

        DumpActivity(activity, "SimpleChat — after OpenAISpanProcessor");

        // Verify the processor received a valid OpenAI span
        activity.Source.Name.Should().StartWith("OpenAI");
        activity.Kind.Should().Be(ActivityKind.Client);

        // Attributes present after processor
        var tags = GetTags(activity);
        tags.Should().ContainKey("gen_ai.system").WhoseValue.Should().Be("openai");
        tags.Should().ContainKey("gen_ai.operation.name").WhoseValue.Should().Be("chat");
        tags.Should().ContainKey("gen_ai.request.model");
        tags.Should().ContainKey("gen_ai.response.model");
        tags.Should().ContainKey("gen_ai.response.id");
        tags.Should().ContainKey("gen_ai.usage.input_tokens");
        tags.Should().ContainKey("gen_ai.usage.output_tokens");
        tags.Should().ContainKey("gen_ai.response.finish_reasons");
        tags.Should().ContainKey("server.address");
        tags.Should().ContainKey("server.port");

        // Current gap: processor does not add message content
        tags.Should().NotContainKey("gen_ai.input.messages");
        tags.Should().NotContainKey("gen_ai.output.messages");
    }

    [TestMethod]
    public async Task ChatWithToolCall_ProcessorReceivesToolCallSpan()
    {
        SkipIfNoCredentials();

        var chatClient = CreateChatClient();
        var weatherTool = ChatTool.CreateFunctionTool(
            functionName: "get_weather",
            functionDescription: "Get the current weather for a location",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "location": { "type": "string", "description": "City name" }
                },
                "required": ["location"]
            }
            """));

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a weather assistant. Always use the get_weather tool."),
            new UserChatMessage("What's the weather in Seattle?")
        };

        var options = new ChatCompletionOptions { Tools = { weatherTool } };
        await chatClient.CompleteChatAsync(messages, options);

        _tracerProvider!.ForceFlush();

        _exportedActivities.Should().HaveCount(1);
        var activity = _exportedActivities[0];

        DumpActivity(activity, "ChatWithToolCall — after OpenAISpanProcessor");

        var tags = GetTags(activity);
        tags.Should().ContainKey("gen_ai.operation.name").WhoseValue.Should().Be("chat");

        var finishReasons = activity.GetTagItem("gen_ai.response.finish_reasons") as string[];
        finishReasons.Should().Contain("tool_calls");

        // Current gap: no message/tool call content captured
        tags.Should().NotContainKey("gen_ai.input.messages");
        tags.Should().NotContainKey("gen_ai.output.messages");
    }

    [TestMethod]
    public async Task ToolCallRoundTrip_ProcessorHandlesMultiTurnConversation()
    {
        SkipIfNoCredentials();

        var chatClient = CreateChatClient();
        var weatherTool = ChatTool.CreateFunctionTool(
            functionName: "get_weather",
            functionDescription: "Get the current weather for a location",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "location": { "type": "string", "description": "City name" }
                },
                "required": ["location"]
            }
            """));

        // First call: trigger tool call
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a weather assistant. Always use the get_weather tool."),
            new UserChatMessage("What's the weather in Seattle?")
        };

        var options = new ChatCompletionOptions { Tools = { weatherTool } };
        var response1 = await chatClient.CompleteChatAsync(messages, options);
        response1.Value.ToolCalls.Should().NotBeEmpty("model should request tool call");

        _exportedActivities.Clear();

        // Second call: provide tool response and get final answer
        var assistantMessage = new AssistantChatMessage(response1.Value);
        var toolCallId = response1.Value.ToolCalls[0].Id;
        var toolResponse = new ToolChatMessage(toolCallId, "Sunny, 72°F, light breeze from the west");

        var followUp = new List<ChatMessage>
        {
            new SystemChatMessage("You are a weather assistant."),
            new UserChatMessage("What's the weather in Seattle?"),
            assistantMessage,
            toolResponse
        };

        await chatClient.CompleteChatAsync(followUp);

        _tracerProvider!.ForceFlush();

        _exportedActivities.Should().HaveCount(1);
        var activity = _exportedActivities[0];

        DumpActivity(activity, "ToolCallRoundTrip — after OpenAISpanProcessor");

        var tags = GetTags(activity);
        tags.Should().ContainKey("gen_ai.operation.name").WhoseValue.Should().Be("chat");
        tags.Should().ContainKey("gen_ai.usage.input_tokens");
        tags.Should().ContainKey("gen_ai.usage.output_tokens");

        var finishReasons = activity.GetTagItem("gen_ai.response.finish_reasons") as string[];
        finishReasons.Should().Contain("stop");

        // Current gap: multi-turn messages with tool call/response not captured
        tags.Should().NotContainKey("gen_ai.input.messages");
        tags.Should().NotContainKey("gen_ai.output.messages");
    }

    #region Helpers

    private ChatClient CreateChatClient()
    {
        var client = new AzureOpenAIClient(
            new Uri(Endpoint!),
            new ApiKeyCredential(ApiKey!));
        return client.GetChatClient(Deployment!);
    }

    private static void SkipIfNoCredentials()
    {
        if (!HasCredentials)
        {
            Assert.Inconclusive(
                "Skipped: set AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT env vars to run.");
        }
    }

    private static Dictionary<string, object?> GetTags(Activity activity)
    {
        return activity.TagObjects.ToDictionary(t => t.Key, t => t.Value);
    }

    private static void DumpActivity(Activity activity, string label)
    {
        Console.WriteLine($"\n=== {label} ===");
        Console.WriteLine($"  Source: {activity.Source.Name}  Kind: {activity.Kind}  Duration: {activity.Duration}");

        Console.WriteLine("  Attributes:");
        foreach (var tag in activity.TagObjects)
            Console.WriteLine($"    {tag.Key} = {FormatValue(tag.Value)}");

        if (activity.Events.Any())
        {
            Console.WriteLine($"  Events ({activity.Events.Count()}):");
            foreach (var ev in activity.Events)
            {
                Console.WriteLine($"    '{ev.Name}'");
                foreach (var attr in ev.Tags)
                    Console.WriteLine($"      {attr.Key} = {FormatValue(attr.Value)}");
            }
        }

        Console.WriteLine("===\n");
    }

    private static string FormatValue(object? value)
    {
        string val = value switch
        {
            string s => s,
            string[] arr => $"[{string.Join(", ", arr)}]",
            null => "(null)",
            _ => value.ToString() ?? "(null)"
        };

        if (val.Length > 120)
        {
            try
            {
                var doc = JsonDocument.Parse(val);
                val = "\n      " + JsonSerializer.Serialize(doc.RootElement, JsonPrint).Replace("\n", "\n      ");
            }
            catch { }
        }

        return val;
    }

    #endregion
}
