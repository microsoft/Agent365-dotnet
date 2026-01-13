// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.A365.Observability.Hosting.Middleware;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.A365.Observability.Hosting.Extensions
{
    /// <summary>
    /// Extension methods for adding observability middleware to the channel adapter.
    /// </summary>
    public static class ChannelAdapterExtensions
    {
        /// <summary>
        /// Adds the <see cref="ObservabilityMiddleware"/> to the adapter's pipeline.
        /// </summary>
        /// <param name="adapter">The channel adapter to add the middleware to.</param>
        /// <returns>The updated channel adapter.</returns>
        /// <remarks>
        /// <para>
        /// This method adds the <see cref="ObservabilityMiddleware"/> to the adapter's pipeline,
        /// which creates InputScope and OutputScope spans for incoming and outgoing activities respectively.
        /// </para>
        /// <example>
        /// <code>
        /// // In your adapter configuration:
        /// adapter.UseA365Observability();
        /// </code>
        /// </example>
        /// </remarks>
        public static IChannelAdapter UseA365Observability(this IChannelAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            return adapter.Use(new ObservabilityMiddleware());
        }

        /// <summary>
        /// Adds the <see cref="ObservabilityMiddleware"/> to the adapter's pipeline with custom resolvers.
        /// </summary>
        /// <param name="adapter">The channel adapter to add the middleware to.</param>
        /// <param name="agentDetailsResolver">Optional resolver to extract agent details from the turn context.</param>
        /// <param name="callerDetailsResolver">Optional resolver to extract caller details from the turn context.</param>
        /// <returns>The updated channel adapter.</returns>
        /// <remarks>
        /// <para>
        /// This method adds the <see cref="ObservabilityMiddleware"/> to the adapter's pipeline with custom resolvers.
        /// Use this overload when you need to provide custom logic for extracting agent and caller details from the turn context.
        /// </para>
        /// <example>
        /// <code>
        /// // In your adapter configuration:
        /// adapter.UseA365Observability(
        ///     agentDetailsResolver: turnContext => new AgentDetails(
        ///         agentId: "my-agent-id",
        ///         agentName: "My Agent"),
        ///     callerDetailsResolver: turnContext => new CallerDetails(
        ///         callerId: turnContext.Activity?.From?.Id ?? "unknown",
        ///         callerName: turnContext.Activity?.From?.Name ?? "Unknown",
        ///         callerUpn: turnContext.Activity?.From?.Name ?? "unknown"));
        /// </code>
        /// </example>
        /// </remarks>
        public static IChannelAdapter UseA365Observability(
            this IChannelAdapter adapter,
            Func<ITurnContext, AgentDetails>? agentDetailsResolver = null,
            Func<ITurnContext, CallerDetails?>? callerDetailsResolver = null)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            return adapter.Use(new ObservabilityMiddleware(agentDetailsResolver, callerDetailsResolver));
        }

        /// <summary>
        /// Adds the provided <see cref="ObservabilityMiddleware"/> instance to the adapter's pipeline.
        /// </summary>
        /// <param name="adapter">The channel adapter to add the middleware to.</param>
        /// <param name="middleware">The observability middleware instance to add.</param>
        /// <returns>The updated channel adapter.</returns>
        /// <remarks>
        /// <para>
        /// Use this method when you have already created an <see cref="ObservabilityMiddleware"/> instance,
        /// for example when using dependency injection.
        /// </para>
        /// <example>
        /// <code>
        /// // When using DI:
        /// var middleware = serviceProvider.GetRequiredService&lt;ObservabilityMiddleware&gt;();
        /// adapter.UseA365Observability(middleware);
        /// </code>
        /// </example>
        /// </remarks>
        public static IChannelAdapter UseA365Observability(
            this IChannelAdapter adapter,
            ObservabilityMiddleware middleware)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            if (middleware == null)
            {
                throw new ArgumentNullException(nameof(middleware));
            }

            return adapter.Use(middleware);
        }
    }
}
