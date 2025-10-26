// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime
{
    using Microsoft.Extensions.DependencyInjection;
    using System;

    /// <summary>
    /// Provides extension methods for configuring Microsoft Agents A365 SDK with OpenTelemetry tracing.
    /// </summary>
    public static class ObservabilityRuntime
    {
        /// <summary>
        /// Adds the Microsoft Agents A365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configure">Optional configuration delegate for the Builder.</param>
        /// <returns>The configured service collection.</returns>
        public static IServiceCollection AddTracing(
            this IServiceCollection services,
            Action<Builder>? configure = null)
        {
            var builder = new Builder(services);
            configure?.Invoke(builder);
            return builder.Build();
        }
    }
}
