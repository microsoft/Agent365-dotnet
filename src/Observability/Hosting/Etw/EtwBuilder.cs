// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Microsoft.Agents.A365.Observability.Hosting.Etw
{
    /// <summary>
    /// Builds the ETW + OpenTelemetry configuration.
    /// </summary>
    public sealed class EtwBuilder
    {
        private readonly IServiceCollection _services;
        private bool _isBuilt = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="EtwBuilder"/> class.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        internal EtwBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Builds the ETW configuration and returns the service collection.
        /// </summary>
        /// <returns>The configured service collection.</returns>
        public IServiceCollection Build()
        {
            EnsureBuilt();
            return _services;
        }

        private void EnsureBuilt()
        {
            if (_isBuilt)
                return;

            _services
                .AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing
                    .AddSource(OpenTelemetryConstants.SourceName)
                    .AddProcessor(new ActivityProcessor())
                    .AddProcessor(new EtwScopeEventProcessor());

                    if (EnvironmentUtils.IsDevelopmentEnvironment())
                    {
                        tracing.AddConsoleExporter();
                    }
                });

            _isBuilt = true;
        }
    }
}
