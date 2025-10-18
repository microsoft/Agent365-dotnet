using FluentAssertions;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability;

namespace Microsoft.Agents.A365.Observability.Tests;

/// <summary>
/// Test to verify that the new builder pattern works as expected in the issue example.
/// </summary>
[TestClass]
public sealed class BuilderPatternTests
{
    [TestMethod]
    public void AddTracing_WithLambdaConfiguration_ShouldWork()
    {
        var services = new ServiceCollection();

        // Use the new lambda configuration approach
        var result = services.AddTracing();

        // Should return the configured service collection directly (no Build() needed)
        result.Should().NotBeNull();
        result.Should().BeSameAs(services);
        result.Should().BeAssignableTo<IServiceCollection>();
    }

    [TestMethod]
    public void AddTracing_WithLambdaConfiguration_WithConnectionString()
    {
        var services = new ServiceCollection();
        const string connectionString = "InstrumentationKey=test-key-lambda";

        // Use lambda configuration with multiple settings
        var result = services.AddTracing(builder => builder
            .WithConnectionString(connectionString));

        result.Should().NotBeNull();
        result.Should().BeSameAs(services);
    }

    [TestMethod]
    public void AddTracing_WithNullLambda_ShouldWork()
    {
        var services = new ServiceCollection();

        var result = services.AddTracing(null);

        result.Should().NotBeNull();
        result.Should().BeSameAs(services);
    }

    [TestMethod]
    public void AddTracing_WithEmptyLambda_ShouldWork()
    {
        var services = new ServiceCollection();

        // Pass empty lambda - should work like no configuration
        var result = services.AddTracing(_ => { });

        result.Should().NotBeNull();
        result.Should().BeSameAs(services);
    }

    [TestMethod]
    public void AddTracing_LambdaConfiguration_ServiceProviderCanBeBuilt()
    {
        var services = new ServiceCollection();
        const string connectionString = "InstrumentationKey=test-key-provider";

        // Configure using lambda
        services.AddTracing(builder => builder
            .WithConnectionString(connectionString));

        // Should be able to build service provider successfully
        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.Should().NotBeNull();
    }
}