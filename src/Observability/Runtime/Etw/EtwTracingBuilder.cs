// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Etw
{
    /// <summary>
    /// Builds the ETW + OpenTelemetry tracing configuration.
    /// </summary>
    public sealed class EtwTracingBuilder
    {
        private readonly IServiceCollection _services;
        private bool _isBuilt = false;
        private static readonly Lazy<ILoggerFactory> FallbackConsoleLoggerFactory = new Lazy<ILoggerFactory>(() => LoggerFactory.Create(b => b.AddConsole()));

        /// <summary>
        /// Initializes a new instance of the <see cref="EtwTracingBuilder"/> class.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        internal EtwTracingBuilder(IServiceCollection services)
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
                .AddSingleton<ExportFormatter>(sp =>
                {
                    var logger = sp.GetService<ILogger<ExportFormatter>>() ?? FallbackConsoleLoggerFactory.Value.CreateLogger<ExportFormatter>();
                    return new ExportFormatter(logger);
                })
                .AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource(OpenTelemetryConstants.SourceName)
                        .AddProcessor(new ActivityProcessor())
                        .AddProcessor(sp =>
                        {
                            return new EtwScopeEventProcessor(formatter: sp.GetRequiredService<ExportFormatter>(), logger: sp.GetService<ILogger<EtwScopeEventProcessor>>() ?? FallbackConsoleLoggerFactory.Value.CreateLogger<EtwScopeEventProcessor>());
                        });

                    if (EnvironmentUtils.IsDevelopmentEnvironment())
                    {
                        tracing.AddConsoleExporter();
                    }
                });

            _isBuilt = true;
        }
    }
}
