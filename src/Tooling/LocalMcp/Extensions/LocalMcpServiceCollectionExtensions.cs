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
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration for binding options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalMcpProxy(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.Configure<WnsConfiguration>(configuration.GetSection("WnsConfiguration"));
        services.Configure<LocalMcpProxyOptions>(configuration.GetSection(LocalMcpProxyOptions.SectionName));

        // Register services
        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddSingleton<IWnsNotificationService, WnsNotificationService>();
        services.AddSingleton<IIntuneStatusService, IntuneStatusService>();

        // Ensure HttpClientFactory is available
        services.AddHttpClient();

        return services;
    }

    /// <summary>
    /// Adds Local MCP Proxy services to the service collection with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureWns">Action to configure WNS settings.</param>
    /// <param name="configureOptions">Optional action to configure proxy options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalMcpProxy(
        this IServiceCollection services,
        Action<WnsConfiguration> configureWns,
        Action<LocalMcpProxyOptions>? configureOptions = null)
    {
        // Configure options
        services.Configure(configureWns);

        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<LocalMcpProxyOptions>(_ => { });
        }

        // Register services
        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddSingleton<IWnsNotificationService, WnsNotificationService>();
        services.AddSingleton<IIntuneStatusService, IntuneStatusService>();

        // Ensure HttpClientFactory is available
        services.AddHttpClient();

        return services;
    }
}
