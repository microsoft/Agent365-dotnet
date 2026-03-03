// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.A365.Observability.Hosting.Middleware;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.A365.Observability.Hosting
{
    /// <summary>
    /// Extension methods for registering observability middleware on an <see cref="IChannelAdapter"/>.
    /// </summary>
    public static class ObservabilityMiddlewareExtensions
    {
        /// <summary>
        /// Adds the observability middleware to the adapter pipeline.
        /// </summary>
        /// <param name="adapter">The channel adapter to add middleware to.</param>
        /// <param name="enableBaggage">
        /// When <c>true</c> (the default), registers <see cref="BaggageTurnMiddleware"/>
        /// which propagates OpenTelemetry baggage context from the <see cref="ITurnContext"/>.
        /// </param>
        /// <param name="enableOutputLogging">
        /// When <c>true</c> (the default), registers <see cref="OutputLoggingMiddleware"/>
        /// which creates <c>OutputScope</c> spans for outgoing messages.
        /// </param>
        /// <returns>The adapter, for method chaining.</returns>
        /// <remarks>
        /// <para>
        /// <b>Baggage middleware</b> should be registered early in the pipeline so that
        /// downstream middleware and handlers run inside the baggage context.
        /// </para>
        /// <para>
        /// <b>Output logging middleware</b> captures outgoing message content verbatim
        /// as span attributes.  Ensure your telemetry backend is appropriate for the
        /// data sensitivity of your agent before enabling this in production.
        /// </para>
        /// <example>
        /// <code>
        /// // In Program.cs, after building the app:
        /// var app = builder.Build();
        /// var adapter = app.Services.GetRequiredService&lt;IChannelAdapter&gt;();
        /// adapter.UseObservabilityMiddleware();
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="adapter"/> is <c>null</c>.</exception>
        public static IChannelAdapter UseObservabilityMiddleware(
            this IChannelAdapter adapter,
            bool enableBaggage = true,
            bool enableOutputLogging = true)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            if (enableBaggage)
            {
                adapter.Use(new BaggageTurnMiddleware());
            }

            if (enableOutputLogging)
            {
                adapter.Use(new OutputLoggingMiddleware());
            }

            return adapter;
        }
    }
}
