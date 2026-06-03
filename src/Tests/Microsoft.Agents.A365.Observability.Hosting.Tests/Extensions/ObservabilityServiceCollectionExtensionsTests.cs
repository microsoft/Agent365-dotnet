using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Hosting.Caching;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Extensions;

/// <summary>
/// Tests for Agent 365 extension methods.
/// </summary>
[TestClass]
public sealed class ObservabilityServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddAgenticTracingExporter_RegistersRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgenticTracingExporter();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var tokenCache = serviceProvider.GetService<IExporterTokenCache<AgenticTokenStruct>>();
        tokenCache.Should().NotBeNull();
        tokenCache.Should().BeOfType<AgenticTokenCache>();

        var options = serviceProvider.GetService<Agent365ExporterOptions>();
        options.Should().NotBeNull();
        options!.TokenResolver.Should().NotBeNull();
    }

    [TestMethod]
    public void AddAgenticTracingExporter_DefaultClusterCategory_IsProduction()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgenticTracingExporter();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
        options.ClusterCategory.Should().Be("production");
    }

    [TestMethod]
    public void AddAgenticTracingExporter_UseS2SEndpoint_IsFalse()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgenticTracingExporter();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
        options.UseS2SEndpoint.Should().BeFalse("agentic exporter uses standard endpoint");
    }

    [TestMethod]
    public void AddServiceTracingExporter_RegistersRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServiceTracingExporter();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var tokenCache = serviceProvider.GetService<IExporterTokenCache<string>>();
        tokenCache.Should().NotBeNull();
        tokenCache.Should().BeOfType<ServiceTokenCache>();

        var options = serviceProvider.GetService<Agent365ExporterOptions>();
        options.Should().NotBeNull();
        options!.TokenResolver.Should().NotBeNull();
    }

    [TestMethod]
    public void AddServiceTracingExporter_DefaultClusterCategory_IsProduction()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServiceTracingExporter();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
        options.ClusterCategory.Should().Be("production");
    }

    [TestMethod]
    public void AddServiceTracingExporter_UseS2SEndpoint_IsTrue()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServiceTracingExporter( );
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
        options.UseS2SEndpoint.Should().BeTrue("service tracing exporter uses S2S endpoint");
    }

    [TestMethod]
    public void AddServiceTracingExporter_TokenResolver_CanBeCalled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddServiceTracingExporter();
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();

        // Act
        var token = options.TokenResolver!("test-agent", "test-tenant");

        // Assert
        // Token resolver should not throw (actual token retrieval logic is in the cache)
        // This just verifies the resolver is wired up
        options.TokenResolver.Should().NotBeNull();
    }

    [TestMethod]
    public void AddAgenticTracingExporter_TokenResolver_CanBeCalled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAgenticTracingExporter();
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();

        // Act
        var token = options.TokenResolver!("test-agent", "test-tenant");

        // Assert
        // Token resolver should not throw (actual token retrieval logic is in the cache)
        // This just verifies the resolver is wired up
        options.TokenResolver.Should().NotBeNull();
    }
}
