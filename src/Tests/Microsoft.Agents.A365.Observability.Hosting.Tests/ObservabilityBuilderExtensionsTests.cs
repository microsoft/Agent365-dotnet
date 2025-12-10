using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests
{
    [TestClass]
    public class ObservabilityBuilderExtensionsTests
    {
        private class MinimalStartup
        {
            public void Configure(IApplicationBuilder app)
            {
                // no-op
            }
        }

        private class MarkerService { }

        [TestMethod]
        public void AddA365Tracing_InvokesConfigureDelegate_RegistersCustomService()
        {
            var webHostBuilder = new WebHostBuilder();

            webHostBuilder.AddA365Tracing(configure: builder =>
            {
                builder.Services.AddSingleton<MarkerService>();
            });
            webHostBuilder.UseStartup<MinimalStartup>();

            using var host = webHostBuilder.Build();
            var service = host.Services.GetService<MarkerService>();
            service.Should().NotBeNull("custom services registered via the configure delegate should be available");
        }

        [TestMethod]
        public void AddA365Tracing_WithOpenTelemetryBuilderTrue_BuildsSuccessfully()
        {
            var webHostBuilder = new WebHostBuilder();
            webHostBuilder.AddA365Tracing(useOpenTelemetryBuilder: true);
            webHostBuilder.UseStartup<MinimalStartup>();

            using var host = webHostBuilder.Build();
            host.Should().NotBeNull();
        }

        [TestMethod]
        public void AddA365Tracing_WithOpenTelemetryBuilderTrue_RegistersOpenTelemetryServices()
        {
            var webHostBuilder = new WebHostBuilder();
            webHostBuilder.AddA365Tracing(useOpenTelemetryBuilder: true);
            webHostBuilder.UseStartup<MinimalStartup>();

            using var host = webHostBuilder.Build();
            var tracerProvider = host.Services.GetService<TracerProvider>();
            tracerProvider.Should().NotBeNull("OpenTelemetry tracing services should be registered when using the OpenTelemetry builder");
        }

        [TestMethod]
        public void AddA365Tracing_WithOpenTelemetryBuilderTrue_AndExporterEnabled_RegistersTracerProvider()
        {
            // Enable exporter via configuration
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

            var webHostBuilder = new WebHostBuilder();

            // Ensure environment variables are part of configuration
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            webHostBuilder.UseConfiguration(configuration);

            // Provide dependencies needed by exporter
            webHostBuilder.AddA365Tracing(useOpenTelemetryBuilder: true, agent365ExporterType: Agent365ExporterType.Agent365Exporter,
                configure: builder =>
                {
                    builder.Services.AddSingleton<HttpClient>(sp => new HttpClient());
                    builder.Services.AddSingleton<Agent365ExporterOptions>(sp => new Agent365ExporterOptions
                    {
                        UseS2SEndpoint = false,
                        TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
                    });
                });
            webHostBuilder.UseStartup<MinimalStartup>();

            using var host = webHostBuilder.Build();
            var tracerProvider = host.Services.GetService<TracerProvider>();
            tracerProvider.Should().NotBeNull("TracerProvider should be registered when exporter is enabled via OpenTelemetry builder");
        }

        [TestMethod]
        public void AddA365Tracing_WithOpenTelemetryBuilderFalse_BuildsSuccessfully()
        {
            var webHostBuilder = new WebHostBuilder();
            webHostBuilder.AddA365Tracing(useOpenTelemetryBuilder: false);
            webHostBuilder.UseStartup<MinimalStartup>();

            using var host = webHostBuilder.Build();
            host.Should().NotBeNull();
        }
    }
}
