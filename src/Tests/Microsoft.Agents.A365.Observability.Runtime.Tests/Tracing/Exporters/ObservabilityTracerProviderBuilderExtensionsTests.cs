// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Reflection;
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
        var tracerProviderBuilder = services.AddOpenTelemetry().WithTracing(builder =>
        {
            builder.AddAgent365Exporter(Agent365ExporterType.Agent365Exporter);
        });

        var serviceProvider = services.BuildServiceProvider();
        var tracerProvider = serviceProvider.GetService<TracerProvider>();

        // Assert
        serviceProvider.Should().NotBeNull();
        tracerProvider.Should().NotBeNull();
        
        // Verify that the exporter type is registered in the static guard
        IsExporterTypeRegistered(Agent365ExporterType.Agent365Exporter).Should().BeTrue("Agent365Exporter type should be registered in static guard");
    }

    /// <summary>
    /// Helper method to clear the static guard registry using reflection.
    /// This is needed for test isolation since static state persists across tests.
    /// </summary>
    private static void ClearStaticGuardRegistry()
    {
        var type = typeof(ObservabilityTracerProviderBuilderExtensions);
        var field = type.GetField("_registeredExporters", BindingFlags.NonPublic | BindingFlags.Static);
        
        if (field?.GetValue(null) is ConcurrentDictionary<Agent365ExporterType, bool> registry)
        {
            registry.Clear();
        }
    }

    /// <summary>
    /// Helper method to check if an exporter type is registered in the static guard using reflection.
    /// </summary>
    private static bool IsExporterTypeRegistered(Agent365ExporterType exporterType)
    {
        var type = typeof(ObservabilityTracerProviderBuilderExtensions);
        var field = type.GetField("_registeredExporters", BindingFlags.NonPublic | BindingFlags.Static);
        
        if (field?.GetValue(null) is ConcurrentDictionary<Agent365ExporterType, bool> registry)
        {
            return registry.ContainsKey(exporterType);
        }
        
        return false;
    }
}