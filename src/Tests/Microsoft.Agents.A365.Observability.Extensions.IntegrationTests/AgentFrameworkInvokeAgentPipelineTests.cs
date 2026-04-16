// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Extensions.AgentFramework;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Extensions.IntegrationTests;

/// <summary>
/// Pipeline integration tests for <see cref="AgentFrameworkSpanProcessor"/> handling invoke_agent spans.
/// Verifies the full Builder → WithAgentFramework → Processor → Exporter chain produces A365 versioned messages.
/// Does NOT require Azure OpenAI credentials.
/// </summary>
[TestClass]
public class AgentFrameworkInvokeAgentPipelineTests
{
    private const string AfSourceName = BuilderExtensions.AgentFrameworkSource;
    private List<Activity> _exportedActivities = new();
    private ServiceProvider? _serviceProvider;
    private ActivitySource? _activitySource;

    [TestInitialize]
    public void Setup()
    {
        _exportedActivities = new List<Activity>();

        var services = new ServiceCollection();
        services.AddLogging();

        new Runtime.Builder(services, configuration: null, useOpenTelemetryBuilder: true)
            .WithAgentFramework()
            .Build();

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(AfSourceName)
                .AddProcessor(new SimpleActivityExportProcessor(new ActivityCapturingExporter(_exportedActivities))));

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.GetService<TracerProvider>();

        _activitySource = new ActivitySource(AfSourceName);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _activitySource?.Dispose();
        _serviceProvider?.Dispose();
    }

    [TestMethod]
    public void InvokeAgent_MapsInputAndOutputToA365StructuredFormat()
    {
        // Agent Framework emits messages with parts array
        var inputJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                role = "system",
                parts = new[] { new { type = "text", content = "You are a helpful agent." } }
            },
            new
            {
                role = "user",
                parts = new[] { new { type = "text", content = "What can you do?" } }
            }
        });
        var outputJson = JsonSerializer.Serialize(new object[]
        {
            new
            {
                role = "assistant",
                parts = new object[] { new { type = "text", content = "I can help with many tasks!" } },
                finish_reason = "stop"
            }
        });

        using (var activity = _activitySource!.StartActivity("invoke_agent TestAgent"))
        {
            activity!.SetTag("gen_ai.operation.name", "invoke_agent");
            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputJson);
            activity.SetTag(OpenTelemetryConstants.GenAiOutputMessagesKey, outputJson);
        }

        ForceFlush();

        var span = FindInvokeAgentSpan();
        span.Should().NotBeNull("invoke_agent span should be captured by pipeline");
        var tags = GetTags(span!);

        // Input: versioned, all roles preserved, TextPart format
        var input = tags[OpenTelemetryConstants.GenAiInputMessagesKey] as string;
        input.Should().Contain("\"version\":\"0.1.0\"", "should use A365 versioned wrapper");
        input.Should().Contain("\"role\":\"system\"", "system messages should be preserved");
        input.Should().Contain("\"role\":\"user\"", "user messages should be mapped");
        input.Should().Contain("\"type\":\"text\"", "should use TextPart format");
        input.Should().Contain("You are a helpful agent.");
        input.Should().Contain("What can you do?");

        // Output: versioned, assistant role, finish reason, TextPart format
        var output = tags[OpenTelemetryConstants.GenAiOutputMessagesKey] as string;
        output.Should().Contain("\"version\":\"0.1.0\"");
        output.Should().Contain("\"role\":\"assistant\"");
        output.Should().Contain("\"type\":\"text\"");
        output.Should().Contain("I can help with many tasks!");
        output.Should().Contain("\"finish_reason\":\"stop\"", "finish reason should be preserved");
    }

    [TestMethod]
    public void InvokeAgent_MapsToolCallAndResponseParts()
    {
        var inputJson = JsonSerializer.Serialize(new object[]
        {
            new
            {
                role = "user",
                parts = new object[] { new { type = "text", content = "What is the weather?" } }
            },
            new
            {
                role = "assistant",
                parts = new object[]
                {
                    new { type = "tool_call", name = "GetWeather", id = "call_123", arguments = "{\"location\":\"Seattle\"}" }
                }
            },
            new
            {
                role = "tool",
                parts = new object[]
                {
                    new { type = "tool_call_response", id = "call_123", response = "Sunny, 72°F" }
                }
            }
        });

        using (var activity = _activitySource!.StartActivity("invoke_agent WeatherAgent"))
        {
            activity!.SetTag("gen_ai.operation.name", "invoke_agent");
            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputJson);
        }

        ForceFlush();

        var span = FindInvokeAgentSpan();
        span.Should().NotBeNull();

        var tags = GetTags(span!);
        var input = tags[OpenTelemetryConstants.GenAiInputMessagesKey] as string;

        input.Should().Contain("\"version\":\"0.1.0\"");
        input.Should().Contain("\"type\":\"tool_call\"", "tool call request should be mapped");
        input.Should().Contain("GetWeather", "function name should be preserved");
        input.Should().Contain("\"type\":\"tool_call_response\"", "tool response should be mapped");
        input.Should().Contain("Sunny", "tool response content should be preserved");
    }

    #region Helpers

    private void ForceFlush()
    {
        _serviceProvider?.GetService<TracerProvider>()?.ForceFlush();
    }

    private Activity? FindInvokeAgentSpan()
    {
        return _exportedActivities.FirstOrDefault(a =>
            a.GetTagItem("gen_ai.operation.name") as string == "invoke_agent");
    }

    private static Dictionary<string, object?> GetTags(Activity activity)
    {
        return activity.TagObjects.ToDictionary(t => t.Key, t => t.Value);
    }

    #endregion
}
