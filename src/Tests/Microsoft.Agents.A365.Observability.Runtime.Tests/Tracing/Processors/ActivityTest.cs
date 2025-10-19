namespace Microsoft.Agents.A365.Observability.Tests.Tracing;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using OpenTelemetry;
using OpenTelemetry.Trace;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

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
}