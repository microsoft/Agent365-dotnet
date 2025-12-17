// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests;

[TestClass]
public class ObservabilityBuilderExtensionsTests
{
    private class MarkerService { }

    [TestMethod]
    public void AddA365Tracing_IHostBuilder_InvokesConfigureDelegate_RegistersCustomService()
    {
        var hostBuilder = new HostBuilder();

        hostBuilder.AddA365Tracing(configure: b =>
        {
            b.Services.AddSingleton<MarkerService>();
        });

        using var host = hostBuilder.Build();
        var service = host.Services.GetService<MarkerService>();
        service.Should().NotBeNull("custom services registered via the configure delegate should be available");
    }

    [TestMethod]
    public void AddA365Tracing_IHostBuilder_WithOpenTelemetryBuilderTrue_BuildsSuccessfully()
    {
        var hostBuilder = new HostBuilder();
        hostBuilder.AddA365Tracing(useOpenTelemetryBuilder: true);

        using var host = hostBuilder.Build();
        host.Should().NotBeNull();
    }

    [TestMethod]
    public void AddA365Tracing_IHostBuilder_WithOpenTelemetryBuilderTrue_RegistersOpenTelemetryServices()
    {
        var hostBuilder = new HostBuilder();
        hostBuilder.AddA365Tracing(useOpenTelemetryBuilder: true);

        using var host = hostBuilder.Build();
        var tracerProvider = host.Services.GetService<TracerProvider>();
        tracerProvider.Should().NotBeNull("OpenTelemetry tracing services should be registered when using the OpenTelemetry builder");
    }

    [TestMethod]
    public void AddA365Tracing_IHostBuilder_WithOpenTelemetryBuilderTrue_AndExporterEnabled_RegistersTracerProvider()
    {
        Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

        var hostBuilder = new HostBuilder();

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        hostBuilder.ConfigureHostConfiguration(cfg =>
        {
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value is not null)
                {
                    cfg.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>(kvp.Key, kvp.Value) });
                }
            }
        });

        hostBuilder.AddA365Tracing(useOpenTelemetryBuilder: true, agent365ExporterType: Agent365ExporterType.Agent365Exporter,
            configure: builder =>
            {
                builder.Services.AddSingleton<HttpClient>(_ => new HttpClient());
                builder.Services.AddSingleton<Agent365ExporterOptions>(_ => new Agent365ExporterOptions
                {
                    UseS2SEndpoint = false,
                    TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
                });
            });

        using var host = hostBuilder.Build();
        var tracerProvider = host.Services.GetService<TracerProvider>();
        tracerProvider.Should().NotBeNull("TracerProvider should be registered when exporter is enabled via OpenTelemetry builder");
    }

    [TestMethod]
    public void AddA365Tracing_IHostBuilder_WithOpenTelemetryBuilderFalse_BuildsSuccessfully()
    {
        var hostBuilder = new HostBuilder();
        hostBuilder.AddA365Tracing(useOpenTelemetryBuilder: false);

        using var host = hostBuilder.Build();
        host.Should().NotBeNull();
    }
}
