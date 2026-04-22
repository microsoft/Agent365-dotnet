// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

[TestClass]
public sealed class SpanLinksTest : ActivityTest
{
    private static readonly ActivityLink[] SampleLinks = new[]
    {
        new ActivityLink(
            new ActivityContext(
                ActivityTraceId.CreateFromString("0aa4621e5ae09963a3de354f3d18aa65"),
                ActivitySpanId.CreateFromString("c1aaa519600b1bf0"),
                ActivityTraceFlags.Recorded)),
        new ActivityLink(
            new ActivityContext(
                ActivityTraceId.CreateFromString("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                ActivitySpanId.CreateFromString("aaaaaaaaaaaaaaaa"),
                ActivityTraceFlags.None),
            new ActivityTagsCollection(new[] { new KeyValuePair<string, object?>("link.reason", "retry") })),
    };

    [TestMethod]
    public void ExecuteToolScope_RecordsSpanLinks_WithFullContextAndAttributes()
    {
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                Util.GetDefaultRequest(),
                new ToolCallDetails("my-tool", "args"),
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(spanLinks: SampleLinks));
        });

        var links = activity.Links.ToList();
        links.Should().HaveCount(2);
        links[0].Context.TraceId.ToHexString().Should().Be("0aa4621e5ae09963a3de354f3d18aa65");
        links[0].Context.SpanId.ToHexString().Should().Be("c1aaa519600b1bf0");
        links[0].Context.TraceFlags.Should().Be(ActivityTraceFlags.Recorded);
        links[1].Context.TraceId.ToHexString().Should().Be("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        links[1].Context.SpanId.ToHexString().Should().Be("aaaaaaaaaaaaaaaa");
        links[1].Tags.Should().Contain(new KeyValuePair<string, object?>("link.reason", "retry"));
    }

    [TestMethod]
    public void ExecuteToolScope_HasEmptyLinks_WhenSpanLinksOmitted()
    {
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                Util.GetDefaultRequest(),
                new ToolCallDetails("my-tool", "args"),
                Util.GetAgentDetails());
        });

        activity.Links.Should().BeEmpty();
    }

    [TestMethod]
    public void InvokeAgentScope_ForwardsSpanLinks()
    {
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                ScopeDetails,
                TestAgentDetails,
                spanDetails: new SpanDetails(spanLinks: SampleLinks));
        });

        var links = activity.Links.ToList();
        links.Should().HaveCount(2);
        links[0].Context.TraceId.ToHexString().Should().Be("0aa4621e5ae09963a3de354f3d18aa65");
        links[0].Context.SpanId.ToHexString().Should().Be("c1aaa519600b1bf0");
        links[0].Context.TraceFlags.Should().Be(ActivityTraceFlags.Recorded);
        links[1].Context.TraceId.ToHexString().Should().Be("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        links[1].Context.SpanId.ToHexString().Should().Be("aaaaaaaaaaaaaaaa");
        links[1].Context.TraceFlags.Should().Be(ActivityTraceFlags.None);
        links[1].Tags.Should().Contain(new KeyValuePair<string, object?>("link.reason", "retry"));
    }

    [TestMethod]
    public void InferenceScope_ForwardsSpanLinks()
    {
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat, "gpt-4", "openai");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                Util.GetDefaultRequest(),
                details,
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(spanLinks: SampleLinks));
        });

        var links = activity.Links.ToList();
        links.Should().HaveCount(2);
        links[0].Context.TraceId.ToHexString().Should().Be("0aa4621e5ae09963a3de354f3d18aa65");
        links[0].Context.SpanId.ToHexString().Should().Be("c1aaa519600b1bf0");
    }

    [TestMethod]
    public void OutputScope_ForwardsSpanLinks()
    {
        var response = new Response(new[] { "hello" });

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetDefaultRequest(),
                response,
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(spanLinks: SampleLinks));
        });

        var links = activity.Links.ToList();
        links.Should().HaveCount(2);
        links[0].Context.TraceId.ToHexString().Should().Be("0aa4621e5ae09963a3de354f3d18aa65");
        links[0].Context.SpanId.ToHexString().Should().Be("c1aaa519600b1bf0");
    }

    [TestMethod]
    public void SpanLinks_PreservesTypedAttributes()
    {
        var linksWithAttrs = new[]
        {
            new ActivityLink(
                new ActivityContext(
                    ActivityTraceId.CreateFromString("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                    ActivitySpanId.CreateFromString("bbbbbbbbbbbbbbbb"),
                    ActivityTraceFlags.Recorded),
                new ActivityTagsCollection(new[]
                {
                    new KeyValuePair<string, object?>("link.type", "causal"),
                    new KeyValuePair<string, object?>("link.index", 0),
                })),
        };

        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                ScopeDetails,
                new AgentDetails("attr-agent"),
                spanDetails: new SpanDetails(spanLinks: linksWithAttrs));
        });

        var links = activity.Links.ToList();
        links.Should().HaveCount(1);
        links[0].Tags.Should().Contain(new KeyValuePair<string, object?>("link.type", "causal"));
        links[0].Tags.Should().Contain(new KeyValuePair<string, object?>("link.index", 0));
    }
}
