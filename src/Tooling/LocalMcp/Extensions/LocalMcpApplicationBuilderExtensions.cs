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
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseLocalMcpProxy(this WebApplication app)
    {
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
