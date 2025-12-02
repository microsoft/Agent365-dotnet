// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;
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
        private static readonly Lazy<ILoggerFactory> FallbackConsoleLoggerFactory = new Lazy<ILoggerFactory>(() => LoggerFactory.Create(b => b.AddConsole()));
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
                .AddSingleton(typeof(IA365EtwLogger<>), typeof(A365EtwLogger<>))
                .AddLogging(logging =>
                {
                    logging.AddOpenTelemetry(otelLogging =>
                    {
                        otelLogging.ParseStateValues = true;

                        using var sp = _services.BuildServiceProvider();
                        var loggerFactory = sp.GetService<ILoggerFactory>() ?? EtwLoggingBuilder.FallbackConsoleLoggerFactory.Value;
                        var exportFormatterLogger = sp.GetService<ILogger<ExportFormatter>>() ?? loggerFactory.CreateLogger<ExportFormatter>();
                        var exportFormatter = new ExportFormatter(exportFormatterLogger);
                        otelLogging.AddProcessor(new EtwLogProcessor(exportFormatter));

                        if (EnvironmentUtils.IsDevelopmentEnvironment())
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
