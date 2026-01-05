// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Etw
{
    /// <summary>
    /// Extension methods for configuring ETW in <see cref="IServiceCollection"/>.
    /// </summary>
    public static class EtwServiceCollectionExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry tracing with ETW to the service collection.
        /// </summary>
        public static IServiceCollection AddTracingWithEtw(this IServiceCollection services, Action<EtwTracingBuilder>? configure = null)
        {
            var builder = new EtwTracingBuilder(services);

            configure?.Invoke(builder);
            
            return builder.Build();
        }

        /// <summary>
        /// Adds OpenTelemetry logging with ETW to the service collection.
        /// </summary>
        public static IServiceCollection AddLoggingWithEtw(this IServiceCollection services, Action<EtwLoggingBuilder>? configure = null)
        {
            var builder = new EtwLoggingBuilder(services);
            configure?.Invoke(builder);
            return builder.Build();
        }
    }
}
