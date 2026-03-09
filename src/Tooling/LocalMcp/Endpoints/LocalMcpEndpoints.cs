// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.A365.Tooling.LocalMcp.Models;
using Microsoft.Agents.A365.Tooling.LocalMcp.Services;
using Microsoft.Agents.A365.Tooling.Services;
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
            ISessionManager sessionManager,
            IServiceProvider serviceProvider) =>
        {
            var registration = sessionManager.RegisterClient(request);
            
            // Invalidate policy cache so next tool call finds the registered desktop
            var policyService = serviceProvider.GetService<IMcpPolicyEnforcementService>();
            if (policyService != null && !string.IsNullOrEmpty(registration.UserIdentifier))
            {
                policyService.InvalidateUserCache(registration.UserIdentifier);
            }
            
            return Results.Ok(new { 
                message = "Registration successful", 
                clientName = registration.ClientName,
                machineName = registration.MachineName,
                userIdentifier = registration.UserIdentifier
            });
        }).AllowAnonymous();

        // GET /api/channels - List registered clients
        app.MapGet("/api/channels", (ISessionManager sessionManager) =>
        {
            var clients = sessionManager.GetAllClients()
                .Select(c => new
                {
                    c.ClientName,
                    c.UserIdentifier,
                    c.MachineName,
                    ChannelUri = c.ChannelUri.Length > 40
                        ? c.ChannelUri.Substring(0, 40) + "..."
                        : c.ChannelUri,
                    c.RegisteredAt,
                    c.LastSeen
                });
            return Results.Json(clients);
        }).AllowAnonymous();

        // GET /api/channels/by-user/{userIdentifier} - Get clients registered to a specific user
        app.MapGet("/api/channels/by-user/{userIdentifier}", (
            string userIdentifier,
            HttpContext context,
            ISessionManager sessionManager,
            IServiceProvider serviceProvider) =>
        {
            // URL decode the user identifier (email addresses contain @ and other special chars)
            var decodedUserIdentifier = System.Net.WebUtility.UrlDecode(userIdentifier);
            
            logger.LogInformation("[CHANNELS] Looking up clients for user '{UserIdentifier}'", decodedUserIdentifier);
            
            var clients = sessionManager.GetClientsByUser(decodedUserIdentifier);
            var clientList = clients.ToList();
            
            if (clientList.Count == 0)
            {
                logger.LogWarning("[CHANNELS] No clients registered for user '{UserIdentifier}'", decodedUserIdentifier);
                
                // Return registration URL
                var regScheme = context.Request.IsHttps ? "https" : "http";
                var regHost = context.Request.Host.ToString();
                var registrationProtocolUrl = $"locaproto:?action=register&callback={regScheme}://{regHost}/api/channels/register&user={System.Net.WebUtility.UrlEncode(decodedUserIdentifier)}";

                // Append serverIds so the Bridging App knows which local MCP servers to provision
                var policyService = serviceProvider.GetService<IMcpPolicyEnforcementService>();
                if (policyService != null)
                {
                    var localServerIds = policyService.GetRegisteredLocalServerIds();
                    if (localServerIds.Count > 0)
                    {
                        var encodedServerIds = System.Net.WebUtility.UrlEncode(string.Join(",", localServerIds));
                        registrationProtocolUrl += $"&serverIds={encodedServerIds}";
                    }
                }
                
                return Results.Json(new 
                { 
                    error = "NO_CLIENTS_REGISTERED",
                    message = $"No desktops are registered for user '{decodedUserIdentifier}'. Please register your desktop to enable local file access.",
                    userIdentifier = decodedUserIdentifier,
                    requiresRegistration = true,
                    registrationProtocolUrl = registrationProtocolUrl
                }, statusCode: 404);
            }
            
            logger.LogInformation("[CHANNELS] Found {Count} client(s) for user '{UserIdentifier}'", clientList.Count, decodedUserIdentifier);
            
            return Results.Json(new
            {
                userIdentifier = decodedUserIdentifier,
                clients = clientList.Select(c => new
                {
                    c.ClientName,
                    c.MachineName,
                    c.RegisteredAt,
                    c.LastSeen
                })
            });
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
                logger.LogWarning("[WNS NOTIFY] Client '{ClientName}' not found - desktop registration required", clientName);
                
                // Return a specific error with registration URL so the agent can prompt the user
                var regScheme = context.Request.IsHttps ? "https" : "http";
                var regHost = context.Request.Host.ToString();
                var registrationProtocolUrl = $"locaproto:?action=register&callback={regScheme}://{regHost}/api/channels/register";
                
                return Results.Json(new 
                { 
                    error = "CLIENT_NOT_REGISTERED",
                    message = $"Desktop client '{clientName}' is not registered. Please register your desktop to enable local file access.",
                    clientName = clientName,
                    requiresRegistration = true,
                    registrationProtocolUrl = registrationProtocolUrl
                }, statusCode: 404);
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
            string? agentAppId = null;
            CloudMcpProxyConfig? cloudConfig = null;

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
                    if (jsonDoc.RootElement.TryGetProperty("agentAppId", out var agentAppIdElement))
                        agentAppId = agentAppIdElement.GetString();
                    
                    // Parse cloud server configuration if present
                    if (jsonDoc.RootElement.TryGetProperty("cloudConfig", out var cloudConfigElement))
                    {
                        cloudConfig = new CloudMcpProxyConfig
                        {
                            ServerId = serverId ?? "unknown",
                            Endpoint = cloudConfigElement.TryGetProperty("endpoint", out var ep) ? ep.GetString() ?? "" : "",
                            Transport = cloudConfigElement.TryGetProperty("transport", out var tr) ? tr.GetString() ?? "sse" : "sse",
                            AuthType = cloudConfigElement.TryGetProperty("authType", out var at) ? at.GetString() ?? "intune_managed" : "intune_managed",
                            Scope = cloudConfigElement.TryGetProperty("scope", out var sc) ? sc.GetString() : null,
                            Audience = cloudConfigElement.TryGetProperty("audience", out var au) ? au.GetString() : null,
                            BearerToken = cloudConfigElement.TryGetProperty("bearerToken", out var bt) ? bt.GetString() : null
                        };
                        
                        // Parse additional headers if present
                        if (cloudConfigElement.TryGetProperty("additionalHeaders", out var headers) && 
                            headers.ValueKind == JsonValueKind.Object)
                        {
                            cloudConfig.AdditionalHeaders = new Dictionary<string, string>();
                            foreach (var header in headers.EnumerateObject())
                            {
                                cloudConfig.AdditionalHeaders[header.Name] = header.Value.GetString() ?? "";
                            }
                        }
                        
                        logger.LogInformation("[WNS NOTIFY] Cloud MCP config detected - Endpoint: {Endpoint}, AuthType: {AuthType}", 
                            cloudConfig.Endpoint, cloudConfig.AuthType);
                    }
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
                if (string.IsNullOrEmpty(serverId))
                {
                    return Results.BadRequest(new { message = "serverId query parameter is required" });
                }

                var sessionId = Guid.NewGuid().ToString();
                callbackUrl = $"{scheme}://{host}/ws/mcp/{sessionId}?serverId={Uri.EscapeDataString(serverId)}";

                sessionManager.CreateSession(sessionId);

                logger.LogInformation("[WNS NOTIFY] Sending MCP notification to '{ClientName}'", clientName);
                logger.LogInformation("[WNS NOTIFY] Session ID: {SessionId}, Server ID: {ServerId}", sessionId, serverId);
                if (!string.IsNullOrEmpty(agentAppId))
                {
                    logger.LogInformation("[WNS NOTIFY] Agent App ID: {AgentAppId}", agentAppId);
                }

                (bool success, string? errorMessage) result;
                
                if (cloudConfig != null)
                {
                    // Cloud MCP server - send extended notification with cloud config
                    logger.LogInformation("[WNS NOTIFY] Using CLOUD MCP notification for '{ServerId}'", serverId);
                    cloudConfig.ServerId = serverId;
                    result = await wnsService.SendCloudMcpNotificationAsync(client.ChannelUri, callbackUrl, cloudConfig, agentAppId);
                }
                else
                {
                    // Local MCP server - send standard notification
                    result = await wnsService.SendNotificationAsync(client.ChannelUri, callbackUrl, serverId, agentAppId);
                }

                if (result.success)
                {
                    return Results.Ok(new { message = "Notification sent", sessionId, callbackUrl, serverType = cloudConfig != null ? "cloud" : "local" });
                }
                else
                {
                    sessionManager.RemoveSession(sessionId);
                    return Results.Json(new { message = $"Failed to send notification: {result.errorMessage}" }, statusCode: 500);
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
                            
                            // Check for REREGISTRATION_REQUIRED error from locaproto
                            if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                            {
                                var errorCode = errorElement.GetString();
                                if (errorCode == "REREGISTRATION_REQUIRED")
                                {
                                    logger.LogWarning("[WS←LOCAPROTO] Desktop requires re-registration");
                                    
                                    // Extract details from the error response
                                    var agentHost = jsonDoc.RootElement.TryGetProperty("agentHost", out var hostEl) 
                                        ? hostEl.GetString() : null;
                                    var machineName = jsonDoc.RootElement.TryGetProperty("machineName", out var machineEl) 
                                        ? machineEl.GetString() : null;
                                    var protocolUrl = jsonDoc.RootElement.TryGetProperty("protocolUrl", out var protoEl) 
                                        ? protoEl.GetString() : null;

                                    logger.LogWarning("[WS←LOCAPROTO] Machine '{MachineName}' needs to re-register with agent", machineName);
                                    logger.LogInformation("[WS←LOCAPROTO] Re-registration protocol URL: {ProtocolUrl}", protocolUrl);

                                    // Mark the session as requiring re-registration
                                    session.RequiresReregistration = true;
                                    session.ReregistrationProtocolUrl = protocolUrl;

                                    // Complete any pending requests with the re-registration error
                                    foreach (var pending in session.PendingRequests)
                                    {
                                        if (session.PendingRequests.TryRemove(pending.Key, out var pendingTcs))
                                        {
                                            pendingTcs.SetResult(message);
                                        }
                                    }

                                    // Close the WebSocket since we need re-registration
                                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Re-registration required", CancellationToken.None);
                                    break;
                                }
                            }
                            
                            if (jsonDoc.RootElement.TryGetProperty("id", out var idElement))
                            {
                                var id = idElement.ToString();
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
                connected = isConnected,
                registered = isConnected,
                webSocketConnected = isConnected
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

                var requestId = idElement.ToString();

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

        // POST /api/intune-check/{clientName} - Initiate Intune status check via WNS
        app.MapPost("/api/intune-check/{clientName}", async (
            string clientName,
            HttpContext context,
            ISessionManager sessionManager,
            IWnsNotificationService wnsService) =>
        {
            var client = sessionManager.GetClient(clientName);
            if (client == null)
            {
                logger.LogWarning("[INTUNE CHECK] Client '{ClientName}' not found - desktop registration required", clientName);
                
                // Return the same CLIENT_NOT_REGISTERED error format used by /api/notify
                var regScheme = context.Request.IsHttps ? "https" : "http";
                var regHost = context.Request.Host.ToString();
                var registrationProtocolUrl = $"locaproto:?action=register&callback={regScheme}://{regHost}/api/channels/register";
                
                return Results.Json(new 
                { 
                    error = "CLIENT_NOT_REGISTERED",
                    message = $"Desktop client '{clientName}' is not registered. Please register your desktop to enable local file access.",
                    clientName = clientName,
                    requiresRegistration = true,
                    registrationProtocolUrl = registrationProtocolUrl
                }, statusCode: 404);
            }

            var requestId = Guid.NewGuid().ToString();
            var httpScheme = context.Request.IsHttps ? "https" : "http";
            var host = context.Request.Host.ToString();
            var callbackUrl = $"{httpScheme}://{host}/api/intune-response/{requestId}";

            sessionManager.CreatePendingIntuneStatusResult(requestId);

            logger.LogInformation("[INTUNE CHECK] Sending Intune check notification to '{ClientName}'", clientName);
            logger.LogInformation("[INTUNE CHECK] Request ID: {RequestId}", requestId);
            logger.LogInformation("[INTUNE CHECK] Callback URL: {CallbackUrl}", callbackUrl);

            var (success, errorMessage) = await wnsService.SendIntuneCheckNotificationAsync(
                client.ChannelUri, requestId, callbackUrl);

            if (success)
            {
                return Results.Ok(new { message = "Intune check notification sent", requestId, callbackUrl });
            }
            else
            {
                return Results.Json(new { message = $"Failed to send notification: {errorMessage}" }, statusCode: 500);
            }
        }).AllowAnonymous();

        // POST /api/intune-response/{requestId} - Callback for Intune status from locaproto
        app.MapPost("/api/intune-response/{requestId}", async (
            string requestId,
            HttpContext context,
            ISessionManager sessionManager) =>
        {
            logger.LogInformation("[INTUNE RESPONSE] Received Intune status for request {RequestId}", requestId);

            string requestBody;
            using (var reader = new StreamReader(context.Request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            logger.LogInformation("[INTUNE RESPONSE] Raw response: {Response}", requestBody);

            try
            {
                var statusResult = JsonSerializer.Deserialize<IntuneStatusResult>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (statusResult != null)
                {
                    statusResult.RequestId = requestId;
                    statusResult.Status = "completed";
                    statusResult.ReceivedAt = DateTime.UtcNow;
                    sessionManager.StoreIntuneStatusResult(statusResult);
                    return Results.Ok(new { message = "Intune status received", requestId });
                }
                else
                {
                    return Results.BadRequest(new { message = "Failed to parse Intune status" });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[INTUNE RESPONSE] Failed to parse response");
                return Results.BadRequest(new { message = $"Failed to parse Intune status: {ex.Message}" });
            }
        }).AllowAnonymous();

        // GET /api/intune-status/{requestId} - Poll for Intune status result
        app.MapGet("/api/intune-status/{requestId}", (string requestId, ISessionManager sessionManager) =>
        {
            logger.LogDebug("[INTUNE STATUS] Polling for request {RequestId}", requestId);

            var result = sessionManager.GetIntuneStatusResult(requestId);

            if (result != null && result.Status == "completed")
            {
                return Results.Json(new
                {
                    status = "completed",
                    requestId,
                    isIntuneManaged = result.IsIntuneManaged,
                    isAzureAdJoined = result.IsAzureAdJoined,
                    mdmUrl = result.MdmUrl,
                    enrolledUserPrincipalName = result.EnrolledUserPrincipalName,
                    tenantId = result.TenantId,
                    deviceId = result.DeviceId,
                    machineName = result.MachineName,
                    checkedAt = result.CheckedAt
                });
            }

            if (result == null)
            {
                sessionManager.CreatePendingIntuneStatusResult(requestId);
            }

            return Results.Json(new { status = result?.Status ?? "pending", requestId });
        }).AllowAnonymous();

        return app;
    }
}
