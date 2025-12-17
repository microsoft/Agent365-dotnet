// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Tests;

/// <summary>
/// Tests to verify direct usage of the public Builder constructor and Build().
/// </summary>
[TestClass]
public sealed class BuilderTests
{
    [TestMethod]
    public void Builder_WithUseOpenTelemetryBuilder_True_ShouldBuild()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var builder = new Builder(services: services, configuration: configuration, useOpenTelemetryBuilder: true);

        var result = builder.Build();

        result.Should().NotBeNull();
        result.Should().BeSameAs(services);
    }

    [TestMethod]
    public void Builder_WithUseOpenTelemetryBuilder_False_ShouldBuild()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var builder = new Builder(services: services, configuration: configuration, useOpenTelemetryBuilder: false);

        var result = builder.Build();

        result.Should().NotBeNull();
        result.Should().BeSameAs(services);
    }

    [TestMethod]
    public void Builder_WithNullConfiguration_ShouldBuild()
    {
        var services = new ServiceCollection();

        var builder = new Builder(services: services, configuration: null, useOpenTelemetryBuilder: true);

        var result = builder.Build();

        result.Should().NotBeNull();
        result.Should().BeSameAs(services);
    }

    [TestMethod]
    public void Builder_WithExporterType_Sync_RegistersTracerProvider()
    {
        try
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            // Provide required dependencies for exporter
            services.AddSingleton<Agent365ExporterOptions>(_ => new Agent365ExporterOptions
            {
                TokenResolver = (_, _) => Task.FromResult<string?>("unit-test-token"),
                UseS2SEndpoint = false
            });

            var builder = new Builder(
                services: services,
                configuration: configuration,
                useOpenTelemetryBuilder: true,
                agent365ExporterType: Agent365ExporterType.Agent365Exporter);

            builder.Build();

            var provider = services.BuildServiceProvider();
            var tracerProvider = provider.GetService<TracerProvider>();
            tracerProvider.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", null);
        }
    }

    [TestMethod]
    public void Builder_WithExporterType_Async_RegistersTracerProvider()
    {
        try
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            // Provide required dependencies for exporter
            services.AddSingleton<Agent365ExporterOptions>(_ => new Agent365ExporterOptions
            {
                TokenResolver = (_, _) => Task.FromResult<string?>("unit-test-token"),
                UseS2SEndpoint = false
            });

            var builder = new Builder(
                services: services,
                configuration: configuration,
                useOpenTelemetryBuilder: true,
                agent365ExporterType: Agent365ExporterType.Agent365ExporterAsync);

            builder.Build();

            var provider = services.BuildServiceProvider();
            var tracerProvider = provider.GetService<TracerProvider>();
            tracerProvider.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", null);
        }
    }

    [TestMethod]
    public void Builder_UseOpenTelemetryBuilder_False_WithExporterType_Sync_CreatesTracerProvider()
    {
        try
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            services.AddSingleton<Agent365ExporterOptions>(_ => new Agent365ExporterOptions
            {
                TokenResolver = (_, _) => Task.FromResult<string?>("unit-test-token"),
                UseS2SEndpoint = false
            });

            var builder = new Builder(
                services: services,
                configuration: configuration,
                useOpenTelemetryBuilder: false,
                agent365ExporterType: Agent365ExporterType.Agent365Exporter);

            builder.Build();

            var provider = services.BuildServiceProvider();
            var tracerProvider = provider.GetService<TracerProvider>();
            tracerProvider.Should().BeNull("useOpenTelemetryBuilder=false should not register TracerProvider in DI");

            using var source = new System.Diagnostics.ActivitySource(OpenTelemetryConstants.SourceName);
            source.HasListeners().Should().BeTrue("Agent365Exporter should register ActivitySource listeners even when not using OpenTelemetryBuilder");
        }
        finally
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", null);
        }
    }

    [TestMethod]
    public void Builder_UseOpenTelemetryBuilder_False_WithExporterType_Async_ExportsAndNoTracerProviderInDI()
    {
        try
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            services.AddSingleton<Agent365ExporterOptions>(_ => new Agent365ExporterOptions
            {
                TokenResolver = (_, _) => Task.FromResult<string?>("unit-test-token"),
                UseS2SEndpoint = false
            });

            var builder = new Builder(
                services: services,
                configuration: configuration,
                useOpenTelemetryBuilder: false,
                agent365ExporterType: Agent365ExporterType.Agent365ExporterAsync);

            builder.Build();

            var provider = services.BuildServiceProvider();
            var tracerProvider = provider.GetService<TracerProvider>();
            tracerProvider.Should().BeNull("useOpenTelemetryBuilder=false should not register TracerProvider in DI");

            using var source = new System.Diagnostics.ActivitySource(OpenTelemetryConstants.SourceName);
            source.HasListeners().Should().BeTrue("Agent365ExporterAsync should register ActivitySource listeners even when not using OpenTelemetryBuilder");
        }
        finally
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", null);
        }
    }
}
