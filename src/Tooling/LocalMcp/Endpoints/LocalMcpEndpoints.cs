// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.A365.Tooling.LocalMcp.Models;
using Microsoft.Agents.A365.Tooling.LocalMcp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Endpoints;

/// <summary>
/// Maps all Local MCP Proxy endpoints to the application.
/// </summary>
public static class LocalMcpEndpoints
{
    /// <summary>
    /// Maps the Local MCP Proxy endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapLocalMcpEndpoints(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<ISessionManager>>();

        // POST /api/channels/register - Desktop client registration
        app.MapPost("/api/channels/register", (
            ChannelRegistrationRequest request,
            ISessionManager sessionManager) =>
        {
            var registration = sessionManager.RegisterClient(request);
            return Results.Ok(new { message = "Registration successful", clientName = request.ClientName });
        }).AllowAnonymous();

        // GET /api/channels - List registered clients
        app.MapGet("/api/channels", (ISessionManager sessionManager) =>
        {
            var clients = sessionManager.GetAllClients()
                .Select(c => new
                {
                    c.ClientName,
                    c.MachineName,
                    ChannelUri = c.ChannelUri.Length > 40
                        ? c.ChannelUri.Substring(0, 40) + "..."
                        : c.ChannelUri,
                    c.RegisteredAt,
                    c.LastSeen
                });
            return Results.Json(clients);
        }).AllowAnonymous();

        // POST /api/notify/{clientName} - Send WNS notification
        app.MapPost("/api/notify/{clientName}", async (
            string clientName,
            HttpContext context,
            ISessionManager sessionManager,
            IWnsNotificationService wnsService,
            IOptions<LocalMcpProxyOptions> options) =>
        {
            var client = sessionManager.GetClient(clientName);
            if (client == null)
            {
                logger.LogWarning("[WNS NOTIFY] Client '{ClientName}' not found", clientName);
                return Results.NotFound(new { message = "Client not found" });
            }

            // Read the request body
            string requestBody;
            using (var reader = new StreamReader(context.Request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            string? notificationType = null;
            string? requestId = null;
            string? serverId = null;
            string? callbackUrl = null;

            // Parse the request
            if (!string.IsNullOrEmpty(requestBody))
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(requestBody);
                    if (jsonDoc.RootElement.TryGetProperty("type", out var typeElement))
                        notificationType = typeElement.GetString();
                    if (jsonDoc.RootElement.TryGetProperty("requestId", out var reqIdElement))
                        requestId = reqIdElement.GetString();
                    if (jsonDoc.RootElement.TryGetProperty("callbackUrl", out var callbackElement))
                        callbackUrl = callbackElement.GetString();
                    if (jsonDoc.RootElement.TryGetProperty("serverId", out var serverIdElement))
                        serverId = serverIdElement.GetString();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[WNS NOTIFY] Failed to parse request body");
                }
            }

            var scheme = context.Request.IsHttps ? "wss" : "ws";
            var httpScheme = context.Request.IsHttps ? "https" : "http";
            var host = context.Request.Host.ToString();

            // Handle discovery requests
            if (notificationType == "list_servers")
            {
                requestId ??= Guid.NewGuid().ToString();
                callbackUrl ??= $"{httpScheme}://{host}/api/discovery/{requestId}/servers";

                logger.LogInformation("[WNS NOTIFY] Sending DISCOVERY notification to '{ClientName}'", clientName);

                var (success, errorMessage) = await wnsService.SendDiscoveryNotificationAsync(
                    client.ChannelUri, requestId, callbackUrl);

                if (success)
                {
                    return Results.Ok(new { message = "Discovery notification sent", requestId, callbackUrl });
                }
                else
                {
                    return Results.Json(new { message = $"Failed to send notification: {errorMessage}" }, statusCode: 500);
                }
            }
            else
            {
                // Handle MCP server invocation
                var sessionId = Guid.NewGuid().ToString();
                serverId ??= options.Value.DefaultServerId;
                callbackUrl = $"{scheme}://{host}/ws/mcp/{sessionId}?serverId={Uri.EscapeDataString(serverId)}";

                sessionManager.CreateSession(sessionId);

                logger.LogInformation("[WNS NOTIFY] Sending MCP notification to '{ClientName}'", clientName);
                logger.LogInformation("[WNS NOTIFY] Session ID: {SessionId}, Server ID: {ServerId}", sessionId, serverId);

                var (success, errorMessage) = await wnsService.SendNotificationAsync(client.ChannelUri, callbackUrl, serverId);

                if (success)
                {
                    return Results.Ok(new { message = "Notification sent", sessionId, callbackUrl });
                }
                else
                {
                    sessionManager.RemoveSession(sessionId);
                    return Results.Json(new { message = $"Failed to send notification: {errorMessage}" }, statusCode: 500);
                }
            }
        }).AllowAnonymous();

        // WebSocket endpoint for locaproto connection
        app.Map("/ws/mcp/{sessionId}", async (HttpContext context, string sessionId, ISessionManager sessionManager) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            session.WebSocket = webSocket;
            session.UpdateActivity();

            logger.LogInformation("[MCP SESSION] {SessionId} WebSocket connected", sessionId);

            try
            {
                var buffer = new byte[1024 * 4];
                var messageBuilder = new StringBuilder();

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        messageBuilder.Clear();

                        session.UpdateActivity();
                        logger.LogDebug("[WS←LOCAPROTO] {Message}", message);

                        try
                        {
                            var jsonDoc = JsonDocument.Parse(message);
                            if (jsonDoc.RootElement.TryGetProperty("id", out var idElement))
                            {
                                var id = idElement.GetInt32();
                                if (session.PendingRequests.TryRemove(id, out var tcs))
                                {
                                    tcs.SetResult(message);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[WS] Error parsing response");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[MCP SESSION] {SessionId} WebSocket error", sessionId);
            }
            finally
            {
                session.WebSocket = null;
                logger.LogInformation("[MCP SESSION] {SessionId} WebSocket disconnected", sessionId);
            }
        }).AllowAnonymous();

        // GET /api/status/{sessionId} - Check session status
        app.MapGet("/api/status/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            var isConnected = session?.IsConnected ?? false;

            return Results.Json(new
            {
                sessionId,
                registered = isConnected,
                connected = isConnected
            });
        }).AllowAnonymous();

