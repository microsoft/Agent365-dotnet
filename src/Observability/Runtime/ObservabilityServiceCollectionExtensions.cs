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
    public static class ObservabilityServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Microsoft Agents A365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configure">Optional configuration delegate for the Builder.</param>
        /// <param name="useOpenTelemetryBuilder">Whether to use the OpenTelemetryBuilder.</param>
        /// <returns>The configured service collection.</returns>
        public static IServiceCollection AddTracing(
            this IServiceCollection services,
            Action<Builder>? configure = null,
            bool useOpenTelemetryBuilder = true)
        {
            var builder = new Builder(services: services, useOpenTelemetryBuilder: useOpenTelemetryBuilder);
            configure?.Invoke(builder);
            return builder.Build();
        }
    }
}