// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Tests.Tracing;
using Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Resources;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common;

public sealed class TestScope : OpenTelemetryScope
{
    public TestScope(string operationName, string activityName, AgentDetails agentDetails, SpanDetails? spanDetails = null)
        : base(operationName, activityName, agentDetails, spanDetails) { }
}

[TestClass]
public partial class ExportFormatterTests : ActivityTest
{
    private static readonly NullLogger<ExportFormatter> NullLogger = NullLogger<ExportFormatter>.Instance;
    private static ExportFormatter CreateFormatter() => new ExportFormatter(NullLogger);
    private static Resource CreateResource(Dictionary<string, object>? attributes = null)
    {
        var builder = ResourceBuilder.CreateEmpty();
        if (attributes != null)
        {
            builder.AddAttributes(attributes.AsEnumerable());
        }
        return builder.Build();
    }

    [TestMethod]
    public void Format_EmptyActivities_ReturnsValidJson()
    {
        // Arrange
        var activities = new List<Activity>();
        var resource = CreateResource(new Dictionary<string, object> { { "env", "test" } });
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatMany(activities, resource);

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("resourceSpans", out var resourceSpans).Should().BeTrue();
        resourceSpans.GetArrayLength().Should().Be(1);
        var spans = resourceSpans[0].GetProperty("scopeSpans");
        spans.GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public void Format_SingleActivity_AllFieldsMapped()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(123);
        var tags = new Dictionary<string, object>
        {
            { "tag1", "value1" },
            { "tag2", 42 }
        };
        var events = new List<ActivityEvent>
        {
            new ActivityEvent("ev1", startTime, new ActivityTagsCollection { { "evtag", "evval" } })
        };
        var links = new List<ActivityLink>
        {
            new ActivityLink(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded), new ActivityTagsCollection { { "linktag", "linkval" } })
        };

        var activity = CreateActivity(
            sourceName: "TestSource",
            displayName: "span1",
            kind: ActivityKind.Client,
            startTimeUtc: startTime,
            duration: duration,
            tags: tags,
            events: events,
            links: links,
            status: ActivityStatusCode.Error,
            statusDescription: "fail"
        );

        var activities = new List<Activity> { activity };
        var resource = CreateResource(new Dictionary<string, object> { { "env", "test" } });
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatMany(activities, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
        var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
        scopeSpans.GetArrayLength().Should().Be(1);

        var scope = scopeSpans[0].GetProperty("scope");
        scope.GetProperty("name").GetString().Should().Be("TestSource");

        var spans = scopeSpans[0].GetProperty("spans");
        spans.GetArrayLength().Should().Be(1);

        var span = spans[0];
        span.GetProperty("name").GetString().Should().Be("span1");
        span.GetProperty("kind").GetInt32().Should().Be((int)ActivityKind.Client);
        span.GetProperty("startTimeUnixNano").GetUInt64().Should().BeGreaterThan(0);
        span.GetProperty("endTimeUnixNano").GetUInt64().Should().BeGreaterThan(span.GetProperty("startTimeUnixNano").GetUInt64());

        var attributes = span.GetProperty("attributes");
        attributes.GetProperty("tag1").GetString().Should().Be("value1");
        attributes.GetProperty("tag2").GetInt32().Should().Be(42);

        var eventsJson = span.GetProperty("events");
        eventsJson.GetArrayLength().Should().Be(1);
        var eventJson = eventsJson[0];
        eventJson.GetProperty("name").GetString().Should().Be("ev1");
        eventJson.GetProperty("attributes").GetProperty("evtag").GetString().Should().Be("evval");

        var linksJson = span.GetProperty("links");
        linksJson.GetArrayLength().Should().Be(1);
        var linkJson = linksJson[0];
        linkJson.GetProperty("attributes").GetProperty("linktag").GetString().Should().Be("linkval");

        var status = span.GetProperty("status");
        status.GetProperty("code").GetInt32().Should().Be((int)ActivityStatusCode.Error);
        status.GetProperty("message").GetString().Should().Be("fail");
    }

