// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Extensions;

/// <summary>
/// Builder for configuring Local MCP Proxy services.
/// </summary>
/// <remarks>
/// <para>
/// The LocalMcpBuilder allows agent developers to configure storage for session management.
/// By default, in-memory storage is used which is suitable for development but not production.
/// </para>
/// <para>
/// For production deployments, implement <see cref="ISessionManager"/> with your preferred
/// storage backend (Cosmos DB, Redis, Table Storage, SQL Server, etc.) and register it
/// using <see cref="UseCustomStorage{TSessionManager}"/> or <see cref="UseCustomStorage(Func{IServiceProvider, ISessionManager})"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Development: Use in-memory storage (default)
/// builder.Services.AddLocalMcpProxy(builder.Configuration)
///     .UseInMemoryStorage();
///
/// // Production: Use custom storage implementation
/// builder.Services.AddLocalMcpProxy(builder.Configuration)
///     .UseCustomStorage&lt;CosmosDbSessionManager&gt;();
///
/// // Production: Use factory for complex initialization
/// builder.Services.AddLocalMcpProxy(builder.Configuration)
///     .UseCustomStorage(sp =&gt; new RedisSessionManager(
///         sp.GetRequiredService&lt;IConnectionMultiplexer&gt;(),
///         sp.GetRequiredService&lt;ILogger&lt;RedisSessionManager&gt;&gt;()
///     ));
/// </code>
/// </example>
public class LocalMcpBuilder
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private bool _storageConfigured;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalMcpBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    public LocalMcpBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
        _storageConfigured = false;
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration => _configuration;

    /// <summary>
    /// Gets a value indicating whether storage has been configured.
    /// </summary>
    internal bool StorageConfigured => _storageConfigured;

    /// <summary>
    /// Use in-memory storage for session management.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Warning:</strong> In-memory storage is suitable for development only.
    /// Data will be lost when the application restarts and cannot be shared across
    /// multiple instances for horizontal scaling.
    /// </para>
    /// <para>
    /// For production deployments, implement <see cref="ISessionManager"/> with a
    /// persistent storage backend.
    /// </para>
    /// </remarks>
    /// <returns>The builder for chaining.</returns>
    public LocalMcpBuilder UseInMemoryStorage()
    {
        // Remove any existing ISessionManager registration and add InMemory
        RemoveExistingSessionManager();
        _services.AddSingleton<ISessionManager, InMemorySessionManager>();
        _storageConfigured = true;
        return this;
    }

    /// <summary>
    /// Use a custom storage implementation for session management.
    /// </summary>
    /// <typeparam name="TSessionManager">The type implementing <see cref="ISessionManager"/>.</typeparam>
    /// <remarks>
    /// <para>
    /// Register your custom <see cref="ISessionManager"/> implementation that uses your
    /// preferred storage backend (Cosmos DB, Redis, Table Storage, SQL Server, etc.).
    /// </para>
    /// <para>
    /// The implementation should handle:
    /// <list type="bullet">
    ///   <item><description>Client registrations (persistent, survives app restarts)</description></item>
    ///   <item><description>MCP sessions (can be in-memory, tied to WebSocket connections)</description></item>
    ///   <item><description>Discovery results (short-lived, can have TTL)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddLocalMcpProxy(builder.Configuration)
    ///     .UseCustomStorage&lt;CosmosDbSessionManager&gt;();
    /// </code>
    /// </example>
    public LocalMcpBuilder UseCustomStorage<TSessionManager>()
        where TSessionManager : class, ISessionManager
    {
        // Remove any existing ISessionManager registration (including default InMemory)
        RemoveExistingSessionManager();
        _services.AddSingleton<ISessionManager, TSessionManager>();
        _storageConfigured = true;
        return this;
    }

    /// <summary>
    /// Use a custom storage implementation for session management with a factory method.
    /// </summary>
    /// <param name="factory">A factory function that creates the session manager.</param>
    /// <remarks>
    /// <para>
    /// Use this overload when your <see cref="ISessionManager"/> implementation requires
    /// complex initialization or dependencies that need to be resolved from the service provider.
    /// </para>
    /// </remarks>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddLocalMcpProxy(builder.Configuration)
    ///     .UseCustomStorage(sp => new RedisSessionManager(
    ///         sp.GetRequiredService&lt;IConnectionMultiplexer&gt;(),
    ///         sp.GetRequiredService&lt;ILogger&lt;RedisSessionManager&gt;&gt;()
    ///     ));
    /// </code>
    /// </example>
    public LocalMcpBuilder UseCustomStorage(Func<IServiceProvider, ISessionManager> factory)
    {
        // Remove any existing ISessionManager registration (including default InMemory)
        RemoveExistingSessionManager();
        _services.AddSingleton(factory);
        _storageConfigured = true;
        return this;
    }

    /// <summary>
    /// Removes any existing ISessionManager registration from the service collection.
    /// </summary>
    private void RemoveExistingSessionManager()
    {
        var existingRegistration = _services.FirstOrDefault(d => d.ServiceType == typeof(ISessionManager));
        if (existingRegistration != null)
        {
            _services.Remove(existingRegistration);
        }
    }

    /// <summary>
    /// Ensures that storage has been configured. If not, defaults to in-memory storage.
    /// </summary>
    /// <remarks>
    /// This is called internally when <see cref="LocalMcpApplicationBuilderExtensions.UseLocalMcpProxy"/>
    /// is invoked. If no storage has been explicitly configured, it defaults to in-memory storage
    /// with a warning logged.
    /// </remarks>
    internal void EnsureStorageConfigured()
    {
        if (!_storageConfigured)
        {
            // Check if ISessionManager was already registered by the developer
            var existingRegistration = _services.FirstOrDefault(d => d.ServiceType == typeof(ISessionManager));
            if (existingRegistration == null)
            {
                // Default to in-memory storage
                UseInMemoryStorage();
            }
            else
            {
                _storageConfigured = true;
            }
        }
    }
}
