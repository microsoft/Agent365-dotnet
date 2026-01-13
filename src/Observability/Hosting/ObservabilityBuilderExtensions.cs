// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Observability.Hosting
{
    using System;
    using Microsoft.Agents.A365.Observability.Hosting.Middleware;
    using Microsoft.Agents.A365.Observability.Runtime;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
    using Microsoft.Agents.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Provides extension methods for configuring Microsoft Agent 365 SDK with OpenTelemetry tracing.
    /// </summary>
    public static class ObservabilityBuilderExtensions
    {
        /// <summary>
        /// Adds the Microsoft Agent 365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <param name="webHostBuilder">The web host builder to which tracing services will be added.</param>
        /// <param name="configure">An optional delegate to further configure the tracing builder.</param>
        /// <param name="useOpenTelemetryBuilder">Specifies whether to use the OpenTelemetry builder for configuration. Defaults to <c>true</c>.</param>
        /// <param name="agent365ExporterType">The type of Agent 365 exporter to use for tracing. Defaults to <see cref="Agent365ExporterType.Agent365Exporter"/>.</param>
        /// <returns>The original <see cref="IWebHostBuilder"/> instance with tracing configured.</returns>
        public static IWebHostBuilder AddA365Tracing(
            this IWebHostBuilder webHostBuilder,
            Action<Builder>? configure = null,
            bool useOpenTelemetryBuilder = true,
            Agent365ExporterType agent365ExporterType = Agent365ExporterType.Agent365Exporter)
        {
            webHostBuilder.ConfigureServices((context, services) =>
            {
                var localBuilder = new Builder(
                    services: services,
                    useOpenTelemetryBuilder: useOpenTelemetryBuilder,
                    agent365ExporterType: agent365ExporterType,
                    configuration: context.Configuration);
                configure?.Invoke(localBuilder);
                localBuilder.Build();
            });
            return webHostBuilder;
        }

        /// <summary>
        /// Adds the Microsoft Agent 365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <param name="builder">The generic host builder to which tracing services will be added.</param>
        /// <param name="configure">An optional delegate to further configure the tracing builder.</param>
        /// <param name="useOpenTelemetryBuilder">Specifies whether to use the OpenTelemetry builder for configuration. Defaults to <c>true</c>.</param>
        /// <param name="agent365ExporterType">The type of Agent 365 exporter to use for tracing. Defaults to <see cref="Agent365ExporterType.Agent365Exporter"/>.</param>
        /// <returns>The original <see cref="IHostBuilder"/> instance with tracing configured.</returns>
        public static IHostBuilder AddA365Tracing(
            this IHostBuilder builder,
            Action<Builder>? configure = null,
            bool useOpenTelemetryBuilder = true,
            Agent365ExporterType agent365ExporterType = Agent365ExporterType.Agent365Exporter)
        {
            builder.ConfigureServices((context, services) =>
            {
                var localBuilder = new Builder(
                    services: services,
                    useOpenTelemetryBuilder: useOpenTelemetryBuilder,
                    agent365ExporterType: agent365ExporterType,
                    configuration: context.Configuration);
                configure?.Invoke(localBuilder);
                localBuilder.Build();
            });
            return builder;
        }

        /// <summary>
        /// Adds the <see cref="ObservabilityMiddleware"/> to the builder's service collection.
        /// </summary>
        /// <typeparam name="TBuilder">The type of the application builder implementing <see cref="IHostApplicationBuilder"/>.</typeparam>
        /// <param name="builder">The builder to configure.</param>
        /// <returns>The configured builder for method chaining.</returns>
        /// <remarks>
        /// <para>
        /// This method registers the <see cref="ObservabilityMiddleware"/> as a singleton service.
        /// After calling this method, you need to add the middleware to your adapter's pipeline.
        /// </para>
        /// <example>
        /// <code>
        /// // In your startup/program configuration:
        /// builder.WithObservabilityMiddleware();
        /// 
        /// // Then add to your adapter:
        /// adapter.Use(serviceProvider.GetRequiredService&lt;ObservabilityMiddleware&gt;());
        /// // Or use the extension method:
        /// adapter.UseA365Observability(serviceProvider.GetRequiredService&lt;ObservabilityMiddleware&gt;());
        /// </code>
        /// </example>
        /// </remarks>
        public static TBuilder WithObservabilityMiddleware<TBuilder>(this TBuilder builder)
            where TBuilder : IHostApplicationBuilder
        {
            builder.Services.AddSingleton<ObservabilityMiddleware>();
            return builder;
        }

        /// <summary>
        /// Adds the <see cref="ObservabilityMiddleware"/> to the builder's service collection with custom resolvers.
        /// </summary>
        /// <typeparam name="TBuilder">The type of the application builder implementing <see cref="IHostApplicationBuilder"/>.</typeparam>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="agentDetailsResolver">Optional resolver to extract agent details from the turn context.</param>
        /// <param name="callerDetailsResolver">Optional resolver to extract caller details from the turn context.</param>
        /// <returns>The configured builder for method chaining.</returns>
        /// <remarks>
        /// <para>
        /// This method registers the <see cref="ObservabilityMiddleware"/> as a singleton service with custom resolvers.
        /// Use this overload when you need to provide custom logic for extracting agent and caller details from the turn context.
        /// </para>
        /// <example>
        /// <code>
        /// // In your startup/program configuration:
        /// builder.WithObservabilityMiddleware(
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
        public static TBuilder WithObservabilityMiddleware<TBuilder>(
            this TBuilder builder,
            Func<ITurnContext, AgentDetails>? agentDetailsResolver = null,
            Func<ITurnContext, CallerDetails?>? callerDetailsResolver = null)
            where TBuilder : IHostApplicationBuilder
        {
            builder.Services.AddSingleton(sp => new ObservabilityMiddleware(agentDetailsResolver, callerDetailsResolver));
            return builder;
        }
    }
}
