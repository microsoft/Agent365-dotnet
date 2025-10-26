using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using OpenTelemetry.Resources;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common
{
    [TestClass]
    public class ExportFormatterTests
    {
        private static Activity CreateActivity(
            string sourceName = "TestSource",
            string? sourceVersion = "1.2.3",
            string displayName = "test-span",
            ActivityKind kind = ActivityKind.Server,
            DateTime? startTimeUtc = null,
            TimeSpan? duration = null,
            Dictionary<string, object>? tags = null,
            List<ActivityEvent>? events = null,
            List<ActivityLink>? links = null,
            ActivitySpanId? parentSpanId = null,
            ActivityStatusCode status = ActivityStatusCode.Ok,
            string? statusDescription = null)
        {
            var source = new ActivitySource(sourceName, sourceVersion);

            using var listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = _ => { },
                ActivityStopped = _ => { }
            };
            ActivitySource.AddActivityListener(listener);

            Activity? activity;
            if (parentSpanId.HasValue)
            {
                var parentContext = new ActivityContext(
                    ActivityTraceId.CreateRandom(),
                    parentSpanId.Value,
                    ActivityTraceFlags.Recorded);
                activity = source.StartActivity(displayName, kind, parentContext);
            }
            else
            {
                activity = source.StartActivity(displayName, kind);
            }

            if (activity == null)
                throw new InvalidOperationException("Failed to start activity.");

            if (startTimeUtc.HasValue)
                activity.SetStartTime(startTimeUtc.Value);

            if (duration.HasValue)
                activity.SetEndTime(activity.StartTimeUtc + duration.Value);

            if (tags != null)
            {
                foreach (var tag in tags)
                    activity.SetTag(tag.Key, tag.Value);
            }

            if (events != null)
            {
                foreach (var ev in events)
                    activity.AddEvent(ev);
            }

            if (links != null)
            {
                foreach (var link in links)
                    activity.AddLink(link);
            }

            activity.SetStatus(status, statusDescription);

            activity.Stop();
            return activity;
        }

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

            // Act
            var json = ExportFormatter.Format(activities, resource);

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
                sourceVersion: "1.2.3",
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

            // Act
            var json = ExportFormatter.Format(activities, resource);

            // Assert
            var doc = JsonDocument.Parse(json);
            var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
            var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
            scopeSpans.GetArrayLength().Should().Be(1);

            var scope = scopeSpans[0].GetProperty("scope");
            scope.GetProperty("name").GetString().Should().Be("TestSource");
            scope.GetProperty("version").GetString().Should().Be("1.2.3");

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
            var act1 = CreateActivity(sourceName: "SourceA", sourceVersion: "1.0", displayName: "spanA");
            var act2 = CreateActivity(sourceName: "SourceA", sourceVersion: "1.0", displayName: "spanB");
            var act3 = CreateActivity(sourceName: "SourceB", sourceVersion: "2.0", displayName: "spanC");

            var activities = new List<Activity> { act1, act2, act3 };
            var resource = CreateResource(new Dictionary<string, object> { { "env", "test" } });

            // Act
            var json = ExportFormatter.Format(activities, resource);

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

            // Act
            var json = ExportFormatter.Format(new[] { act }, resource);

            // Assert
            var doc = JsonDocument.Parse(json);
            var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
            var resourceObj = resourceSpans[0].GetProperty("resource");
            var attrs = resourceObj.GetProperty("attributes");
            attrs.GetProperty("custom1").GetString().Should().Be("val1");
            attrs.GetProperty("custom2").GetInt32().Should().Be(123);
        }

        [TestMethod]
        public void Format_ParentSpanId_IsMapped()
        {
            // Arrange
            var parentSpanId = ActivitySpanId.CreateRandom();
            var act = CreateActivity(parentSpanId: parentSpanId);
            var resource = CreateResource();

            // Act
            var json = ExportFormatter.Format(new[] { act }, resource);

            // Assert
            var doc = JsonDocument.Parse(json);
            var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
            var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
            var span = scopeSpans[0].GetProperty("spans")[0];
            var parentSpanIdJson = span.GetProperty("parentSpanId").GetString();
            parentSpanIdJson.Should().Be(parentSpanId.ToHexString().ToLowerInvariant());
        }

        [TestMethod]
        public void Format_NullOrEmptyEventsAndLinks_AreOmitted()
        {
            // Arrange
            var act = CreateActivity();
            var resource = CreateResource();

            // Act
            var json = ExportFormatter.Format(new[] { act }, resource);

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

            // Act
            var json = ExportFormatter.Format(new[] { act }, resource);

            // Assert
            var doc = JsonDocument.Parse(json);
            var resourceSpans = doc.RootElement.GetProperty("resourceSpans");
            var scopeSpans = resourceSpans[0].GetProperty("scopeSpans");
            var span = scopeSpans[0].GetProperty("spans")[0];

            span.TryGetProperty("attributes", out var attrsProp).Should().BeTrue();
            attrsProp.EnumerateObject().Should().BeEmpty();
        }
    }
}