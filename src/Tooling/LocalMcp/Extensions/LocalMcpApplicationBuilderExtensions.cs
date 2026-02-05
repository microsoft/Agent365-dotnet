// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Endpoints;
using Microsoft.Agents.A365.Tooling.LocalMcp.Models;
using Microsoft.Agents.A365.Tooling.LocalMcp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Extensions;

/// <summary>
/// Extension methods for configuring the Local MCP Proxy middleware in the application pipeline.
/// </summary>
public static class LocalMcpApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Local MCP Proxy endpoints and starts the background cleanup task.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method must be called after AddLocalMcpProxy has been called to register the required services.
    /// </para>
    /// <para>
    /// If no storage was explicitly configured via the <see cref="LocalMcpBuilder"/>, this method
    /// will default to in-memory storage and log a warning. For production deployments, you should
    /// configure a persistent storage backend.
    /// </para>
    /// </remarks>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseLocalMcpProxy(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LocalMcpProxy");

        // Check if ISessionManager is registered
        var sessionManager = app.Services.GetService<ISessionManager>();
        if (sessionManager == null)
        {
            // This should not happen if AddLocalMcpProxy() was called (it registers InMemory by default)
            throw new InvalidOperationException(
                "ISessionManager is not registered. Ensure AddLocalMcpProxy() is called before " +
                "UseLocalMcpProxy().");
        }

        // Log warning if using in-memory storage (the default)
        if (sessionManager is InMemorySessionManager)
        {
            logger.LogWarning(
                "[LocalMcp] Using in-memory storage for session management. " +
                "WARNING: In-memory storage is suitable for DEVELOPMENT ONLY. " +
                "Data will be lost on app restart and cannot scale horizontally. " +
                "For production, configure a persistent storage backend using " +
                ".UseCustomStorage<TSessionManager>() when calling AddLocalMcpProxy().");
        }

        // Enable WebSockets (required for MCP communication)
        app.UseWebSockets();

        // Start background cleanup task
        StartCleanupTask(app);

        // Map all Local MCP endpoints
        app.MapLocalMcpEndpoints();

        return app;
    }

    private static void StartCleanupTask(WebApplication app)
    {
        var sessionManager = app.Services.GetRequiredService<ISessionManager>();
        var options = app.Services.GetRequiredService<IOptions<LocalMcpProxyOptions>>().Value;
        var logger = app.Services.GetRequiredService<ILogger<ISessionManager>>();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.CleanupIntervalSeconds));

                var now = DateTime.UtcNow;
                var staleTimeout = TimeSpan.FromSeconds(options.IdleTimeoutSeconds);
                var pendingTimeout = TimeSpan.FromMinutes(options.PendingSessionTimeoutMinutes);

                foreach (var session in sessionManager.GetAllSessions())
                {
                    var idleTime = now - session.LastActivity;

                    if (idleTime > staleTimeout && session.IsConnected)
                    {
                        logger.LogInformation("[CLEANUP] Session {SessionId} is stale (idle for {IdleSeconds}s), closing WebSocket...",
                            session.SessionId, idleTime.TotalSeconds);

                        try
                        {
                            await session.WebSocket!.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Session timeout due to inactivity",
                                CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[CLEANUP] Error closing WebSocket for {SessionId}", session.SessionId);
                        }

                        sessionManager.RemoveSession(session.SessionId);
                    }
                    else if (!session.IsConnected && (now - session.Created) > pendingTimeout)
                    {
                        sessionManager.RemoveSession(session.SessionId);
                        logger.LogInformation("[CLEANUP] Old disconnected session {SessionId} removed", session.SessionId);
                    }
                }
            }
        });
    }
}
