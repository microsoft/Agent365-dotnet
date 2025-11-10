// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;

namespace Microsoft.Agents.A365.Observability.Hosting.Etw
{
    /// <summary>
    /// Builds the ETW + OpenTelemetry logging configuration.
    /// </summary>
    public sealed class EtwLoggingBuilder
    {
        private readonly IServiceCollection _services;
        private bool _isBuilt = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="EtwLoggingBuilder"/> class.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        internal EtwLoggingBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Builds the ETW logging configuration and returns the service collection.
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
                .AddSingleton(typeof(IEtwLogger<>), typeof(EtwLogger<>))
                .AddOpenTelemetry()
                .WithLogging(logging =>
                {
                    logging
                        .AddProcessor(new EtwLogProcessor());

                    if (EnvironmentUtils.IsDevelopmentEnvironment())
                    {
                        logging.AddConsoleExporter();
                    }
                }, (options =>
                {
                    options.ParseStateValues = true;
                }));

            _isBuilt = true;
        }
    }
}
