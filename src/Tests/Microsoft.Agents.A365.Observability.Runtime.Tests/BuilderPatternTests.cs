using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        HostApplicationBuilder builder = new HostApplicationBuilder();

        // Use the new lambda configuration approach
        var result = builder.AddA365Tracing();

        // Should return the configured service collection directly (no Build() needed)
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
        result.Services.Should().BeAssignableTo<IServiceCollection>();
    }

    [TestMethod]
    public void AddTracing_WithNullLambda_ShouldWork()
    {
        HostApplicationBuilder builder = new HostApplicationBuilder();

        var result = builder.AddA365Tracing(null);

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [TestMethod]
    public void AddTracing_WithEmptyLambda_ShouldWork()
    {
        HostApplicationBuilder builder = new HostApplicationBuilder();

        // Pass empty lambda - should work like no configuration
        var result = builder.AddA365Tracing(_ => { });

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }
}