    [TestMethod]
    public void Format_MultipleActivities_GroupedBySource()
    {
        // Arrange
        var act1 = CreateActivity(sourceName: "SourceA", displayName: "spanA");
        var act2 = CreateActivity(sourceName: "SourceA", displayName: "spanB");
        var act3 = CreateActivity(sourceName: "SourceB", displayName: "spanC");

        var activities = new List<Activity> { act1, act2, act3 };
        var resource = CreateResource(new Dictionary<string, object> { { "env", "test" } });
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatMany(activities, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
        var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
        scopeSpans.GetArrayLength().Should().Be(2);

        var scopeA = scopeSpans[0].GetProperty("scope");
        var scopeB = scopeSpans[1].GetProperty("scope");

        var names = new[] { scopeA.GetProperty("name").GetString(), scopeB.GetProperty("name").GetString() };
        names.Should().Contain("SourceA");
        names.Should().Contain("SourceB");

        var spansA = scopeSpans[0].GetProperty("spans");
        var spansB = scopeSpans[1].GetProperty("spans");
        (spansA.GetArrayLength() + spansB.GetArrayLength()).Should().Be(3);
    }

    [TestMethod]
    public void Format_ResourceAttributes_AreMapped()
    {
        // Arrange
        var act = CreateActivity();
        var resource = CreateResource(new Dictionary<string, object>
        {
            { "custom1", "val1" },
            { "custom2", 123 }
        });
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatMany(new[] { act }, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
        var resourceObj = resourceSpans[0].GetProperty("resource");
        var attrs = resourceObj.GetProperty("attributes");
        attrs.GetProperty("custom1").GetString().Should().Be("val1");
        attrs.GetProperty("custom2").GetInt32().Should().Be(123);
    }

    [TestMethod]
    public void Format_ManuallySetParentContext_IsReflectedInExport()
    {
        // Arrange
        var manualParentActivity = CreateActivity();
        var parentContext = manualParentActivity.Context;
        var parentSpanId = manualParentActivity.SpanId.ToString();
        var activity = ListenForActivity(() =>
        {
            using var toolScope = ExecuteToolScope.Start(new Request(), new ToolCallDetails("TestTool", "Input: 42"), Util.GetAgentDetails(), spanDetails: new SpanDetails(parentContext: parentContext));
        });

        var resource = ResourceBuilder.CreateDefault().Build();
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatMany(new[] { activity! }, resource);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
        var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
        var spans = scopeSpans[0].GetProperty("spans");
        var span = spans[0];
        var parentSpanIdJson = span.GetProperty("parentSpanId").GetString()?.ToLowerInvariant();
        parentSpanIdJson.Should().Be(parentSpanId.ToLowerInvariant());
    }

    [TestMethod]
    public void Format_NullOrEmptyEventsAndLinks_AreOmitted()
    {
        // Arrange
        var act = CreateActivity();
        var resource = CreateResource();
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatMany(new[] { act }, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
        var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
        var span = scopeSpans[0].GetProperty("spans")[0];

        span.TryGetProperty("events", out var eventsProp).Should().BeFalse();
        span.TryGetProperty("links", out var linksProp).Should().BeFalse();
    }

    [TestMethod]
    public void Format_NullOrEmptyAttributes_AreOmitted()
    {
        // Arrange
        var act = CreateActivity(tags: new Dictionary<string, object>());
        var resource = CreateResource();
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatMany(new[] { act }, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
        var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
        var span = scopeSpans[0].GetProperty("spans")[0];

        span.TryGetProperty("attributes", out var attrsProp).Should().BeTrue();
        attrsProp.EnumerateObject().Should().BeEmpty();
    }

    [TestMethod]
    public void FormatSingle_Activity_AllFieldsMapped()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(123);
        var tags = new Dictionary<string, object>
        {
            { "tag1", "value1" },
            { "tag2", 42 }
        };
        var events = new List<ActivityEvent>
        {
            new ActivityEvent("ev1", startTime, new ActivityTagsCollection { { "evtag", "evval" } })
        };
        var links = new List<ActivityLink>
        {
            new ActivityLink(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded), new ActivityTagsCollection { { "linktag", "linkval" } })
        };

        var activity = CreateActivity(
            sourceName: "TestSource",
            displayName: "span1",
            kind: ActivityKind.Client,
            startTimeUtc: startTime,
            duration: duration,
            tags: tags,
            events: events,
            links: links,
            status: ActivityStatusCode.Error,
            statusDescription: "fail"
        );
        var resource = CreateResource(new Dictionary<string, object> { { "env", "test" } });
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatSingle(activity, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var resourceSpan = root.GetProperty("resourceSpan");
        var scopeSpan = resourceSpan.GetProperty("scopeSpan");
        var scope = scopeSpan.GetProperty("scope");
        scope.GetProperty("name").GetString().Should().Be("TestSource");
        var span = scopeSpan.GetProperty("span");
        span.GetProperty("name").GetString().Should().Be("span1");
        span.GetProperty("kind").GetInt32().Should().Be((int)ActivityKind.Client);
        span.GetProperty("startTimeUnixNano").GetUInt64().Should().BeGreaterThan(0);
        span.GetProperty("endTimeUnixNano").GetUInt64().Should().BeGreaterThan(span.GetProperty("startTimeUnixNano").GetUInt64());
        var attributes = span.GetProperty("attributes");
        attributes.GetProperty("tag1").GetString().Should().Be("value1");
        attributes.GetProperty("tag2").GetInt32().Should().Be(42);
        var eventsJson = span.GetProperty("events");
        var eventJson = eventsJson[0];
        eventJson.GetProperty("name").GetString().Should().Be("ev1");
        eventJson.GetProperty("attributes").GetProperty("evtag").GetString().Should().Be("evval");
        var linksJson = span.GetProperty("links");
        var linkJson = linksJson[0];
        linkJson.GetProperty("attributes").GetProperty("linktag").GetString().Should().Be("linkval");
        var status = span.GetProperty("status");
        status.GetProperty("code").GetInt32().Should().Be((int)ActivityStatusCode.Error);
        status.GetProperty("message").GetString().Should().Be("fail");
    }

    [TestMethod]
    public void FormatSingle_Activity_ResourceAttributesMapped()
    {
        // Arrange
        var act = CreateActivity();
        var resource = CreateResource(new Dictionary<string, object>
        {
            { "custom1", "val1" },
            { "custom2", 123 }
        });
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatSingle(act, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var resourceSpan = root.GetProperty("resourceSpan");
        var resourceObj = resourceSpan.GetProperty("resource");
        var attrs = resourceObj.GetProperty("attributes");
        attrs.GetProperty("custom1").GetString().Should().Be("val1");
        attrs.GetProperty("custom2").GetInt32().Should().Be(123);
    }

    [TestMethod]
    public void FormatSingle_Activity_ParentSpanIdIsMapped()
    {
        // Arrange
        var parentSpanId = ActivitySpanId.CreateRandom();
        var act = CreateActivity(parentSpanId: parentSpanId);
        var resource = CreateResource();
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatSingle(act, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var resourceSpan = root.GetProperty("resourceSpan");
        var scopeSpan = resourceSpan.GetProperty("scopeSpan");
        var span = scopeSpan.GetProperty("span");
        var parentSpanIdJson = span.GetProperty("parentSpanId").GetString();
        parentSpanIdJson.Should().Be(parentSpanId.ToHexString().ToLowerInvariant());
    }

    [TestMethod]
    public void FormatSingle_Activity_NullOrEmptyEventsAndLinksAreOmitted()
    {
        // Arrange
        var act = CreateActivity();
        var resource = CreateResource();
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatSingle(act, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var resourceSpan = root.GetProperty("resourceSpan");
        var scopeSpan = resourceSpan.GetProperty("scopeSpan");
        var span = scopeSpan.GetProperty("span");
        span.TryGetProperty("events", out var eventsProp).Should().BeFalse();
        span.TryGetProperty("links", out var linksProp).Should().BeFalse();
    }

    [TestMethod]
    public void FormatSingle_Activity_NullOrEmptyAttributesAreOmitted()
    {
        // Arrange
        var act = CreateActivity(tags: new Dictionary<string, object>());
        var resource = CreateResource();
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatSingle(act, resource);

        // Assert
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var resourceSpan = root.GetProperty("resourceSpan");
        var scopeSpan = resourceSpan.GetProperty("scopeSpan");
        var span = scopeSpan.GetProperty("span");
        span.TryGetProperty("attributes", out var attrsProp).Should().BeTrue();
        attrsProp.EnumerateObject().Should().BeEmpty();
    }

    private static ulong ToUnixNanos(DateTimeOffset dto)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (ulong)((dto.UtcDateTime - epoch).Ticks * 100);
    }

    [TestMethod]
    public void FormatLogData_WithAllFields_ProducesExpectedJson()
    {
        // Arrange
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        var end = DateTimeOffset.UtcNow;
        var spanId = "span-123";
        var parentSpanId = "parent-456";
        var data = new InvokeAgentData(
            new Dictionary<string, object?>
            {
                { "attr1", "value1" },
                { "attr2", 42 }
            },
            start,
            end,
            spanId,
            parentSpanId,
            spanKind: SpanKindConstants.Client);
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatLogData(data.ToDictionary());

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("Name").GetString().Should().Be("InvokeAgent");
        root.GetProperty("SpanId").GetString().Should().Be(spanId);
        root.GetProperty("ParentSpanId").GetString().Should().Be(parentSpanId);
        root.GetProperty("Kind").GetString().Should().Be(SpanKindConstants.Client);

        var attrs = root.GetProperty("Attributes");
        attrs.GetProperty("attr1").GetString().Should().Be("value1");
        attrs.GetProperty("attr2").GetInt32().Should().Be(42);

        var startNs = root.GetProperty("StartTimeUnixNano").GetUInt64();
        var endNs = root.GetProperty("EndTimeUnixNano").GetUInt64();
        startNs.Should().Be(ToUnixNanos(start));
        endNs.Should().Be(ToUnixNanos(end));
        endNs.Should().BeGreaterThan(startNs);

        // Duration is not part of the serialized payload
        root.TryGetProperty("Duration", out _).Should().BeFalse();
    }

    [TestMethod]
    public void FormatLogData_WithMissingOptionalFields_ProducesDefaults()
    {
        // Arrange
        var explicitSpanId = "explicit-span";
        var data = new InvokeAgentData(
            new Dictionary<string, object?> { { "key", "val" } },
            startTime: null,
            endTime: null,
            spanId: explicitSpanId,
            parentSpanId: null);
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatLogData(data.ToDictionary());

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("Name").GetString().Should().Be("InvokeAgent");
        root.GetProperty("SpanId").GetString().Should().Be(explicitSpanId);
        root.GetProperty("StartTimeUnixNano").GetUInt64().Should().Be(0);
        root.GetProperty("EndTimeUnixNano").GetUInt64().Should().Be(0);

        // ParentSpanId should be omitted due to null (ignore when writing null)
        root.TryGetProperty("ParentSpanId", out _).Should().BeFalse();

        // Kind defaults to Client when SpanKind is null
        root.GetProperty("Kind").GetString().Should().Be(SpanKindConstants.Client);

        var attrs = root.GetProperty("Attributes");
        attrs.GetProperty("key").GetString().Should().Be("val");
    }

#region ExportFormatter FormatMany Truncation Tests

private class ListLogger<T> : ILogger<T>
{
    private readonly List<string> _logs;
    public ListLogger(List<string> logs) => _logs = logs;
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logs.Add(formatter(state, exception));
    }
}

[TestMethod]
public void FormatMany_DoesNothing_WhenUnderLimit()
{
    // Arrange
    using var activity = CreateActivity("tenant-1", "agent-1");
    activity.SetTag("gen_ai.tool.call.arguments", new string('a', 1024)); // 1KB
    var resource = ResourceBuilder.CreateEmpty().Build();
    var logs = new List<string>();
    var formatter = new ExportFormatter(new ListLogger<ExportFormatter>(logs));

    // Act
    var json = formatter.FormatMany(new[] { activity }, resource);

    // Assert
    var doc = JsonDocument.Parse(json);
    var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
    var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
    var span = scopeSpans[0].GetProperty("spans")[0];
    span.GetProperty("attributes").GetProperty("gen_ai.tool.call.arguments").GetString().Should().NotBe("TRUNCATED");
    logs.Should().NotContain(l => l.Contains("Truncated"));
}

[TestMethod]
public void FormatMany_TruncatesSingleLargeKey()
{
    // Arrange
    using var activity = CreateActivity("tenant-1", "agent-2");
    activity.SetTag("gen_ai.tool.call.arguments", new string('b', 300 * 1024)); // 300KB
    var resource = ResourceBuilder.CreateEmpty().Build();
    var logs = new List<string>();
    var formatter = new ExportFormatter(new ListLogger<ExportFormatter>(logs));

    // Act
    var json = formatter.FormatMany(new[] { activity }, resource);

    // Assert
    var doc = JsonDocument.Parse(json);
    var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
    var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
    var span = scopeSpans[0].GetProperty("spans")[0];
    span.GetProperty("attributes").GetProperty("gen_ai.tool.call.arguments").GetString().Should().Be("TRUNCATED");
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.tool.call.arguments' size = "));
    logs.Should().Contain(l => l.Contains("Truncated 'gen_ai.tool.call.arguments'"));
}

[TestMethod]
public void FormatMany_TruncatesMultipleKeys_LargestFirst()
{
    // Arrange
    using var activity = CreateActivity("tenant-1", "agent-1");
    activity.SetTag("gen_ai.tool.call.arguments", new string('c', 200 * 1024));
    activity.SetTag("gen_ai.tool.call.result", new string('d', 100 * 1024));
    var resource = ResourceBuilder.CreateEmpty().Build();
    var logs = new List<string>();
    var formatter = new ExportFormatter(new ListLogger<ExportFormatter>(logs));

    // Act
    var json = formatter.FormatMany(new[] { activity }, resource);

    // Assert
    var doc = JsonDocument.Parse(json);
    var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
    var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
    var span = scopeSpans[0].GetProperty("spans")[0];
    var attr = span.GetProperty("attributes");
    attr.GetProperty("gen_ai.tool.call.arguments").GetString().Should().Be("TRUNCATED");
    logs.Should().Contain(l => l.Contains("Truncated 'gen_ai.tool.call.arguments'"));
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.tool.call.arguments' size = "));
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.tool.call.result' size = "));
}

[TestMethod]
public void FormatMany_LogsAllKeySizes()
{
    // Arrange
    using var activity = CreateActivity("tenant-1", "agent-1");
    activity.SetTag("gen_ai.tool.call.arguments", new string('x', 100 * 1024));
    activity.SetTag("gen_ai.tool.call.result", new string('y', 125 * 1024));
    activity.SetTag("gen_ai.input.messages", new string('z', 75 * 1024));
    activity.SetTag("gen_ai.agent.invocation_input", new string('z', 0));
    activity.SetTag("gen_ai.agent.invocation_output", new string('z', 0));
    activity.SetTag("gen_ai.output.messages", new string('z', 0));
    var resource = ResourceBuilder.CreateEmpty().Build();
    var logs = new List<string>();
    var formatter = new ExportFormatter(new ListLogger<ExportFormatter>(logs));

    // Act
    formatter.FormatMany(new[] { activity }, resource);

    // Assert
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.tool.call.arguments' size = 100"));
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.tool.call.result' size = 125"));
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.input.messages' size = 75"));
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.agent.invocation_input' size = 0"));
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.agent.invocation_output' size = 0"));
    logs.Should().Contain(l => l.Contains("Key 'gen_ai.output.messages' size = 0"));
}

#endregion
}
