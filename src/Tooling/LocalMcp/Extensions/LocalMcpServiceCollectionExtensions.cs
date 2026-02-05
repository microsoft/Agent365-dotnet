// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Models;
using Microsoft.Agents.A365.Tooling.LocalMcp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Extensions;

/// <summary>
/// Extension methods for adding Local MCP Proxy services to the DI container.
/// </summary>
public static class LocalMcpServiceCollectionExtensions
{
    /// <summary>
    /// Adds Local MCP Proxy services to the service collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method returns a <see cref="LocalMcpBuilder"/> which allows you to configure
    /// the storage backend for session management. By default, in-memory storage is used
    /// if no storage is explicitly configured.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> For production deployments, you should configure a persistent
    /// storage backend using one of the builder methods:
    /// <list type="bullet">
    ///   <item><description><see cref="LocalMcpBuilder.UseInMemoryStorage"/> - Development only</description></item>
    ///   <item><description><see cref="LocalMcpBuilder.UseCustomStorage{TSessionManager}"/> - Your implementation</description></item>
    ///   <item><description><see cref="LocalMcpBuilder.UseCustomStorage(Func{IServiceProvider, ISessionManager})"/> - Factory method</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration for binding options.</param>
    /// <returns>A <see cref="LocalMcpBuilder"/> for configuring storage and other options.</returns>
    /// <example>
    /// <code>
    /// // Development: Use in-memory storage (default)
    /// builder.Services.AddLocalMcpProxy(builder.Configuration)
    ///     .UseInMemoryStorage();
    ///
    /// // Production: Use custom storage
    /// builder.Services.AddLocalMcpProxy(builder.Configuration)
    ///     .UseCustomStorage&lt;CosmosDbSessionManager&gt;();
    /// </code>
    /// </example>
    public static LocalMcpBuilder AddLocalMcpProxy(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options from configuration
        services.Configure<WnsConfiguration>(configuration.GetSection("WnsConfiguration"));
        services.Configure<LocalMcpProxyOptions>(configuration.GetSection(LocalMcpProxyOptions.SectionName));

        // Register core services
        services.AddSingleton<IWnsNotificationService, WnsNotificationService>();

        // Register default in-memory storage (can be overridden by builder methods)
        // This ensures zero-config works for development
        services.AddSingleton<ISessionManager, InMemorySessionManager>();

        // Ensure HttpClientFactory is available
        services.AddHttpClient();

        // Return builder for fluent configuration (storage methods will override the default)
        return new LocalMcpBuilder(services, configuration);
    }

    /// <summary>
    /// Adds Local MCP Proxy services to the service collection with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureWns">Action to configure WNS settings.</param>
    /// <param name="configureOptions">Optional action to configure proxy options.</param>
    /// <returns>A <see cref="LocalMcpBuilder"/> for configuring storage and other options.</returns>
    public static LocalMcpBuilder AddLocalMcpProxy(
        this IServiceCollection services,
        Action<WnsConfiguration> configureWns,
        Action<LocalMcpProxyOptions>? configureOptions = null)
    {
        // Configure options via actions
        services.Configure(configureWns);

        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<LocalMcpProxyOptions>(_ => { });
        }

        // Register core services
        services.AddSingleton<IWnsNotificationService, WnsNotificationService>();

        // Register default in-memory storage (can be overridden by builder methods)
        services.AddSingleton<ISessionManager, InMemorySessionManager>();

        // Ensure HttpClientFactory is available
        services.AddHttpClient();

        // Return builder for fluent configuration (no configuration object available in this overload)
        return new LocalMcpBuilder(services, null!);
    }
}
