// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Extensions.AgentFramework;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Extensions.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="AgentFrameworkSpanProcessor"/> verifying the mapping from
/// Agent Framework message format to A365 versioned message format.
/// Agent Framework sets gen_ai.input.messages / gen_ai.output.messages as span tags with
/// JSON arrays of {role, parts[{type, content}], finish_reason?}. The processor should
/// convert these to the A365 versioned format {version, messages[{role, parts[...]}]}.
/// </summary>
[TestClass]
public class AgentFrameworkSpanProcessorTests
{
    private static readonly JsonSerializerOptions JsonPrint = new() { WriteIndented = true };
    private static readonly string SourceName = BuilderExtensions.AgentFrameworkSource;

    private List<Activity> _exportedActivities = new();
    private TracerProvider? _tracerProvider;
    private ActivitySource? _activitySource;

    [TestInitialize]
    public void Setup()
    {
        _exportedActivities = new List<Activity>();
        _activitySource = new ActivitySource(SourceName);

        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddProcessor(new AgentFrameworkSpanProcessor())
            .AddProcessor(new ActivityCapturingProcessor(_exportedActivities))
            .Build();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _activitySource?.Dispose();
        _tracerProvider?.Dispose();
    }

    [TestMethod]
    public void SimpleChat_MapsToVersionedInputAndOutputMessages()
    {
        // Arrange — simulate what Agent Framework emits for a simple chat
        var inputJson = @"[
            {""role"": ""system"", ""parts"": [{""type"": ""text"", ""content"": ""You are a helpful assistant.""}]},
            {""role"": ""user"", ""parts"": [{""type"": ""text"", ""content"": ""What is the capital of France?""}]}
        ]";
        var outputJson = @"[
            {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""The capital of France is Paris.""}], ""finish_reason"": ""stop""}
        ]";

        using (var activity = _activitySource!.StartActivity("chat gpt-4o-mini", ActivityKind.Client))
        {
            activity!.SetTag("gen_ai.operation.name", "chat");
            activity.SetTag("gen_ai.input.messages", inputJson);
            activity.SetTag("gen_ai.output.messages", outputJson);
        }

        _tracerProvider!.ForceFlush();

        // Assert
        _exportedActivities.Should().HaveCount(1);
        var span = _exportedActivities[0];
        DumpActivity(span, "AF SimpleChat");

        var input = span.GetTagItem("gen_ai.input.messages") as string;
        input.Should().Contain("\"version\":\"0.1.0\"", "should produce versioned wrapper");
        input.Should().Contain("\"role\":\"system\"", "should preserve system message");
        input.Should().Contain("\"role\":\"user\"", "should preserve user message");
        input.Should().Contain("\"type\":\"text\"", "should use TextPart format");
        input.Should().Contain("capital of France");

        var output = span.GetTagItem("gen_ai.output.messages") as string;
        output.Should().Contain("\"version\":\"0.1.0\"");
        output.Should().Contain("\"role\":\"assistant\"");
        output.Should().Contain("\"type\":\"text\"");
        output.Should().Contain("Paris");
        output.Should().Contain("\"finish_reason\":\"stop\"");
    }

    [TestMethod]
    public void ToolCallConversation_MapsToolCallAndResponseParts()
    {
        // Arrange — simulate a tool call round-trip
        var inputJson = @"[
            {""role"": ""system"", ""parts"": [{""type"": ""text"", ""content"": ""You are a weather assistant.""}]},
            {""role"": ""user"", ""parts"": [{""type"": ""text"", ""content"": ""What is the weather in Seattle?""}]},
            {""role"": ""assistant"", ""parts"": [{""type"": ""tool_call"", ""name"": ""GetWeather"", ""id"": ""call_123"", ""arguments"": ""{\""location\"": \""Seattle\""}""}]},
            {""role"": ""tool"", ""parts"": [{""type"": ""tool_call_response"", ""id"": ""call_123"", ""response"": ""Sunny, 72°F""}]}
        ]";
        var outputJson = @"[
            {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""The weather in Seattle is sunny with 72°F.""}], ""finish_reason"": ""stop""}
        ]";

        using (var activity = _activitySource!.StartActivity("chat gpt-4o-mini", ActivityKind.Client))
        {
            activity!.SetTag("gen_ai.operation.name", "chat");
            activity.SetTag("gen_ai.input.messages", inputJson);
            activity.SetTag("gen_ai.output.messages", outputJson);
        }

        _tracerProvider!.ForceFlush();

        _exportedActivities.Should().HaveCount(1);
        var span = _exportedActivities[0];
        DumpActivity(span, "AF ToolCall");

        var input = span.GetTagItem("gen_ai.input.messages") as string;
        input.Should().Contain("\"version\":\"0.1.0\"");
        input.Should().Contain("\"type\":\"tool_call\"", "should map tool_call parts");
        input.Should().Contain("\"name\":\"GetWeather\"");
        input.Should().Contain("\"id\":\"call_123\"");
        input.Should().Contain("\"type\":\"tool_call_response\"", "should map tool_call_response parts");
        input.Should().Contain("Sunny, 72");
    }

    [TestMethod]
    public void MultimodalMessage_MapsBlobAndUriParts()
    {
        // Arrange — simulate a multimodal message with blob and uri parts
        var inputJson = @"[
            {""role"": ""user"", ""parts"": [
                {""type"": ""text"", ""content"": ""Describe this image""},
                {""type"": ""blob"", ""modality"": ""image"", ""content"": ""aW1hZ2VkYXRh"", ""mime_type"": ""image/png""},
                {""type"": ""uri"", ""modality"": ""image"", ""uri"": ""https://example.com/image.png"", ""mime_type"": ""image/png""}
            ]}
        ]";

        using (var activity = _activitySource!.StartActivity("chat gpt-4o-mini", ActivityKind.Client))
        {
            activity!.SetTag("gen_ai.operation.name", "chat");
            activity.SetTag("gen_ai.input.messages", inputJson);
        }

        _tracerProvider!.ForceFlush();

        _exportedActivities.Should().HaveCount(1);
        var span = _exportedActivities[0];
        DumpActivity(span, "AF Multimodal");

        var input = span.GetTagItem("gen_ai.input.messages") as string;
        input.Should().Contain("\"version\":\"0.1.0\"");
        input.Should().Contain("\"type\":\"text\"");
        input.Should().Contain("Describe this image");
        input.Should().Contain("\"type\":\"blob\"", "should map blob parts");
        input.Should().Contain("\"modality\":\"image\"");
        input.Should().Contain("\"type\":\"uri\"", "should map uri parts");
        input.Should().Contain("https://example.com/image.png");
    }

    [TestMethod]
    public void InvokeAgentOperation_MapsMessages()
    {
        // Arrange — invoke_agent operation also gets mapped
        var inputJson = @"[
            {""role"": ""user"", ""parts"": [{""type"": ""text"", ""content"": ""Hello agent""}]}
        ]";
        var outputJson = @"[
            {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""Hello! How can I help?""}], ""finish_reason"": ""stop""}
        ]";

        using (var activity = _activitySource!.StartActivity("invoke_agent TestAgent", ActivityKind.Internal))
        {
            activity!.SetTag("gen_ai.operation.name", "invoke_agent");
            activity.SetTag("gen_ai.input.messages", inputJson);
            activity.SetTag("gen_ai.output.messages", outputJson);
        }

        _tracerProvider!.ForceFlush();

        _exportedActivities.Should().HaveCount(1);
        var span = _exportedActivities[0];
        DumpActivity(span, "AF InvokeAgent");

        var input = span.GetTagItem("gen_ai.input.messages") as string;
        input.Should().Contain("\"version\":\"0.1.0\"");
        input.Should().Contain("Hello agent");

        var output = span.GetTagItem("gen_ai.output.messages") as string;
        output.Should().Contain("\"version\":\"0.1.0\"");
        output.Should().Contain("Hello! How can I help?");
    }

    [TestMethod]
    public void ExecuteToolOperation_CopiesResultToEventContent()
    {
        // Arrange — execute_tool copies tool result to gen_ai.event.content
        using (var activity = _activitySource!.StartActivity("execute_tool GetWeather", ActivityKind.Internal))
        {
            activity!.SetTag("gen_ai.operation.name", "execute_tool");
            activity.SetTag("gen_ai.tool.call.result", "Sunny, 72°F");
        }

        _tracerProvider!.ForceFlush();

        _exportedActivities.Should().HaveCount(1);
        var span = _exportedActivities[0];

        var eventContent = span.GetTagItem("gen_ai.event.content") as string;
        eventContent.Should().Be("Sunny, 72°F");
    }

    [TestMethod]
    public void ReasoningPart_MapsCorrectly()
    {
        var inputJson = @"[
            {""role"": ""assistant"", ""parts"": [
                {""type"": ""reasoning"", ""content"": ""Let me think about this step by step...""},
                {""type"": ""text"", ""content"": ""The answer is 42.""}
            ]}
        ]";

        using (var activity = _activitySource!.StartActivity("chat gpt-4o-mini", ActivityKind.Client))
        {
            activity!.SetTag("gen_ai.operation.name", "chat");
            activity.SetTag("gen_ai.input.messages", inputJson);
        }

        _tracerProvider!.ForceFlush();

        var span = _exportedActivities[0];
        DumpActivity(span, "AF Reasoning");

        var input = span.GetTagItem("gen_ai.input.messages") as string;
        input.Should().Contain("\"version\":\"0.1.0\"");
        input.Should().Contain("\"type\":\"reasoning\"", "should map reasoning parts");
        input.Should().Contain("step by step");
        input.Should().Contain("\"type\":\"text\"");
        input.Should().Contain("answer is 42");
    }

    [TestMethod]
    public void InvalidJson_PreservesOriginal()
    {
        using (var activity = _activitySource!.StartActivity("chat gpt-4o-mini", ActivityKind.Client))
        {
            activity!.SetTag("gen_ai.operation.name", "chat");
            activity.SetTag("gen_ai.input.messages", "not valid json");
        }

        _tracerProvider!.ForceFlush();

        var span = _exportedActivities[0];
        // Mapper returns null for invalid JSON, so the original tag stays
        var input = span.GetTagItem("gen_ai.input.messages") as string;
        input.Should().Be("not valid json", "invalid JSON should be left unchanged");
    }

    #region Helpers

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
