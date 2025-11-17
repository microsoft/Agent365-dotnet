// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts.Details;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using OpenTelemetry;
using OpenTelemetry.Trace;
using static Microsoft.Agents.A365.Observability.Contracts.OpenTelemetryConstants;

namespace Microsoft.Agents.A365.Observability.Contracts.Tests;

public abstract class ActivityTest
{
    protected const string AgentId = "agentId";
    
    protected readonly InvokeAgentDetails Details = new(
        new Uri("https://microsoft.com"),
        new AgentDetails(AgentId));
    
    protected ActivityTest()
    {
        AppContext.SetSwitch(EnableOpenTelemetrySwitch, true);
    }

    protected TracerProvider ConstructTracerProvider()
    {
        return Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddProcessor(new ActivityProcessor())
            .Build();
    }

    protected Activity ListenForActivity(Action action)
    {
        Activity? startedActivity = null;
        using var activityListener = new ActivityListener();
        activityListener.ShouldListenTo = source => source.Name == SourceName;
        activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        activityListener.ActivityStarted = activity => startedActivity = activity;
        ActivitySource.AddActivityListener(activityListener);
        action();
        startedActivity.Should().NotBeNull();
        return startedActivity!;
    }

    protected Activity CreateActivity(
            string sourceName = SourceName,
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
        var source = new ActivitySource(sourceName);

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
}