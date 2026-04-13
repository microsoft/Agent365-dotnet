// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Extensions.AgentFramework;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Extensions.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="AgentFrameworkSpanProcessor"/> running against real Azure OpenAI
/// via <see cref="IChatClient"/> with <c>UseOpenTelemetry()</c> — the same pipeline used by Agent Framework.
/// Pipeline: IChatClient.UseOpenTelemetry() → TracerProvider → AgentFrameworkSpanProcessor → captured spans.
/// Requires: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT env vars.
/// </summary>
[TestClass]
public class AgentFrameworkSpanProcessorTests
{
    private static readonly JsonSerializerOptions JsonPrint = new() { WriteIndented = true };
    private const string OTelSourceName = BuilderExtensions.AgentFrameworkChatClientSource;

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
        _exportedActivities = new List<Activity>();

        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(OTelSourceName)
            .AddProcessor(new AgentFrameworkSpanProcessor())
            .AddProcessor(new ActivityCapturingProcessor(_exportedActivities))
            .Build();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _tracerProvider?.Dispose();
    }

    [TestMethod]
    public async Task SimpleChat_ProcessorMapsToVersionedFormat()
    {
        SkipIfNoCredentials();

        var chatClient = CreateChatClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant. Reply in one sentence."),
            new(ChatRole.User, "What is the capital of France?")
        };

        await chatClient.GetResponseAsync(messages);

        _tracerProvider!.ForceFlush();

        var chatSpan = FindChatSpan();
        chatSpan.Should().NotBeNull("IChatClient should emit a chat span");
        DumpActivity(chatSpan!, "AF SimpleChat");

        var tags = GetTags(chatSpan!);

        // Verify the processor mapped to A365 versioned format
        tags.Should().ContainKey("gen_ai.input.messages");
        var input = tags["gen_ai.input.messages"] as string;
        input.Should().Contain("\"version\":\"0.1.0\"", "should produce versioned wrapper");
        input.Should().Contain("\"type\":\"text\"", "should use TextPart format");
        input.Should().Contain("capital of France");

        tags.Should().ContainKey("gen_ai.output.messages");
        var output = tags["gen_ai.output.messages"] as string;
        output.Should().Contain("\"version\":\"0.1.0\"");
        output.Should().Contain("\"type\":\"text\"");
        output.Should().Contain("\"role\":\"assistant\"");
    }

    [TestMethod]
    public async Task ChatWithToolCall_ProcessorMapsToolCallParts()
    {
        SkipIfNoCredentials();

        var chatClient = CreateChatClientWithTools();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a weather assistant. Always use the GetWeather function."),
            new(ChatRole.User, "What's the weather in Seattle?")
        };

        await chatClient.GetResponseAsync(messages);

        _tracerProvider!.ForceFlush();

        // Dump all spans to see what the full pipeline emits
        Console.WriteLine($"\n  All captured activities ({_exportedActivities.Count}):");
        foreach (var act in _exportedActivities)
        {
            var op = act.GetTagItem("gen_ai.operation.name") as string ?? "(none)";
            Console.WriteLine($"    {act.Source.Name} | {act.DisplayName} | op={op}");
            DumpActivity(act, $"AF ToolCall — {act.DisplayName}");
        }

        // Find a chat span that has input messages
        var chatSpan = _exportedActivities.LastOrDefault(a =>
        {
            var input = a.GetTagItem("gen_ai.input.messages") as string;
            return input != null && input.Contains("version");
        });

        chatSpan.Should().NotBeNull("should have at least one span with versioned input messages");

        var tags = GetTags(chatSpan!);
        var input = tags["gen_ai.input.messages"] as string;
        input.Should().Contain("\"version\":\"0.1.0\"");
        input.Should().Contain("weather");
    }

    [TestMethod]
    public async Task SimpleChat_RawFormatBeforeProcessor()
    {
        SkipIfNoCredentials();

        // Capture spans WITHOUT the processor to see raw IChatClient format
        _tracerProvider?.Dispose();
        var rawActivities = new List<Activity>();
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(OTelSourceName)
            .AddProcessor(new ActivityCapturingProcessor(rawActivities))
            .Build();

        var chatClient = CreateChatClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Say hello")
        };

        await chatClient.GetResponseAsync(messages);

        _tracerProvider!.ForceFlush();

        var chatSpan = rawActivities.FirstOrDefault(a =>
            a.GetTagItem("gen_ai.operation.name") as string == "chat");

        chatSpan.Should().NotBeNull("IChatClient should emit a chat span");
        DumpActivity(chatSpan!, "AF Raw (no processor)");

        // Document the raw format that Microsoft.Extensions.AI emits
        var tags = GetTags(chatSpan!);
        tags.Should().ContainKey("gen_ai.input.messages",
            "Microsoft.Extensions.AI UseOpenTelemetry should emit input messages");
        tags.Should().ContainKey("gen_ai.output.messages",
            "Microsoft.Extensions.AI UseOpenTelemetry should emit output messages");

        // Log the raw format for analysis
        Console.WriteLine($"\n  Raw gen_ai.input.messages:\n{tags["gen_ai.input.messages"]}");
        Console.WriteLine($"\n  Raw gen_ai.output.messages:\n{tags["gen_ai.output.messages"]}");
    }

    #region Helpers

    private IChatClient CreateChatClient()
    {
        return new AzureOpenAIClient(
                new Uri(Endpoint!),
                new ApiKeyCredential(ApiKey!))
            .GetChatClient(Deployment!)
            .AsIChatClient()
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: OTelSourceName,
                configure: cfg => cfg.EnableSensitiveData = true)
            .Build();
    }

    private IChatClient CreateChatClientWithTools()
    {
        return new AzureOpenAIClient(
                new Uri(Endpoint!),
                new ApiKeyCredential(ApiKey!))
            .GetChatClient(Deployment!)
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .UseOpenTelemetry(
                sourceName: OTelSourceName,
                configure: cfg => cfg.EnableSensitiveData = true)
            .Build();
    }

    private Activity? FindChatSpan()
    {
        return _exportedActivities.FirstOrDefault(a =>
            a.GetTagItem("gen_ai.operation.name") as string == "chat");
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
