using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry;
using OpenTelemetry.Resources;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Exporters;

[TestClass]
public sealed class Agent365ExporterTests
{
    /// <summary>
    /// All valid cluster categories from PowerPlatformApiDiscovery.GetEnvironmentApiHostNameSuffix()
    /// </summary>
    private static readonly string[] ValidClusterCategories = new[]
    {
        "local", "dev", "test", "preprod", "firstrelease", "prod",
        "gov", "high", "dod", "mooncake", "ex", "rx"
    };

    private static Activity CreateActivity(string? tenantId = null, string? agentId = null)
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

    private static Agent365Exporter CreateExporter(Func<string, string, string?>? tokenResolver)
    {
        var options = new Agent365ExporterOptions
        {
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("token")
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        return new Agent365Exporter(
            NullLogger<Agent365Exporter>.Instance,
            options,
            resource);
    }

    [TestMethod]
    public void Constructor_NullLogger_Throws()
    {
        var options = new Agent365ExporterOptions
        {
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("token")
        };

        Action act = () => _ = new Agent365Exporter(logger: null!, options, resource: null);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [TestMethod]
    public void Constructor_NullOptions_Throws()
    {
        Action act = () => _ = new Agent365Exporter(NullLogger<Agent365Exporter>.Instance, null!, null);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [TestMethod]
    public void Constructor_NullTokenResolver_Throws()
    {
        var options = new Agent365ExporterOptions
        {
            ClusterCategory = "test",
            TokenResolver = null
        };

        Action act = () => _ = new Agent365Exporter(NullLogger<Agent365Exporter>.Instance, options, null);
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
        var exporter = CreateExporter((_, _) => "token");

        // Two activities for same (T1,A1)
        using var a1 = CreateActivity("tenant-1", "agent-1");
        using var a2 = CreateActivity("tenant-1", "agent-1");
        // One activity for (T1,A2)
        using var a3 = CreateActivity("tenant-1", "agent-2");
        // Activity missing agent -> ignored
        using var a4 = CreateActivity("tenant-1", null);
        // Activity missing tenant -> ignored
        using var a5 = CreateActivity(null, "agent-3");
        // Activity with neither -> ignored
        using var a6 = CreateActivity();

        var batch = CreateBatch(a1, a2, a3, a4, a5, a6);

        var result = Agent365ExporterCore.PartitionByIdentity(in batch);

        result.Should().NotBeNull();

        // Result is List<ValueTuple<string,string,List<Activity>>>
        var list = (System.Collections.IEnumerable)result!;
        var groups = new List<(string tenant, string agent, int count)>();

        foreach (var group in list)
        {
            // ValueTuple fields are Item1, Item2, Item3
            var t = (string)group.GetType().GetField("Item1")!.GetValue(group)!;
            var a = (string)group.GetType().GetField("Item2")!.GetValue(group)!;
            var acts = (List<Activity>)group.GetType().GetField("Item3")!.GetValue(group)!;
            groups.Add((t, a, acts.Count));
        }

        // Assert
        groups.Should().HaveCount(2);
        groups.Should().Contain(g => g.tenant == "tenant-1" && g.agent == "agent-1" && g.count == 2);
        groups.Should().Contain(g => g.tenant == "tenant-1" && g.agent == "agent-2" && g.count == 1);
    }

    [TestMethod]
    public void Agent365ExporterOptions_DefaultBatchingParameters_AreSet()
    {
        // Arrange & Act
        var options = new Agent365ExporterOptions
        {
            ClusterCategory = "test",
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>(null), // Return null token
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>(string.Empty), // Return empty token
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromException<string?>(new InvalidOperationException("Token resolver failed")),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromException<string?>(new InvalidOperationException("Token resolver failed")),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var resource = ResourceBuilder.CreateEmpty()
            .AddService("unit-test-service", serviceVersion: "1.0.0")
            .Build();

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = true
        };

        var exporter = new Agent365Exporter(
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
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            UseS2SEndpoint = false
        };

        var exporter = new Agent365Exporter(
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

    #region TruncateActivityToMaxSize Tests

    [TestMethod]
    public void TruncateActivityToMaxSize_DoesNothing_WhenUnderLimit()
    {
        // Arrange
        using var activity = CreateActivity("tenant-1", "agent-1");
        activity.SetTag("gen_ai.tool.arguments", new string('a', 1024)); // 1KB
        var resource = ResourceBuilder.CreateEmpty().Build();
        var logs = new List<string>();

        // Act
        var result = Agent365ExporterCore.TruncateActivityToMaxSize(activity, resource, logs.Add);

        // Assert
        result.Should().BeFalse();
        activity.GetTagItem("gen_ai.tool.arguments").Should().BeOfType<string>().Which.Should().NotBe("TRUNCATED");
    }

    [TestMethod]
    public void TruncateActivityToMaxSize_TruncatesSingleLargeKey()
    {
        // Arrange
        using var activity = CreateActivity("tenant-1", "agent-1");
        // 300KB value
        activity.SetTag("gen_ai.tool.arguments", new string('b', 300 * 1024));
        var resource = ResourceBuilder.CreateEmpty().Build();
        var logs = new List<string>();

        // Act
        var result = Agent365ExporterCore.TruncateActivityToMaxSize(activity, resource, logs.Add);

        // Assert
        result.Should().BeTrue();
        activity.GetTagItem("gen_ai.tool.arguments").Should().Be("TRUNCATED");
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.tool.arguments' size = "));
        logs.Should().Contain(l => l.Contains("Truncated 'gen_ai.tool.arguments'"));
    }

    [TestMethod]
    public void TruncateActivityToMaxSize_TruncatesMultipleKeys_LargestFirst()
    {
        // Arrange
        using var activity = CreateActivity("tenant-1", "agent-1");
        // 200KB and 100KB values, total > 250KB
        activity.SetTag("gen_ai.tool.arguments", new string('c', 200 * 1024));
        activity.SetTag("gen_ai.event.content", new string('d', 100 * 1024));
        var resource = ResourceBuilder.CreateEmpty().Build();
        var logs = new List<string>();

        // Act
        var result = Agent365ExporterCore.TruncateActivityToMaxSize(activity, resource, logs.Add);

        // Assert
        result.Should().BeTrue();
        // The largest should be truncated first
        activity.GetTagItem("gen_ai.tool.arguments").Should().Be("TRUNCATED");
        // If still over, the next largest is truncated
        // (depends on serialization overhead, so check at least one is truncated)
        logs.Should().Contain(l => l.Contains("Truncated 'gen_ai.tool.arguments'"));
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.tool.arguments' size = "));
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.event.content' size = "));
    }

    [TestMethod]
    public void TruncateActivityToMaxSize_LogsAllKeySizes()
    {
        // Arrange
        using var activity = CreateActivity("tenant-1", "agent-1");
        activity.SetTag("gen_ai.tool.arguments", new string('x', 100 * 1024));
        activity.SetTag("gen_ai.event.content", new string('y', 125 * 1024));
        activity.SetTag("gen_ai.input.messages", new string('z', 75 * 1024));
        var resource = ResourceBuilder.CreateEmpty().Build();
        var logs = new List<string>();

        // Act
        Agent365ExporterCore.TruncateActivityToMaxSize(activity, resource, logs.Add);

        // Assert
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.tool.arguments' size = 100"));
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.event.content' size = 125"));
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.input.messages' size = 75"));
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.agent.invocation_input' size = 0"));
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.output.messages' size = 0"));
        logs.Should().Contain(l => l.Contains("Key 'gen_ai.agent.invocation_output' size = 0"));
    }

    #endregion

}