// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime
{
    using Microsoft.Agents.A365.Observability.Runtime.Common;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
    using Microsoft.Extensions.DependencyInjection;
    using OpenTelemetry.Trace;
    using System;

    /// <summary>
    /// Builder for configuring SDK with OpenTelemetry tracing.
    /// </summary>
    public sealed class Builder
    {
        private readonly IServiceCollection _services;
        private bool _isBuilt = false;


        /// <summary>
        /// Initializes a new instance of the <see cref="Builder"/> class.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        internal Builder(IServiceCollection services)
        {
            _services = services;
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
            return Environment.GetEnvironmentVariable("EnableAgent365Exporter") == "true";
        }

        private void EnsureBuilt()
        {
            if (_isBuilt)
                return;

            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            // Configure OpenTelemetry with all processors in a single call
            _services
                .AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing
                        .SetSampler(new ParentBasedSampler(
                            rootSampler: new AlwaysOnSampler(),
                            localParentNotSampled: new AlwaysOnSampler(),
                            remoteParentNotSampled: new AlwaysOnSampler()))
                        .AddSource(OpenTelemetryConstants.SourceName)
                        .AddProcessor(new ActivityProcessor());

                    if (IsAgent365ExporterEnabled())
                    {
                        tracing.AddAgent365Exporter();
                    }
                    else if (EnvironmentUtils.IsDevelopmentEnvironment())
                    {
                        tracing.AddConsoleExporter();
                    }
                });

            _isBuilt = true;
        }
    }
}