        // POST /api/heartbeat/{sessionId} - Keep session alive
        app.MapPost("/api/heartbeat/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session != null)
            {
                session.UpdateActivity();
                return Results.Ok(new { alive = true });
            }

            return Results.NotFound();
        }).AllowAnonymous();

        // POST /api/mcp/{sessionId} - HTTP proxy for MCP requests
        app.MapPost("/api/mcp/{sessionId}", async (
            HttpContext context,
            string sessionId,
            ISessionManager sessionManager,
            IOptions<LocalMcpProxyOptions> options) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null || !session.IsConnected)
            {
                return Results.Problem("Session not found or WebSocket not connected", statusCode: 404);
            }

            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var requestBody = await reader.ReadToEndAsync();

                session.UpdateActivity();
                logger.LogDebug("[API→WS] {RequestBody}", requestBody);

                var jsonDoc = JsonDocument.Parse(requestBody);

                if (!jsonDoc.RootElement.TryGetProperty("id", out var idElement))
                {
                    logger.LogDebug("[API→WS] Received message without ID (notification)");
                    return Results.Ok(new { message = "Notification received" });
                }

                var requestId = idElement.GetInt32();

                var tcs = new TaskCompletionSource<string>();
                session.PendingRequests.TryAdd(requestId, tcs);

                try
                {
                    var messageBytes = Encoding.UTF8.GetBytes(requestBody);
                    await session.WebSocket!.SendAsync(
                        new ArraySegment<byte>(messageBytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);

                    var responseTask = tcs.Task;
                    var timeout = TimeSpan.FromSeconds(options.Value.McpRequestTimeoutSeconds);
                    var elapsed = TimeSpan.Zero;
                    var checkInterval = TimeSpan.FromSeconds(5);

                    while (elapsed < timeout)
                    {
                        var delayTask = Task.Delay(checkInterval);
                        var completedTask = await Task.WhenAny(responseTask, delayTask);

                        if (completedTask == responseTask)
                            break;

                        session.UpdateActivity();
                        elapsed += checkInterval;
                    }

                    if (!responseTask.IsCompleted)
                    {
                        session.PendingRequests.TryRemove(requestId, out _);
                        return Results.Problem("Request timeout", statusCode: 504);
                    }

                    var jsonResponse = await responseTask;
                    logger.LogDebug("[WS→API] {Response}", jsonResponse);

                    return Results.Content(jsonResponse, "application/json");
                }
                finally
                {
                    session.PendingRequests.TryRemove(requestId, out _);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[MCP PROXY] Error communicating with MCP server");
                return Results.Problem($"Error: {ex.Message}", statusCode: 500);
            }
        }).AllowAnonymous();

        // POST /api/discovery/{requestId}/servers - Receive discovery results
        app.MapPost("/api/discovery/{requestId}/servers", async (
            string requestId,
            HttpContext context,
            ISessionManager sessionManager) =>
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var requestBody = await reader.ReadToEndAsync();

                logger.LogInformation("[DISCOVERY] Received server list for request {RequestId}", requestId);

                var result = new DiscoveryResult
                {
                    RequestId = requestId,
                    Status = "completed",
                    RawResponse = requestBody,
                    ReceivedAt = DateTime.UtcNow
                };

                sessionManager.StoreDiscoveryResult(result);

                return Results.Ok(new { message = "Servers received", requestId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DISCOVERY] Error processing server list for request {RequestId}", requestId);
                return Results.Problem($"Error: {ex.Message}", statusCode: 500);
            }
        }).AllowAnonymous();

        // GET /api/discovery/{requestId}/servers - Poll discovery results
        app.MapGet("/api/discovery/{requestId}/servers", (string requestId, ISessionManager sessionManager) =>
        {
            logger.LogInformation("[DISCOVERY] SDK polling for request {RequestId}", requestId);

            var result = sessionManager.GetDiscoveryResult(requestId);

            if (result != null && result.Status == "completed")
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(result.RawResponse);
                    JsonElement serversArray;
                    string? errorMessage = null;

                    if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        serversArray = jsonDoc.RootElement;
                    }
                    else if (jsonDoc.RootElement.TryGetProperty("servers", out var servers))
                    {
                        serversArray = servers;
                        jsonDoc.RootElement.TryGetProperty("error", out var errorElement);
                        errorMessage = errorElement.ValueKind == JsonValueKind.String ? errorElement.GetString() : null;
                    }
                    else
                    {
                        return Results.Json(new { status = "completed", requestId, servers = new object[] { }, error = "Unexpected response format" });
                    }

                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        return Results.Json(new { status = "error", requestId, servers = new object[] { }, error = errorMessage });
                    }

                    return Results.Json(new { status = "completed", requestId, servers = serversArray });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[DISCOVERY] Failed to parse response");
                    return Results.Json(new { status = "completed", requestId, servers = new object[] { } });
                }
            }

            if (result == null)
            {
                sessionManager.CreatePendingDiscoveryResult(requestId);
            }

            return Results.Json(new { status = result?.Status ?? "pending", requestId });
        }).AllowAnonymous();

        return app;
    }
}
