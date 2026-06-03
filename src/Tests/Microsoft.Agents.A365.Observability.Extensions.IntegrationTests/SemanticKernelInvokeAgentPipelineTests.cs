// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Extensions.IntegrationTests;

/// <summary>
/// Pipeline integration tests for <see cref="SemanticKernelSpanProcessor"/> handling invoke_agent spans.
/// Verifies the full Builder → WithSemanticKernel → Processor → Exporter chain produces A365 versioned messages.
/// Does NOT require Azure OpenAI credentials.
/// </summary>
[TestClass]
public class SemanticKernelInvokeAgentPipelineTests
{
    private const string SkSourceName = "Microsoft.SemanticKernel.Agent";
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
            .WithSemanticKernel()
            .Build();

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(SkSourceName)
                .AddProcessor(new SimpleActivityExportProcessor(new ActivityCapturingExporter(_exportedActivities))));

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.GetService<TracerProvider>();

        _activitySource = new ActivitySource(SkSourceName);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _activitySource?.Dispose();
        _serviceProvider?.Dispose();
    }

    [TestMethod]
    public void InvokeAgent_MapsInvocationTagsToA365StructuredFormat()
    {
        // SK emits invoke_agent spans with invocation-specific keys containing JSON arrays of objects
        var inputJson = @"[{""role"":""system"",""name"":""TestAgent"",""content"":""You are a helpful assistant."",""tool_calls"":[]},{""role"":""user"",""name"":null,""content"":""What can you do?"",""tool_calls"":[]}]";

        var nestedContentJson = @"{""contentType"":""Text"",""content"":""I can help with many tasks!""}";
        var outputJson = $@"[{{""role"":""Assistant"",""name"":""TestAgent"",""content"":{JsonSerializer.Serialize(nestedContentJson)}}}]";

        using (var activity = _activitySource!.StartActivity("invoke_agent TestAgent"))
        {
            activity!.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, "invoke_agent");
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, inputJson);
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationOutputKey, outputJson);
        }

        ForceFlush();

        var span = FindInvokeAgentSpan();
        span.Should().NotBeNull("invoke_agent span should be captured by pipeline");
        var tags = GetTags(span!);

        // Invocation keys should be removed, results on standard keys
        tags.Should().NotContainKey(OpenTelemetryConstants.GenAiAgentInvocationInputKey);
        tags.Should().NotContainKey(OpenTelemetryConstants.GenAiAgentInvocationOutputKey);

        // Input: versioned, all roles preserved, TextPart format
        var input = tags[OpenTelemetryConstants.GenAiInputMessagesKey] as string;
        input.Should().StartWith("[");
        input.Should().Contain("\"role\":\"system\"", "system messages should be preserved");
        input.Should().Contain("\"role\":\"user\"", "user messages should be mapped");
        input.Should().Contain("\"type\":\"text\"", "should use TextPart format");
        input.Should().Contain("You are a helpful assistant.");
        input.Should().Contain("What can you do?");

        // Output: versioned, nested content extracted, TextPart format
        var output = tags[OpenTelemetryConstants.GenAiOutputMessagesKey] as string;
        output.Should().StartWith("[");
        output.Should().Contain("\"role\":\"assistant\"");
        output.Should().Contain("\"type\":\"text\"");
        output.Should().Contain("I can help with many tasks!", "nested content should be extracted");
        output.Should().NotContain("contentType", "wrapper should be unwrapped");
    }

    [TestMethod]
    public void InvokeAgent_MapsStringArrayInputWithMessagePrefix()
    {
        // SK also emits invocation input as JSON array of serialized message strings (not objects)
        var messages = new List<string>
        {
            JsonSerializer.Serialize(new { role = "system", content = "System prompt" }),
            JsonSerializer.Serialize(new { role = "user", content = "Message:Hello agent" })
        };
        var inputJson = JsonSerializer.Serialize(messages);

        using (var activity = _activitySource!.StartActivity("invoke_agent TestAgent"))
        {
            activity!.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, "invoke_agent");
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, inputJson);
        }

        ForceFlush();

        var span = FindInvokeAgentSpan();
        span.Should().NotBeNull();

        var tags = GetTags(span!);
        tags.Should().NotContainKey(OpenTelemetryConstants.GenAiAgentInvocationInputKey);

        var input = tags[OpenTelemetryConstants.GenAiInputMessagesKey] as string;
        input.Should().StartWith("[");
        input.Should().Contain("\"role\":\"system\"", "system messages should be preserved");
        input.Should().Contain("\"role\":\"user\"");
        input.Should().Contain("Hello agent", "Message: prefix should be trimmed");
        input.Should().NotContain("Message:", "Message: prefix should be removed");
    }

    #region Helpers

    private void ForceFlush()
    {
        _serviceProvider?.GetService<TracerProvider>()?.ForceFlush();
    }

    private Activity? FindInvokeAgentSpan()
    {
        return _exportedActivities.FirstOrDefault(a =>
            a.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string == "invoke_agent");
    }

    private static Dictionary<string, object?> GetTags(Activity activity)
    {
        return activity.TagObjects.ToDictionary(t => t.Key, t => t.Value);
    }

    #endregion
}
