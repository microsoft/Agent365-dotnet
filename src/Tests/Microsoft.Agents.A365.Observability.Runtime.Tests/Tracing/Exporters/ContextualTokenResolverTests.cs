// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry;
using OpenTelemetry.Resources;
using System.Diagnostics;
using System.Net;

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Exporters;

[TestClass]
public sealed class ContextualTokenResolverTests
{
    private static readonly Agent365ExporterCore Core = new(
        new ExportFormatter(NullLogger<ExportFormatter>.Instance),
        NullLogger<Agent365ExporterCore>.Instance);

    #region AgentIdentity Tests

    [TestMethod]
    public void AgentIdentity_Constructor_SetsProperties()
    {
        var identity = new AgentIdentity("agent-1", "user-oid-123");

        identity.AgentId.Should().Be("agent-1");
        identity.AgenticUserId.Should().Be("user-oid-123");
    }

    [TestMethod]
    public void AgentIdentity_Constructor_NullAgenticUserId_IsValid()
    {
        var identity = new AgentIdentity("agent-1");

        identity.AgentId.Should().Be("agent-1");
        identity.AgenticUserId.Should().BeNull();
    }

    [TestMethod]
    public void AgentIdentity_Constructor_ExplicitNull_IsValid()
    {
        var identity = new AgentIdentity("agent-1", null);

        identity.AgentId.Should().Be("agent-1");
        identity.AgenticUserId.Should().BeNull();
    }

    #endregion

    #region TokenResolverContext Tests

    [TestMethod]
    public void TokenResolverContext_Constructor_SetsProperties()
    {
        var identity = new AgentIdentity("agent-1", "user-oid-123");
        var context = new TokenResolverContext(identity, "tenant-abc");

        context.Identity.Should().BeSameAs(identity);
        context.TenantId.Should().Be("tenant-abc");
        context.Identity.AgentId.Should().Be("agent-1");
        context.Identity.AgenticUserId.Should().Be("user-oid-123");
    }

    [TestMethod]
    public void TokenResolverContext_S2SScenario_NullAgenticUserId()
    {
        var identity = new AgentIdentity("agent-1");
        var context = new TokenResolverContext(identity, "tenant-abc");

        context.Identity.AgenticUserId.Should().BeNull();
    }

    #endregion

    #region Exporter Constructor Validation Tests

    [TestMethod]
    public void Constructor_NeitherResolver_Throws()
    {
        var options = new Agent365ExporterOptions
        {
            TokenResolver = null,
            ContextualTokenResolver = null,
        };

        Action act = () => _ = new Agent365Exporter(Core, NullLogger<Agent365Exporter>.Instance, options, null);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Constructor_OnlyTokenResolver_Succeeds()
    {
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("token"),
        };

        var exporter = new Agent365Exporter(Core, NullLogger<Agent365Exporter>.Instance, options, null);
        exporter.Should().NotBeNull();
    }

    [TestMethod]
    public void Constructor_OnlyContextualTokenResolver_Succeeds()
    {
        var options = new Agent365ExporterOptions
        {
            ContextualTokenResolver = ctx => Task.FromResult<string?>("token"),
        };

        var exporter = new Agent365Exporter(Core, NullLogger<Agent365Exporter>.Instance, options, null);
        exporter.Should().NotBeNull();
    }

    [TestMethod]
    public void Constructor_BothResolvers_Succeeds()
    {
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("legacy-token"),
            ContextualTokenResolver = ctx => Task.FromResult<string?>("contextual-token"),
        };

