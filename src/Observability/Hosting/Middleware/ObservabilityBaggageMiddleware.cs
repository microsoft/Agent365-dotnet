// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Hosting.Middleware
{
    /// <summary>
    /// ASP.NET Core middleware for setting per-request observability context (baggage).
    /// </summary>
    public sealed class ObservabilityBaggageMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Func<HttpContext, (string? tenant, string? agentId)> _resolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservabilityBaggageMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="resolver">Function to resolve tenant and agent IDs from the HTTP context.</param>
        /// <param name="tagActivity">Whether to tag the current activity (currently unused).</param>
        public ObservabilityBaggageMiddleware(
            RequestDelegate next,
            Func<HttpContext, (string? tenant, string? agentId)>? resolver = null,
            bool tagActivity = true)
        {
            _next = next;
            // require explicit resolver to avoid accidental usage without tenant/agent resolution
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>
        /// Invokes the middleware to set observability context for the current request.
        /// </summary>
        /// <param name="ctx">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext ctx)
        {
            var (tenant, agent) = _resolver(ctx);

            using (new BaggageBuilder()
                .TenantId(tenant)
                .AgentId(agent)
                .Build())
            {
                await _next(ctx);
            }
        }

    }

    /// <summary>
    /// A middleware to set per-request observability context (baggage). 
    /// Middleware to be used in ASP.NET scenarios.
    /// </summary>
    public static class ObservabilityBaggageMiddlewareExtensions
    {
        /// <summary>
        /// Adds the observability baggage middleware to the application pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="resolver">Function to resolve tenant and agent IDs from the HTTP context.</param>
        /// <returns>The application builder for method chaining.</returns>
        public static IApplicationBuilder UseObservabilityRequestContext(
            this IApplicationBuilder app,
            Func<HttpContext, (string? tenant, string? agentId)>? resolver)
            => app.UseMiddleware<ObservabilityBaggageMiddleware>(resolver);
    }
}
