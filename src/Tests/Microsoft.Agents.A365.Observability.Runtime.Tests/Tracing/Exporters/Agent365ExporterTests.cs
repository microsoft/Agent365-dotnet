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
using System.Reflection;
using System.Net;

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Exporters;

[TestClass]
public sealed class Agent365ExporterTests
{
    /// <summary>
    /// All valid cluster categories from PowerPlatformApiDiscovery.GetEnvironmentApiHostNameSuffix()
    /// </summary>
    private static readonly string[] ValidClusterCategories = new[]
    {
        "firstrelease", "prod", "production", "gov", "high", "dod", "mooncake", "ex", "rx"
    };

    private static Activity CreateActivity(string? tenantId = null, string? agentId = null, string? operationName = "invoke_agent")
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Agent365Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);

        var source = new ActivitySource("Agent365Sdk");
        var activity = source.StartActivity("test-span", ActivityKind.Client);
        if (activity == null)
            throw new InvalidOperationException("Failed to start activity. Ensure an ActivityListener is registered.");

        if (operationName != null)
        {
            activity.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, operationName);
        }
        if (tenantId != null)
        {
            activity.SetTag(OpenTelemetryConstants.TenantIdKey, tenantId);
        }
        if (agentId != null)
        {
            activity.SetTag(OpenTelemetryConstants.GenAiAgentIdKey, agentId);
        }
        activity.Stop();
        return activity;
    }

    private static Batch<Activity> CreateBatch(params Activity[] activities)
    {
        // Batch<T> has an internal ctor; use reflection
        var batchType = typeof(Batch<Activity>);
        var ctor = batchType
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault();

        if (ctor == null)
        {
            Assert.Inconclusive("Could not locate internal Batch<Activity> constructor - OpenTelemetry version changed.");
        }

        // Create a CircularBuffer<Activity> from the activities array
        var circularBufferType = batchType.Assembly.GetType("OpenTelemetry.Internal.CircularBuffer`1")!
            .MakeGenericType(typeof(Activity));
        var buffer = Activator.CreateInstance(circularBufferType, activities.Length);
        
        if (buffer == null)
        {
            Assert.Inconclusive("Could not create CircularBuffer<Activity> - Activator.CreateInstance returned null.");
        }
        
        var addMethod = circularBufferType.GetMethod("Add");
        foreach (var act in activities)
        {
            addMethod!.Invoke(buffer, new object[] { act });
        }

        object? batchObj;
        try
        {
            batchObj = ctor.Invoke(new object[] { buffer, activities.Length });
        }
        catch (TargetParameterCountException)
        {
            Assert.Inconclusive("Unexpected Batch<Activity> constructor shape - adjust test helper.");
            throw;
        }

        return (Batch<Activity>)batchObj!;
    }

    private static readonly Agent365ExporterCore _agent365ExporterCore = new Agent365ExporterCore(new ExportFormatter(NullLogger<ExportFormatter>.Instance), NullLogger<Agent365ExporterCore>.Instance);

    private static Agent365Exporter CreateExporter(Func<string, string, string?>? tokenResolver)
    {
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("token")
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        return new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);
    }

    [TestMethod]
    public void Constructor_NullLogger_Throws()
    {
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("token")
        };
        Action act = () => _ = new Agent365Exporter(Agent365ExporterTests._agent365ExporterCore, null!, options, resource: null);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [TestMethod]
    public void Constructor_NullOptions_Throws()
    {
        Action act = () => _ = new Agent365Exporter(Agent365ExporterTests._agent365ExporterCore, NullLogger<Agent365Exporter>.Instance, null!, null);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [TestMethod]
    public void Constructor_NullTokenResolver_Throws()
    {
        var options = new Agent365ExporterOptions
        {
            TokenResolver = null
        };
        Action act = () => _ = new Agent365Exporter(Agent365ExporterTests._agent365ExporterCore, NullLogger<Agent365Exporter>.Instance, options, null);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("TokenResolver");
    }

    [TestMethod]
    public void Export_NoIdentityActivities_ReturnsSuccess()
    {
        var exporter = CreateExporter((_, _) => "token");
        using var act1 = CreateActivity(); // No tenant / agent tags
        using var act2 = CreateActivity(); // No tenant / agent tags

        var batch = CreateBatch(act1, act2);
        var result = exporter.Export(in batch);

        result.Should().Be(ExportResult.Success);
    }

    [TestMethod]
    public void Export_WithIdentity_TokenResolverThrows_ReturnsFailure()
    {
        var exporter = CreateExporter((_, _) => throw new InvalidOperationException("Resolver failed"));
        using var act = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");

        var batch = CreateBatch(act);
        var result = exporter.Export(in batch);

        result.Should().Be(ExportResult.Failure);
    }

    [TestMethod]
    public void PartitionByIdentity_GroupsActivitiesByTenantAndAgent()
    {
        // Arrange
        using var a1 = CreateActivity("tenant-1", "agent-1");
        using var a2 = CreateActivity("tenant-1", "agent-1");
        using var a3 = CreateActivity("tenant-1", "agent-2");
        using var a4 = CreateActivity("tenant-1", null);      // ignored (missing agent)
        using var a5 = CreateActivity(null, "agent-3");       // ignored (missing tenant)
        using var a6 = CreateActivity();                       // ignored (missing both)

        var batch = CreateBatch(a1, a2, a3, a4, a5, a6);

        // Act
        var groups = Agent365ExporterTests._agent365ExporterCore.PartitionByIdentity(in batch);

        // Assert
        groups.Should().HaveCount(2);
        groups.Should().Contain(g => g.TenantId == "tenant-1" && g.AgentId == "agent-1" && g.Activities.Count == 2);
        groups.Should().Contain(g => g.TenantId == "tenant-1" && g.AgentId == "agent-2" && g.Activities.Count == 1);
    }

    [TestMethod]
    public void PartitionByIdentity_FiltersOutNonGenAISpans()
    {
        // Arrange
        using var genaiSpan = CreateActivity("tenant-1", "agent-1", operationName: "invoke_agent");
        using var httpSpan = CreateActivity("tenant-1", "agent-1", operationName: null);
        using var dbSpan = CreateActivity("tenant-1", "agent-1", operationName: "some_random_op");
        using var chatSpan = CreateActivity("tenant-1", "agent-1", operationName: "Chat");

        var batch = CreateBatch(genaiSpan, httpSpan, dbSpan, chatSpan);

        // Act
        var groups = Agent365ExporterTests._agent365ExporterCore.PartitionByIdentity(in batch);

        // Assert
        groups.Should().HaveCount(1);
        groups.Should().Contain(g => g.TenantId == "tenant-1" && g.AgentId == "agent-1" && g.Activities.Count == 2);
    }

    [TestMethod]
    public void Agent365ExporterOptions_DefaultBatchingParameters_AreSet()
    {
        // Arrange & Act
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("token")
        };

        // Assert
        options.MaxQueueSize.Should().Be(2048);
        options.ScheduledDelayMilliseconds.Should().Be(5000);
        options.ExporterTimeoutMilliseconds.Should().Be(30000);
        options.MaxExportBatchSize.Should().Be(512);
    }

    [TestMethod]
    public void Agent365ExporterOptions_CustomBatchingParameters_CanBeSet()
    {
        // Arrange & Act
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("token"),
            MaxQueueSize = 4096,
            ScheduledDelayMilliseconds = 10000,
            ExporterTimeoutMilliseconds = 60000,
            MaxExportBatchSize = 1024
        };

        // Assert
        options.MaxQueueSize.Should().Be(4096);
        options.ScheduledDelayMilliseconds.Should().Be(10000);
        options.ExporterTimeoutMilliseconds.Should().Be(60000);
        options.MaxExportBatchSize.Should().Be(1024);
    }

    [TestMethod]
    public void Agent365ExporterOptions_UseS2SEndpoint_DefaultsToFalse()
    {
        // Arrange & Act
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("token")
        };

        // Assert
        options.UseS2SEndpoint.Should().BeFalse();
    }

    [TestMethod]
    public void Agent365ExporterOptions_UseS2SEndpoint_CanBeSetToTrue()
    {
        // Arrange & Act
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("token"),
            UseS2SEndpoint = true
        };

        // Assert
        options.UseS2SEndpoint.Should().BeTrue();
    }

    [TestMethod]
    public void Agent365ExporterOptions_UseS2SEndpoint_CanBeSetToFalse()
    {
        // Arrange & Act
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("token"),
            UseS2SEndpoint = false
        };

        // Assert
        options.UseS2SEndpoint.Should().BeFalse();
    }

    #region S2S Endpoint Functional Tests

    [TestMethod]
    public void UseS2SEndpoint_WhenFalse_UsesStandardEndpoint()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var exporter = CreateExporter((_, _) => "test-token");
        using var activity = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        options.UseS2SEndpoint.Should().BeFalse();
        result.Should().Be(ExportResult.Failure); // Expected to fail as there's no real endpoint
    }

    [TestMethod]
    public void UseS2SEndpoint_WhenTrue_UsesS2SEndpoint()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        options.UseS2SEndpoint.Should().BeTrue();
        result.Should().Be(ExportResult.Failure); // Expected to fail as there's no real endpoint
    }

    [TestMethod]
    public void UseS2SEndpoint_CanBeToggled_FromFalseToTrue()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        // Act
        options.UseS2SEndpoint = true;

        // Assert
        options.UseS2SEndpoint.Should().BeTrue();
    }

    [TestMethod]
    public void UseS2SEndpoint_CanBeToggled_FromTrueToFalse()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        // Act
        options.UseS2SEndpoint = false;

        // Assert
        options.UseS2SEndpoint.Should().BeFalse();
    }

    [TestMethod]
    public void Export_WithMultipleActivities_StandardEndpoint_GroupsByIdentity()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        // Create activities for different tenant/agent combinations
        using var activity1 = CreateActivity("tenant-1", "agent-1");
        using var activity2 = CreateActivity("tenant-1", "agent-1");
        using var activity3 = CreateActivity("tenant-2", "agent-2");

        var batch = CreateBatch(activity1, activity2, activity3);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure); // Expected to fail due to no real endpoint
    }

    [TestMethod]
    public void Export_WithMultipleActivities_S2SEndpoint_GroupsByIdentity()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        // Create activities for different tenant/agent combinations
        using var activity1 = CreateActivity("tenant-1", "agent-1");
        using var activity2 = CreateActivity("tenant-1", "agent-1");
        using var activity3 = CreateActivity("tenant-2", "agent-2");

        var batch = CreateBatch(activity1, activity2, activity3);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure); // Expected to fail due to no real endpoint
    }

    [TestMethod]
    public void Export_S2SEndpoint_TokenResolverCalled_WithCorrectParameters()
    {
        // Arrange
        string? capturedAgentId = null;
        string? capturedTenantId = null;

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (agentId, tenantId) =>
            {
                capturedAgentId = agentId;
                capturedTenantId = tenantId;
                return Task.FromResult<string?>("test-token");
            },
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        capturedAgentId.Should().Be("agent-456");
        capturedTenantId.Should().Be("tenant-123");
    }

    [TestMethod]
    public void Export_StandardEndpoint_TokenResolverCalled_WithCorrectParameters()
    {
        // Arrange
        string? capturedAgentId = null;
        string? capturedTenantId = null;

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (agentId, tenantId) =>
            {
                capturedAgentId = agentId;
                capturedTenantId = tenantId;
                return Task.FromResult<string?>("test-token");
            },
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        capturedAgentId.Should().Be("agent-456");
        capturedTenantId.Should().Be("tenant-123");
    }

    [TestMethod]
    public void Export_S2SEndpoint_NullToken_StillSendsRequest()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>(null), // Return null token
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure); // Expected to fail due to no real endpoint
    }

    [TestMethod]
    public void Export_S2SEndpoint_EmptyToken_StillSendsRequest()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>(string.Empty), // Return empty token
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure); // Expected to fail due to no real endpoint
    }

    [TestMethod]
    public void Export_S2SEndpoint_TokenResolverThrows_ReturnsFailure()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromException<string?>(new InvalidOperationException("Token resolver failed")),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure);
    }

    [TestMethod]
    public void Export_StandardEndpoint_TokenResolverThrows_ReturnsFailure()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromException<string?>(new InvalidOperationException("Token resolver failed")),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure);
    }

    [TestMethod]
    public void Export_S2SEndpoint_ActivitiesWithoutIdentity_ReturnsSuccess()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity1 = CreateActivity(); // No tenant/agent
        using var activity2 = CreateActivity(); // No tenant/agent

        var batch = CreateBatch(activity1, activity2);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
    }

    [TestMethod]
    public void Export_StandardEndpoint_ActivitiesWithoutIdentity_ReturnsSuccess()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity1 = CreateActivity(); // No tenant/agent
        using var activity2 = CreateActivity(); // No tenant/agent

        var batch = CreateBatch(activity1, activity2);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
    }

    [TestMethod]
    public void Export_S2SEndpoint_ActivityWithOnlyTenantId_IsSkipped()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: null);
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
    }

    [TestMethod]
    public void Export_S2SEndpoint_ActivityWithOnlyAgentId_IsSkipped()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: null, agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
    }

    [TestMethod]
    public void Export_StandardEndpoint_ActivityWithOnlyTenantId_IsSkipped()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
                Agent365ExporterTests._agent365ExporterCore,
                NullLogger<Agent365Exporter>.Instance,
                options,
                resource);

        using var activity = CreateActivity(tenantId: "tenant-123", agentId: null);
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
    }

    [TestMethod]
    public void Export_StandardEndpoint_ActivityWithOnlyAgentId_IsSkipped()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity(tenantId: null, agentId: "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
    }

    [TestMethod]
    public void Export_S2SEndpoint_MixedBatch_ProcessesOnlyValidActivities()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var validActivity = CreateActivity("tenant-123", "agent-456");
        using var noTenant = CreateActivity(null, "agent-789");
        using var noAgent = CreateActivity("tenant-456", null);
        using var noIdentity = CreateActivity();

        var batch = CreateBatch(validActivity, noTenant, noAgent, noIdentity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure);
    }

    [TestMethod]
    public void Export_StandardEndpoint_MixedBatch_ProcessesOnlyValidActivities()
    {
        // Arrange
        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var validActivity = CreateActivity("tenant-123", "agent-456");
        using var noTenant = CreateActivity(null, "agent-789");
        using var noAgent = CreateActivity("tenant-456", null);
        using var noIdentity = CreateActivity();

        var batch = CreateBatch(validActivity, noTenant, noAgent, noIdentity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure);
    }

    [TestMethod]
    public void Export_S2SEndpoint_WithCustomResource_ProcessesCorrectly()
    {
        // Arrange
        var resource = ResourceBuilder.CreateEmpty()
            .AddService("custom-service", serviceVersion: "2.0.0")
            .AddAttributes(new Dictionary<string, object>
            {
                ["custom.attribute"] = "custom-value",
                ["environment"] = "test"
            })
            .Build();

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity("tenant-123", "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure); // Expected to fail due to no real endpoint
    }

    [TestMethod]
    public void Export_StandardEndpoint_WithCustomResource_ProcessesCorrectly()
    {
        // Arrange
        var resource = ResourceBuilder.CreateEmpty()
            .AddService("custom-service", serviceVersion: "2.0.0")
            .AddAttributes(new Dictionary<string, object>
            {
                ["custom.attribute"] = "custom-value",
                ["environment"] = "test"
            })
            .Build();

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);

        using var activity = CreateActivity("tenant-123", "agent-456");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Failure); // Expected to fail due to no real endpoint
    }

    [TestMethod]
    public void Export_S2SEndpoint_WithDifferentClusterCategories_ProcessesCorrectly()
    {
        // Test with all valid cluster categories from PowerPlatformApiDiscovery.GetEnvironmentApiHostNameSuffix
        foreach (var category in ValidClusterCategories)
        {
            // Arrange
            var options = new Agent365ExporterOptions
            {
                ClusterCategory = category,
                TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
                UseS2SEndpoint = true
            };

            var resource = ResourceBuilder.CreateEmpty()
                .AddService("unit-test-service", serviceVersion: "1.0.0")
                .Build();

            var exporter = new Agent365Exporter(
                Agent365ExporterTests._agent365ExporterCore,
                NullLogger<Agent365Exporter>.Instance,
                options,
                resource);

            using var activity = CreateActivity("tenant-123", "agent-456");
            var batch = CreateBatch(activity);

            // Act
            var result = exporter.Export(in batch);

            // Assert
            result.Should().Be(ExportResult.Failure, $"cluster category '{category}' should be processed"); // Expected to fail due to no real endpoint
        }
    }

    [TestMethod]
    public void Export_StandardEndpoint_WithDifferentClusterCategories_ProcessesCorrectly()
    {
        // Test with all valid cluster categories from PowerPlatformApiDiscovery.GetEnvironmentApiHostNameSuffix
        foreach (var category in ValidClusterCategories)
        {
            // Arrange
            var options = new Agent365ExporterOptions
            {
                ClusterCategory = category,
                TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
                UseS2SEndpoint = false
            };

            var resource = ResourceBuilder.CreateEmpty()
                .AddService("unit-test-service", serviceVersion: "1.0.0")
                .Build();

            var exporter = new Agent365Exporter(
                Agent365ExporterTests._agent365ExporterCore,
                NullLogger<Agent365Exporter>.Instance,
                options,
                resource);

            using var activity = CreateActivity("tenant-123", "agent-456");
            var batch = CreateBatch(activity);

            // Act
            var result = exporter.Export(in batch);

            // Assert
            result.Should().Be(ExportResult.Failure, $"cluster category '{category}' should be processed"); // Expected to fail due to no real endpoint
        }
    }

    #endregion

    #region Domain Override Functional Tests

    [TestMethod]
    public void Export_RequestUri_EnvVar_Overrides_CustomDomainResolver_WhenBothSet()
    {
        // Arrange
        var overrideDomain = "override.example.com";
        Environment.SetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE", overrideDomain);
        var resolverDomain = "resolver.example.com"; // should be ignored due to env var

        string? observedUri = null;
        var handler = new TestHttpMessageHandler(req =>
        {
            observedUri = req.RequestUri?.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false,
            DomainResolver = tenantId => resolverDomain
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource,
            httpClient);

        using var activity = CreateActivity(tenantId: "tenant-env-overrides", agentId: "agent-xyz");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
        observedUri.Should().NotBeNull();
        observedUri!.Should().StartWith($"https://{overrideDomain}");
        observedUri!.Should().Contain($"/observability/tenants/tenant-env-overrides/otlp/agents/agent-xyz/traces");
        observedUri!.Should().Contain("api-version=1");

        // Cleanup
        Environment.SetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE", null);
    }

    [TestMethod]
    public void Export_RequestUri_UsesEnvVar_WhenNoResolverSet()
    {
        // Arrange
        var overrideDomain = "override.example.com";
        Environment.SetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE", overrideDomain);

        string? observedUri = null;
        var handler = new TestHttpMessageHandler(req =>
        {
            observedUri = req.RequestUri?.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false,
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource,
            httpClient);

        using var activity = CreateActivity(tenantId: "tenant-env", agentId: "agent-xyz");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
        observedUri.Should().NotBeNull();
        observedUri!.Should().StartWith($"https://{overrideDomain}");
        observedUri!.Should().Contain($"/observability/tenants/tenant-env/otlp/agents/agent-xyz/traces");
        observedUri!.Should().Contain("api-version=1");

        // Cleanup
        Environment.SetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE", null);
    }

    [TestMethod]
    public void Export_RequestUri_UsesCustomDomainResolver_WhenProvided()
    {
        // Arrange
        var resolverDomain = "resolver.example.com";
        // Ensure env variable is not set so resolver is used
        Environment.SetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE", null);

        string? observedUri = null;
        var handler = new TestHttpMessageHandler(req =>
        {
            observedUri = req.RequestUri?.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false,
            DomainResolver = tenantId => resolverDomain
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource,
            httpClient);

        using var activity = CreateActivity(tenantId: "tenant-resolver", agentId: "agent-xyz");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
        observedUri.Should().NotBeNull();
        observedUri!.Should().StartWith($"https://{resolverDomain}");
        observedUri!.Should().Contain($"/observability/tenants/tenant-resolver/otlp/agents/agent-xyz/traces");
        observedUri!.Should().Contain("api-version=1");
        // Cleanup
        Environment.SetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE", null);
    }

    [TestMethod]
    public void Export_RequestUri_UsesDefaultEndpoint_WhenNoResolverAndNoEnvVarSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE", null);

        string? observedUri = null;
        var handler = new TestHttpMessageHandler(req =>
        {
            observedUri = req.RequestUri?.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource,
            httpClient);

        var tenantId = "tenant-discovery-noset";
        using var activity = CreateActivity(tenantId: tenantId, agentId: "agent-xyz");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
        observedUri.Should().NotBeNull();
        observedUri!.Should().StartWith($"https://{Agent365ExporterOptions.DefaultEndpointHost}");
        observedUri!.Should().Contain($"/observability/tenants/{tenantId}/otlp/agents/agent-xyz/traces");
        observedUri!.Should().Contain("api-version=1");
    }

    [TestMethod]
    public void Export_RequestUri_S2SEndpoint_ContainsOtlpSegment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE", null);

        string? observedUri = null;
        var handler = new TestHttpMessageHandler(req =>
        {
            observedUri = req.RequestUri?.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);

        var options = new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
            Agent365ExporterTests._agent365ExporterCore,
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource,
            httpClient);

        var tenantId = "tenant-s2s";
        using var activity = CreateActivity(tenantId: tenantId, agentId: "agent-xyz");
        var batch = CreateBatch(activity);

        // Act
        var result = exporter.Export(in batch);

        // Assert
        result.Should().Be(ExportResult.Success);
        observedUri.Should().NotBeNull();
        observedUri!.Should().Contain($"/observabilityService/tenants/{tenantId}/otlp/agents/agent-xyz/traces");
        observedUri!.Should().Contain("api-version=1");
    }

    #endregion

    #region BuildRequestUri Tests

    [TestMethod]
    public void BuildRequestUri_BareHost_PrependsHttps()
    {
        var uri = _agent365ExporterCore.BuildRequestUri("agent365.svc.cloud.microsoft", "/observability/tenants/t1/otlp/agents/a1/traces");
        uri.Should().Be("https://agent365.svc.cloud.microsoft/observability/tenants/t1/otlp/agents/a1/traces?api-version=1");
    }

    [TestMethod]
    public void BuildRequestUri_HttpsEndpoint_DoesNotDoublePrependScheme()
    {
        var uri = _agent365ExporterCore.BuildRequestUri("https://custom.example.com", "/observability/tenants/t1/otlp/agents/a1/traces");
        uri.Should().Be("https://custom.example.com/observability/tenants/t1/otlp/agents/a1/traces?api-version=1");
    }

    [TestMethod]
    public void BuildRequestUri_TrailingSlash_IsNormalized()
    {
        var uri = _agent365ExporterCore.BuildRequestUri("https://custom.example.com/", "/observability/tenants/t1/otlp/agents/a1/traces");
        uri.Should().Be("https://custom.example.com/observability/tenants/t1/otlp/agents/a1/traces?api-version=1");
    }

    [TestMethod]
    public void BuildRequestUri_BareHostWithTrailingSlash_IsNormalized()
    {
        var uri = _agent365ExporterCore.BuildRequestUri("agent365.svc.cloud.microsoft/", "/observability/tenants/t1/otlp/agents/a1/traces");
        uri.Should().Be("https://agent365.svc.cloud.microsoft/observability/tenants/t1/otlp/agents/a1/traces?api-version=1");
    }

    [TestMethod]
    public void BuildRequestUri_HttpEndpoint_ThrowsArgumentException()
    {
        Action act = () => _agent365ExporterCore.BuildRequestUri("http://insecure.example.com", "/observability/tenants/t1/otlp/agents/a1/traces");
        act.Should().Throw<ArgumentException>().WithParameterName("endpoint");
    }

    #endregion

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

}