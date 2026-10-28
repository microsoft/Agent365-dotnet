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
        var tokenCache = serviceProvider.GetService<IExporterTokenCache<ObservabilityTokenResolver>>();
        tokenCache.Should().NotBeNull();
        tokenCache.Should().BeOfType<AgenticTokenCache>();

        serviceProvider.GetService<IExporterTokenCache<AgenticTokenStruct>>()
            .Should().BeNull("the delegated AgenticTokenStruct cache contract was removed");

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
    public void AddAgenticTracingExporter_UsesIgnoredLegacyEndpointFlag()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgenticTracingExporter();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
#pragma warning disable CS0618
        options.UseS2SEndpoint.Should().BeFalse("the compatibility property default is preserved but ignored");
#pragma warning restore CS0618
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
    public void AddServiceTracingExporter_UsesIgnoredLegacyEndpointFlag()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServiceTracingExporter();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
#pragma warning disable CS0618
        options.UseS2SEndpoint.Should().BeFalse("OBS export always routes to S2S regardless of this compatibility property");
#pragma warning restore CS0618
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
