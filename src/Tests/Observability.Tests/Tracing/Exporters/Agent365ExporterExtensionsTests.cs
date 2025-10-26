using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Exporters;

[TestClass]
public sealed class Agent365ExporterExtensionsTests
{
    [TestMethod]
    public void AddAgent365Exporter_WithCustomBatchingParameters_ShouldConfigureProcessor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Configure custom batching parameters
        services.AddSingleton(new Agent365ExporterOptions
        {
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token"),
            MaxQueueSize = 4096,
            ScheduledDelayMilliseconds = 10000,
            ExporterTimeoutMilliseconds = 60000,
            MaxExportBatchSize = 1024
        });

        var tracerProviderBuilder = services.AddOpenTelemetry().WithTracing(builder =>
        {
            builder.AddAgent365Exporter();
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Service provider should be built successfully with custom parameters
        serviceProvider.Should().NotBeNull();
        
        // Verify the options were registered correctly
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
        options.MaxQueueSize.Should().Be(4096);
        options.ScheduledDelayMilliseconds.Should().Be(10000);
        options.ExporterTimeoutMilliseconds.Should().Be(60000);
        options.MaxExportBatchSize.Should().Be(1024);
    }

    [TestMethod]
    public void AddAgent365Exporter_WithDefaultBatchingParameters_ShouldUseDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Configure with default batching parameters
        services.AddSingleton(new Agent365ExporterOptions
        {
            ClusterCategory = "test",
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
        });

        var tracerProviderBuilder = services.AddOpenTelemetry().WithTracing(builder =>
        {
            builder.AddAgent365Exporter();
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Service provider should be built successfully with default parameters
        serviceProvider.Should().NotBeNull();
        
        // Verify the default options
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
        options.MaxQueueSize.Should().Be(2048);
        options.ScheduledDelayMilliseconds.Should().Be(5000);
        options.ExporterTimeoutMilliseconds.Should().Be(30000);
        options.MaxExportBatchSize.Should().Be(512);
    }

    [TestMethod]
    public void AddAgent365Exporter_ThrowsWhenTracerProviderBuilderIsNull()
    {
        // Arrange
        TracerProviderBuilder? builder = null;

        // Act
        Action act = () => builder!.AddAgent365Exporter();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }
}