        var exporter = new Agent365Exporter(Core, NullLogger<Agent365Exporter>.Instance, options, null);
        exporter.Should().NotBeNull();
    }

    #endregion

    #region ContextualTokenResolver Precedence Tests

    [TestMethod]
    public async Task ExportBatchCoreAsync_ContextualResolver_TakesPrecedence()
    {
        // Arrange
        string? capturedAgentId = null;
        string? capturedTenantId = null;
        string? capturedAuid = null;
        bool legacyResolverCalled = false;

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) =>
            {
                legacyResolverCalled = true;
                return Task.FromResult<string?>("legacy-token");
            },
            ContextualTokenResolver = ctx =>
            {
                capturedAgentId = ctx.Identity.AgentId;
                capturedTenantId = ctx.TenantId;
                capturedAuid = ctx.Identity.AgenticUserId;
                return Task.FromResult<string?>("contextual-token");
            },
        };

        using var activity = CreateActivityWithAuid("tenant-1", "agent-1", "user-oid-123");
        var groups = new List<(string TenantId, string AgentId, List<Activity> Activities)>
        {
            ("tenant-1", "agent-1", new List<Activity> { activity })
        };

        // Act
        var result = await Core.ExportBatchCoreAsync(
            groups,
            ResourceBuilder.CreateEmpty().Build(),
            options,
            (agentId, tenantId) => options.TokenResolver!(agentId, tenantId),
            request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        result.Should().Be(ExportResult.Success);
        legacyResolverCalled.Should().BeFalse("ContextualTokenResolver should take precedence");
        capturedAgentId.Should().Be("agent-1");
        capturedTenantId.Should().Be("tenant-1");
        capturedAuid.Should().Be("user-oid-123");
    }

    [TestMethod]
    public async Task ExportBatchCoreAsync_LegacyResolver_UsedWhenNoContextual()
    {
        // Arrange
        bool legacyResolverCalled = false;
        string? capturedAgentId = null;
        string? capturedTenantId = null;

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (agentId, tenantId) =>
            {
                legacyResolverCalled = true;
                capturedAgentId = agentId;
                capturedTenantId = tenantId;
                return Task.FromResult<string?>("legacy-token");
            },
            ContextualTokenResolver = null,
        };

        using var activity = CreateActivityWithAuid("tenant-1", "agent-1", "user-oid-123");
        var groups = new List<(string TenantId, string AgentId, List<Activity> Activities)>
        {
            ("tenant-1", "agent-1", new List<Activity> { activity })
        };

        // Act
        var result = await Core.ExportBatchCoreAsync(
            groups,
            ResourceBuilder.CreateEmpty().Build(),
            options,
            (agentId, tenantId) => options.TokenResolver!(agentId, tenantId),
            request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        result.Should().Be(ExportResult.Success);
        legacyResolverCalled.Should().BeTrue();
        capturedAgentId.Should().Be("agent-1");
        capturedTenantId.Should().Be("tenant-1");
    }

    [TestMethod]
    public async Task ExportBatchCoreAsync_ContextualResolver_NullAuid_InS2SScenario()
    {
        // Arrange
        string? capturedAuid = "sentinel";

        var options = new Agent365ExporterOptions
        {
            ContextualTokenResolver = ctx =>
            {
                capturedAuid = ctx.Identity.AgenticUserId;
                return Task.FromResult<string?>("token");
            },
        };

        // Activity without AUID tag
        using var activity = CreateActivityWithAuid("tenant-1", "agent-1", null);
        var groups = new List<(string TenantId, string AgentId, List<Activity> Activities)>
        {
            ("tenant-1", "agent-1", new List<Activity> { activity })
        };

        // Act
        var result = await Core.ExportBatchCoreAsync(
            groups,
            ResourceBuilder.CreateEmpty().Build(),
            options,
            (_, _) => Task.FromResult<string?>(null),
            request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        result.Should().Be(ExportResult.Success);
        capturedAuid.Should().BeNull("S2S scenario should pass null AgenticUserId");
    }

    [TestMethod]
    public async Task ExportBatchCoreAsync_ContextualResolver_ReturnsNull_Fails()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            ContextualTokenResolver = ctx => Task.FromResult<string?>(null),
        };

        using var activity = CreateActivityWithAuid("tenant-1", "agent-1", "user-oid-123");
        var groups = new List<(string TenantId, string AgentId, List<Activity> Activities)>
        {
            ("tenant-1", "agent-1", new List<Activity> { activity })
        };

        // Act
        var result = await Core.ExportBatchCoreAsync(
            groups,
            ResourceBuilder.CreateEmpty().Build(),
            options,
            (_, _) => Task.FromResult<string?>(null),
            request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        result.Should().Be(ExportResult.Failure, "null token should result in failure");
    }

    [TestMethod]
    public async Task ExportBatchCoreAsync_ContextualResolver_Throws_Fails()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            ContextualTokenResolver = ctx => throw new InvalidOperationException("auth error"),
        };

        using var activity = CreateActivityWithAuid("tenant-1", "agent-1", "user-oid-123");
        var groups = new List<(string TenantId, string AgentId, List<Activity> Activities)>
        {
            ("tenant-1", "agent-1", new List<Activity> { activity })
        };

        // Act
        var result = await Core.ExportBatchCoreAsync(
            groups,
            ResourceBuilder.CreateEmpty().Build(),
            options,
            (_, _) => Task.FromResult<string?>(null),
            request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        result.Should().Be(ExportResult.Failure, "exception in resolver should not crash, but fail export");
    }

    #endregion

    #region Helpers

    private static Activity CreateActivityWithAuid(string? tenantId, string? agentId, string? agenticUserId)
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Agent365Sdk.ContextualTest",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);

        var source = new ActivitySource("Agent365Sdk.ContextualTest");
        var activity = source.StartActivity("test-span", ActivityKind.Client)!;

        activity.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, "invoke_agent");

        if (tenantId != null)
            activity.SetTag(OpenTelemetryConstants.TenantIdKey, tenantId);
        if (agentId != null)
            activity.SetTag(OpenTelemetryConstants.GenAiAgentIdKey, agentId);
        if (agenticUserId != null)
            activity.SetTag(OpenTelemetryConstants.AgentAUIDKey, agenticUserId);

        activity.Stop();
        return activity;
    }

    #endregion
}
