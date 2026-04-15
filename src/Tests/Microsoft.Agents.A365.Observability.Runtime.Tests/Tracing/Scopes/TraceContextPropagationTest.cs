// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class TraceContextPropagationTest : ActivityTest
{
    [TestMethod]
    public void InjectTraceContext_ReturnsTraceparentHeader()
    {
        // Arrange & Act
        Dictionary<string, string>? headers = null;
        ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                Util.GetDefaultRequest(),
                new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4", "openai"),
                Util.GetAgentDetails());
            headers = scope.InjectTraceContext();
        });

        // Assert
        headers.Should().NotBeNull();
        headers!.Should().ContainKey("traceparent");

        var traceparent = headers!["traceparent"];
        var parts = traceparent.Split('-');
        parts.Should().HaveCount(4, "traceparent should have 4 parts (version-trace_id-span_id-flags)");
        parts[0].Should().Be("00", "version should be 00");
        parts[1].Should().HaveLength(32, "trace_id should be 32 hex chars");
        parts[2].Should().HaveLength(16, "span_id should be 16 hex chars");
        parts[3].Should().HaveLength(2, "flags should be 2 hex chars");
    }

    [TestMethod]
    public void GetActivityContext_ReturnsValidContext()
    {
        // Arrange & Act
        ActivityContext? ctx = null;
        ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                Util.GetDefaultRequest(),
                new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4", "openai"),
                Util.GetAgentDetails());
            ctx = scope.GetActivityContext();
        });

        // Assert
        ctx.Should().NotBeNull();
        ctx!.Value.TraceId.Should().NotBe(default(ActivityTraceId));
        ctx!.Value.SpanId.Should().NotBe(default(ActivitySpanId));
    }

    [TestMethod]
    public void ExtractContextFromHeaders_ParsesTraceparent()
    {
        // Arrange
        var traceId = "1234567890abcdef1234567890abcdef";
        var spanId = "abcdefabcdef1234";
        var traceparent = $"00-{traceId}-{spanId}-01";
        var headers = new Dictionary<string, string> { { "traceparent", traceparent } };

        // Act
        var ctx = TraceContextHelper.ExtractContextFromHeaders(headers);

        // Assert
        ctx.TraceId.ToHexString().Should().Be(traceId);
        ctx.SpanId.ToHexString().Should().Be(spanId);
        ctx.TraceFlags.Should().Be(ActivityTraceFlags.Recorded);
    }

    [TestMethod]
    public void ExtractContextFromHeaders_ReturnsDefault_WhenNoTraceparent()
    {
        // Arrange
        var headers = new Dictionary<string, string>();

        // Act
        var ctx = TraceContextHelper.ExtractContextFromHeaders(headers);

        // Assert
        ctx.TraceId.Should().Be(default(ActivityTraceId));
    }

    [TestMethod]
    public void GetTraceparent_ReturnsValue_WhenPresent()
    {
        // Arrange
        var expected = "00-1234567890abcdef1234567890abcdef-abcdefabcdef1234-01";
        var headers = new Dictionary<string, string> { { "traceparent", expected } };

        // Act
        var result = TraceContextHelper.GetTraceparent(headers);

        // Assert
        result.Should().Be(expected);
    }

    [TestMethod]
    public void GetTraceparent_ReturnsNull_WhenMissing()
    {
        // Arrange
        var headers = new Dictionary<string, string>();

        // Act
        var result = TraceContextHelper.GetTraceparent(headers);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void InjectExtract_RoundTrip_PreservesTraceContext()
    {
        // Arrange - create a parent scope and inject its context
        Dictionary<string, string>? injectedHeaders = null;
        ListenForActivity(() =>
        {
            using var parentScope = InferenceScope.Start(
                Util.GetDefaultRequest(),
                new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4", "openai"),
                Util.GetAgentDetails());
            injectedHeaders = parentScope.InjectTraceContext();
        });

        // Act - extract context from headers and create child scope
        var parentContext = TraceContextHelper.ExtractContextFromHeaders(injectedHeaders!);
        var childActivity = ListenForActivity(() =>
        {
            using var childScope = ExecuteToolScope.Start(
                Util.GetDefaultRequest(),
                new ToolCallDetails("search_tool", "{\"query\": \"test\"}"),
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(parentContext: parentContext));
        });

        // Assert - child should share parent's trace ID
        var parentTraceparent = injectedHeaders!["traceparent"];
        var parentTraceId = parentTraceparent.Split('-')[1];
        var parentSpanId = parentTraceparent.Split('-')[2];
        childActivity.TraceId.ToHexString().Should().Be(parentTraceId, "child should have same trace_id as parent");
        childActivity.ParentSpanId.ToHexString().Should().Be(parentSpanId, "child's parent span should be the parent's span");
    }

    [TestMethod]
    public void InvokeAgentScope_WithParentContext_InheritsTraceId()
    {
        // Arrange
        var traceId = "1234567890abcdef1234567890abcdef";
        var spanId = "abcdefabcdef1234";
        var traceparent = $"00-{traceId}-{spanId}-01";
        var parentContext = TraceContextHelper.ExtractContextFromHeaders(
            new Dictionary<string, string> { { "traceparent", traceparent } });

        // Act
        Dictionary<string, string>? childHeaders = null;
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                ScopeDetails,
                TestAgentDetails,
                spanDetails: new SpanDetails(parentContext: parentContext));
            childHeaders = scope.InjectTraceContext();
        });

        // Assert - span inherits parent's trace ID
        activity.TraceId.ToHexString().Should().Be(traceId);
        activity.ParentSpanId.ToHexString().Should().Be(spanId);

        // Assert - injected headers contain valid traceparent
        childHeaders.Should().ContainKey("traceparent");
    }

    [TestMethod]
    public void ExecuteToolScope_WithExtractedParentContext_SetsParent()
    {
        // Arrange
        var traceId = "abcdef1234567890abcdef1234567890";
        var spanId = "1234567890abcdef";
        var traceparent = $"00-{traceId}-{spanId}-01";
        var parentContext = TraceContextHelper.ExtractContextFromHeaders(
            new Dictionary<string, string> { { "traceparent", traceparent } });

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                Util.GetDefaultRequest(),
                new ToolCallDetails("test-tool", "args"),
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(parentContext: parentContext));
        });

        // Assert
        activity.TraceId.ToHexString().Should().Be(traceId);
        activity.ParentSpanId.ToHexString().Should().Be(spanId);
    }

    [TestMethod]
    public void OutputScope_WithExtractedParentContext_SetsParent()
    {
        // Arrange
        var traceId = "abcdef1234567890abcdef1234567890";
        var spanId = "1234567890abcdef";
        var traceparent = $"00-{traceId}-{spanId}-01";
        var parentContext = TraceContextHelper.ExtractContextFromHeaders(
            new Dictionary<string, string> { { "traceparent", traceparent } });

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetDefaultRequest(),
                new Response(new[] { "test" }),
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(parentContext: parentContext));
        });

        // Assert
        activity.TraceId.ToHexString().Should().Be(traceId);
        activity.ParentSpanId.ToHexString().Should().Be(spanId);
    }
}
