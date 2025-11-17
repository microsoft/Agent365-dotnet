// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using System;

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

        // TODO: Move to a common utilities library and remove it from Runtime.
        /// <summary>
        /// Returns true if the current environment is a development environment.
        /// </summary>
        /// <returns></returns>
        public static bool IsDevelopmentEnvironment()
        {
            var environment = GetCurrentEnvironment();
            return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the current environment name.
        /// </summary>
        /// <returns>The current environment name.</returns>
        private static string GetCurrentEnvironment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                   Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                   "Development";
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
                .AddSingleton(typeof(IA365EtwLogger<>), typeof(A365EtwLogger<>))
                .AddLogging(logging =>
                {
                    logging.AddOpenTelemetry(otelLogging =>
                    {
                        otelLogging.ParseStateValues = true;
                        otelLogging.AddProcessor(new EtwLogProcessor());

                        if (IsDevelopmentEnvironment())
                        {
                            otelLogging.AddConsoleExporter();
                        }
                    });
                })
                .Configure<LoggerFilterOptions>(options =>
                {
                    options.AddFilter<OpenTelemetryLoggerProvider>(
                        (category, level) => category != null && category.StartsWith(Constants.EtwCategoryPrefix, StringComparison.Ordinal));
                });

            _isBuilt = true;
        }
    }
}
