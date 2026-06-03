// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Extensions.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="SemanticKernelSpanProcessor"/> running against real Azure OpenAI
/// via Semantic Kernel's <see cref="IChatCompletionService"/>.
/// Pipeline: SK SDK → TracerProvider → SemanticKernelSpanProcessor → captured spans.
/// Requires: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT env vars.
/// </summary>
[TestClass]
public class SemanticKernelSpanProcessorTests
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
    private ServiceProvider? _serviceProvider;

    [TestInitialize]
    public void Setup()
    {
        _exportedActivities = new List<Activity>();

        // Use the real A365 SDK initialization path: Builder → WithSemanticKernel → Build
        var services = new ServiceCollection();
        services.AddLogging();

        new Runtime.Builder(services, configuration: null, useOpenTelemetryBuilder: true)
            .WithSemanticKernel()
            .Build();

        // Add a capturing exporter — runs after all processors, at export time
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddProcessor(new SimpleActivityExportProcessor(new ActivityCapturingExporter(_exportedActivities))));

        _serviceProvider = services.BuildServiceProvider();

        // Force the TracerProvider to be created by resolving it
        _serviceProvider.GetService<TracerProvider>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    [TestMethod]
    public async Task SimpleChat_ProcessorSetsInputAndOutputMessages()
    {
        SkipIfNoCredentials();

        var kernel = CreateKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage("You are a helpful assistant. Reply in one sentence.");
        history.AddUserMessage("What is the capital of France?");

        _ = await chatService.GetChatMessageContentAsync(history);

        ForceFlush();

        // Find the chat completion span (SK may emit multiple spans)
        var chatSpan = _exportedActivities.FirstOrDefault(a =>
        {
            var op = a.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string;
            return op == "Chat" || op == "chat" || op == "chat.completions";
        });

        chatSpan.Should().NotBeNull("SK should emit a chat completion span");
        DumpActivity(chatSpan!, "SK SimpleChat — after SemanticKernelSpanProcessor");

        var tags = GetTags(chatSpan!);

        // Processor should rename operation to "Chat"
        tags.Should().ContainKey("gen_ai.operation.name");

        // Processor should produce versioned A365 input messages
        tags.Should().ContainKey("gen_ai.input.messages",
            "SemanticKernelSpanProcessor should map input events to A365 versioned format");

        var inputMessages = tags["gen_ai.input.messages"] as string;
        inputMessages.Should().StartWith("[");
        inputMessages.Should().Contain("capital of France", "should contain user message text");
        inputMessages.Should().Contain("\"type\":\"text\"", "should use TextPart format");
        inputMessages.Should().Contain("\"role\":\"system\"", "should include system message");
        inputMessages.Should().Contain("\"role\":\"user\"", "should include user message");

        // Processor should produce versioned A365 output messages
        tags.Should().ContainKey("gen_ai.output.messages",
            "SemanticKernelSpanProcessor should map choice events to A365 versioned format");

        var outputMessages = tags["gen_ai.output.messages"] as string;
        outputMessages.Should().StartWith("[");
        outputMessages.Should().Contain("\"type\":\"text\"", "should use TextPart format");
        outputMessages.Should().Contain("\"role\":\"assistant\"", "should have assistant role");
        outputMessages.Should().Contain("\"finish_reason\":\"stop\"", "should map SK Stop → stop");

        // Verify standard attributes are present
        tags.Should().ContainKey("gen_ai.request.model");
        tags.Should().ContainKey("gen_ai.usage.input_tokens");
        tags.Should().ContainKey("gen_ai.usage.output_tokens");

        // Dump all activities for visibility
        Console.WriteLine($"\n  All captured activities ({_exportedActivities.Count}):");
        foreach (var act in _exportedActivities)
        {
            var op = act.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string ?? "(none)";
            Console.WriteLine($"    {act.Source.Name} | {act.DisplayName} | op={op}");
        }
    }

    [TestMethod]
    public async Task ChatWithToolCall_ProcessorHandlesToolCallSpan()
    {
        SkipIfNoCredentials();

        var kernel = CreateKernel();
        kernel.Plugins.AddFromFunctions("Weather",
        [
            KernelFunctionFactory.CreateFromMethod(
                ([System.ComponentModel.Description("City name")] string location) =>
                    $"Sunny, 72°F in {location}",
                "GetWeather",
                "Get the current weather for a location")
        ]);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage("You are a weather assistant. Use the GetWeather function when asked about weather.");
        history.AddUserMessage("What's the weather in Seattle?");

        var settings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        _ = await chatService.GetChatMessageContentAsync(history, settings, kernel);

        ForceFlush();

        // Dump all spans
        Console.WriteLine($"\n  All captured activities ({_exportedActivities.Count}):");
        foreach (var act in _exportedActivities)
        {
            var op = act.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string ?? "(none)";
            Console.WriteLine($"    {act.Source.Name} | {act.DisplayName} | op={op}");
            DumpActivity(act, $"SK ToolCall — {act.DisplayName}");
        }

        // Find the chat span
        var chatSpan = _exportedActivities.FirstOrDefault(a =>
        {
            var op = a.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string;
            return op == "Chat" || op == "chat" || op == "chat.completions";
        });

        chatSpan.Should().NotBeNull("SK should emit a chat completion span");

        var tags = GetTags(chatSpan!);

        // Input messages should be in versioned format with tool call parts
        tags.Should().ContainKey("gen_ai.input.messages");
        var inputMessages = tags["gen_ai.input.messages"] as string;
        inputMessages.Should().StartWith("[");
        inputMessages.Should().Contain("\"role\":\"user\"");
        inputMessages.Should().Contain("weather");

        // The last chat span (after tool response) should have full round-trip in input
        var lastChatSpan = _exportedActivities.LastOrDefault(a =>
        {
            var op = a.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string;
            return op == "Chat";
        });

        if (lastChatSpan != null)
        {
            var lastTags = GetTags(lastChatSpan);
            var lastInput = lastTags["gen_ai.input.messages"] as string;
            lastInput.Should().Contain("\"type\":\"tool_call\"",
                "assistant tool call request should be mapped as ToolCallRequestPart");
            lastInput.Should().Contain("\"type\":\"tool_call_response\"",
                "tool response should be mapped as ToolCallResponsePart");
            lastInput.Should().Contain("GetWeather", "should contain function name");

            var lastOutput = lastTags["gen_ai.output.messages"] as string;
            lastOutput.Should().StartWith("[");
            lastOutput.Should().Contain("\"finish_reason\":\"stop\"");
        }

        tags.Should().ContainKey("gen_ai.usage.input_tokens");
    }

    [TestMethod]
    public async Task SimpleChat_SpanEventsContainMessageContent()
    {
        SkipIfNoCredentials();

        var kernel = CreateKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddUserMessage("Say hello");

        _ = await chatService.GetChatMessageContentAsync(history);

        ForceFlush();

        // Find chat span and examine raw events before processor
        var chatSpan = _exportedActivities.FirstOrDefault(a =>
        {
            var op = a.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string;
            return op == "Chat" || op == "chat" || op == "chat.completions";
        });

        chatSpan.Should().NotBeNull();

        // SK emits events — log them to see the format
        Console.WriteLine($"\n  Span events ({chatSpan!.Events.Count()}):");
        foreach (var ev in chatSpan.Events)
        {
            Console.WriteLine($"    Event: '{ev.Name}'");
            foreach (var attr in ev.Tags)
            {
                string val = FormatValue(attr.Value);
                Console.WriteLine($"      {attr.Key} = {val}");
            }
        }

        // SK should emit gen_ai.user.message and gen_ai.choice events
        var eventNames = chatSpan.Events.Select(e => e.Name).ToList();
        eventNames.Should().Contain("gen_ai.user.message",
            "SK should emit user message events when sensitive diagnostics are enabled");
        eventNames.Should().Contain("gen_ai.choice",
            "SK should emit choice events when sensitive diagnostics are enabled");
    }

    #region Helpers

    private void ForceFlush()
    {
        var tracerProvider = _serviceProvider?.GetService<TracerProvider>();
        tracerProvider?.ForceFlush();
    }

    private Kernel CreateKernel()
    {
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: Deployment!,
            endpoint: Endpoint!,
            apiKey: ApiKey!);
        return builder.Build();
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
