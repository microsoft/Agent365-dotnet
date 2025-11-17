// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime
{
    using Microsoft.Agents.A365.Observability.Runtime.Common;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using OpenTelemetry;
    using OpenTelemetry.Trace;
    using System;

    /// <summary>
    /// Builder for configuring SDK with OpenTelemetry tracing.
    /// </summary>
    public sealed class Builder
    {
        private readonly IServiceCollection _services;
        private readonly bool _useOpenTelemetryBuilder;
        private readonly Agent365ExporterType _agent365ExporterType;
        private bool _isBuilt = false;

        /// <summary>
        /// Gets the configuration instance used for feature toggles and runtime settings.
        /// </summary>
        public IConfiguration? Configuration { get; }


        /// <summary>
        /// Initializes a new instance of the <see cref="Builder"/> class.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="useOpenTelemetryBuilder">Whether to use the OpenTelemetryBuilder to add OpenTelemetry services to the supplied service collection.</param>
        /// <param name="agent365ExporterType">The type of Agent365 exporter to use.</param>
        /// <param name="configuration">The configuration instance.</param>
        internal Builder(IServiceCollection services, bool useOpenTelemetryBuilder, Agent365ExporterType agent365ExporterType, IConfiguration? configuration)
        {
            this._services = services;
            this._useOpenTelemetryBuilder = useOpenTelemetryBuilder;
            this._agent365ExporterType = agent365ExporterType;
            Configuration = configuration;
        }

        /// <summary>
        /// Gets the services collection for continued configuration.
        /// </summary>
        public IServiceCollection Services => _services;

        /// <summary>
        /// Builds the AI configuration and returns the service collection.
        /// </summary>
        /// <returns>The configured service collection.</returns>
        public IServiceCollection Build()
        {
            EnsureBuilt();
            return _services;
        }

        private bool IsAgent365ExporterEnabled()
        {
            if (Configuration != null && Configuration["EnableAgent365Exporter"] != null)
            {
                string enabledEnv = Configuration["EnableAgent365Exporter"]!;
                return enabledEnv.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void EnsureBuilt()
        {
            if (_isBuilt)
                return;

            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            // Configure OpenTelemetry with all processors in a single call
            // NOTE: _useOpenTelemetryBuilder = true does two things.
            // 1. Uses the provided service collection to create the tracer provider (using IDeferredTracerProviderBuilder in ObservabilityTracerProviderBuilderExtensions.AddAgent365Exporter())
            // 2. Adds open telemetry and tracing services to the service collecion.
            // _useOpenTelemetryBuilder = false just uses the provided service collection to create the tracer provider (using ObservabilityTracerProviderBuilderExtensions.AddAgent365Exporter(IServiceCollection))
            if (this._useOpenTelemetryBuilder)
            {
                _services
                    .AddOpenTelemetry()
                    .WithTracing(tracerProviderBuilder =>
                    {
                        this.Configure(tracerProviderBuilder: tracerProviderBuilder);
                    });
            }
            else
            {
                var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();
                this.Configure(tracerProviderBuilder: tracerProviderBuilder);
                tracerProviderBuilder.Build();
            }

            _isBuilt = true;
        }

        private void Configure(TracerProviderBuilder tracerProviderBuilder)
        {
            tracerProviderBuilder
                .SetSampler(new ParentBasedSampler(
                    rootSampler: new AlwaysOnSampler(),
                    localParentNotSampled: new AlwaysOnSampler(),
                    remoteParentNotSampled: new AlwaysOnSampler()))
                .AddSource(OpenTelemetryConstants.SourceName)
                .AddProcessor(new ActivityProcessor());

            if (IsAgent365ExporterEnabled())
            {
                if (this._useOpenTelemetryBuilder)
                {
                    tracerProviderBuilder.AddAgent365Exporter(exporterType: this._agent365ExporterType);
                }
                else
                {
                    tracerProviderBuilder.AddAgent365Exporter(serviceCollection: this._services, exporterType: this._agent365ExporterType);
                }
            }
            else if (EnvironmentUtils.IsDevelopmentEnvironment())
            {
                tracerProviderBuilder.AddConsoleExporter();
            }
        }
    }
}