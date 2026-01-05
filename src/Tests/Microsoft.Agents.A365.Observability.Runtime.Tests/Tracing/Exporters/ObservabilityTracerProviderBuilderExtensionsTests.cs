// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Tracing.Exporters;

/// <summary>
/// Unit tests for ObservabilityTracerProviderBuilderExtensions functionality.
/// </summary>
[TestClass]
public sealed class ObservabilityTracerProviderBuilderExtensionsTests
{
    [TestMethod]
    public void AddAgent365Exporter_UpdatesExporterRegistrationService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAgent365ExporterRegistrationService, Agent365ExporterRegistrationService>();
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
        
        // Verify that the exporter type is registered in the exporter registration service
        var exporterRegistrationService = serviceProvider.GetService<IAgent365ExporterRegistrationService>();
        exporterRegistrationService.Should().NotBeNull();
        exporterRegistrationService!.IsExporterRegistered(Agent365ExporterType.Agent365Exporter)
            .Should().BeTrue("Agent365Exporter type should be registered in the service");
    }
}