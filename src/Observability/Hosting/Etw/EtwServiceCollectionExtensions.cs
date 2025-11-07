// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.Agents.A365.Observability.Hosting.Etw
{
    /// <summary>
    /// Extension methods for configuring ETW in <see cref="IServiceCollection"/>.
    /// </summary>
    public static class EtwServiceCollectionExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry tracing with ETW to the service collection.
        /// </summary>
        public static IServiceCollection AddTracingWithEtw(this IServiceCollection services, Action<EtwBuilder>? configure = null)
        {
            var builder = new EtwBuilder(services);

            configure?.Invoke(builder);
            
            return builder.Build();
        }
    }
}
