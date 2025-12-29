// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Tracing.Exporters;

/// <summary>
/// Unit tests for ObservabilityTracerProviderBuilderExtensions static guard functionality.
/// </summary>
[TestClass]
public sealed class ObservabilityTracerProviderBuilderExtensionsTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        ClearStaticGuardRegistry();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        ClearStaticGuardRegistry();
    }

    [TestMethod]
    public void AddAgent365Exporter_UpdatesStaticGuardRegistry()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new Agent365ExporterOptions
        {
            TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
        });

        // Act
        services.AddOpenTelemetry().WithTracing(builder =>
        {
            builder.AddAgent365Exporter(Agent365ExporterType.Agent365Exporter);
        });

        var serviceProvider = services.BuildServiceProvider();
        var tracerProvider = serviceProvider.GetService<TracerProvider>();

        // Assert
        serviceProvider.Should().NotBeNull();
        tracerProvider.Should().NotBeNull();
        
        // Verify that the exporter type is registered in the static guard
        ObservabilityTracerProviderBuilderExtensions.RegisteredExporters
            .Should().ContainKey(Agent365ExporterType.Agent365Exporter)
            .WhoseValue.Should().BeTrue("Agent365Exporter type should be registered in static guard");
    }

    /// <summary>
    /// Helper method to clear the static guard registry.
    /// This is needed for test isolation since static state persists across tests.
    /// </summary>
    private static void ClearStaticGuardRegistry()
    {
        ObservabilityTracerProviderBuilderExtensions.RegisteredExporters.Clear();
    }
}