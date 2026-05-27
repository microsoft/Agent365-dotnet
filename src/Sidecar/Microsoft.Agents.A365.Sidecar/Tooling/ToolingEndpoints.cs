// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Sidecar.Auth;
using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Sidecar.Tooling;

/// <summary>
/// Extension methods for mapping Tooling API endpoints.
/// Uses V2 tooling with per-audience token acquisition via <see cref="SidecarMcpTokenProvider"/>.
/// </summary>
public static class ToolingEndpoints
{
    /// <summary>
    /// Maps the MCP Tooling discovery and invocation endpoints.
    /// </summary>
    public static WebApplication MapToolingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/tools").WithTags("Tooling");

        // GET /api/v1/tools/servers — List available MCP tool servers (V2 gateway)
        group.MapGet("/servers", async (
            IMcpToolServerConfigurationService toolService,
            SidecarTokenProvider tokenProvider,
            IOptions<SidecarOptions> options,
            ILogger<Program> logger) =>
        {
            var sidecarOpts = options.Value;
            var agentId = sidecarOpts.Agent.Id;

            logger.LogDebug("Listing tool servers for agent {AgentId} via V2 gateway", agentId);

            try
            {
                // Acquire gateway auth token using blueprint credentials
                var authToken = await tokenProvider.ResolveToolingTokenAsync(
                    sidecarOpts.Tooling.GatewayScope) ?? string.Empty;

                var servers = await toolService.ListToolServersAsync(agentId, authToken);
                var response = servers.Select(s => new ToolServerInfo
                {
                    Id = s.id,
                    Name = s.mcpServerName,
                    Url = s.url,
                    Publisher = s.publisher,
                    HasV2Auth = !string.IsNullOrEmpty(s.audience),
                    Audience = s.audience,
                }).ToList();
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to list tool servers");
                return Results.Problem("Failed to list tool servers", statusCode: 502);
            }
        }).WithName("ListToolServers");

        // POST /api/v1/tools/enumerate — Enumerate all tools from all servers (V2 per-audience tokens)
        group.MapPost("/enumerate", async (
            IMcpToolServerConfigurationService toolService,
            SidecarTokenProvider tokenProvider,
            SidecarMcpTokenProvider mcpTokenProvider,
            IOptions<SidecarOptions> options,
            ILogger<Program> logger) =>
        {
            var sidecarOpts = options.Value;
            var agentId = sidecarOpts.Agent.Id;

            logger.LogInformation("Enumerating all tools for agent {AgentId} via V2", agentId);

            try
            {
                var authToken = await tokenProvider.ResolveToolingTokenAsync(
                    sidecarOpts.Tooling.GatewayScope) ?? string.Empty;

                var (servers, toolsByServer) = await toolService.EnumerateToolsFromServersAsync(
                    agentId, authToken, mcpTokenProvider, turnContext: null!, new ToolOptions());

                var result = toolsByServer.Select(kvp => new
                {
                    serverName = kvp.Key,
                    tools = kvp.Value.Select(t => new
                    {
                        name = t.Name,
                        description = t.Description,
                    }),
                });

                return Results.Ok(new { servers = servers.Count, tools = result });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enumerate tools");
                return Results.Problem($"Failed to enumerate tools: {ex.Message}", statusCode: 502);
            }
        }).WithName("EnumerateTools");

        // GET /api/v1/tools — API documentation
        group.MapGet("/", (ILogger<Program> logger) =>
        {
            return Results.Ok(new
            {
                version = "v2",
                message = "Agent365 Sidecar Tooling API (V2 per-audience tokens)",
                endpoints = new
                {
                    servers = "GET /api/v1/tools/servers — List MCP tool servers",
                    enumerate = "POST /api/v1/tools/enumerate — Enumerate all tools with V2 auth",
                    invoke = "POST /api/v1/tools/servers/{serverId}/tools/{toolName}/invoke — Invoke a tool",
                },
            });
        }).WithName("ListAllTools");

        // POST /api/v1/tools/servers/{serverId}/tools/{toolName}/invoke — Invoke a tool
        group.MapPost("/servers/{serverId}/tools/{toolName}/invoke", async (
            string serverId,
            string toolName,
            ToolInvocationRequest body,
            IMcpToolServerConfigurationService toolService,
            SidecarTokenProvider tokenProvider,
            SidecarMcpTokenProvider mcpTokenProvider,
            IOptions<SidecarOptions> options,
            ILogger<Program> logger) =>
        {
            var sidecarOpts = options.Value;
            var agentId = sidecarOpts.Agent.Id;

            logger.LogInformation("Invoking tool {ToolName} on server {ServerId}", toolName, serverId);

            try
            {
                var authToken = await tokenProvider.ResolveToolingTokenAsync(
                    sidecarOpts.Tooling.GatewayScope) ?? string.Empty;

                // List servers to find target
                var servers = await toolService.ListToolServersAsync(agentId, authToken);
                var targetServer = servers.FirstOrDefault(s => s.id == serverId || s.mcpServerName == serverId);

                if (targetServer == null)
                {
                    return Results.NotFound(new { error = $"Server '{serverId}' not found" });
                }

                // Get per-server token for V2
                var serverToken = await mcpTokenProvider.GetTokenAsync(targetServer);

                // Get tools from the target server
                var tools = await toolService.GetMcpClientToolsAsync(
                    null!, targetServer, serverToken, new ToolOptions());

                var tool = tools.FirstOrDefault(t =>
                    string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));

                if (tool == null)
                {
                    return Results.NotFound(new { error = $"Tool '{toolName}' not found on server '{serverId}'" });
                }

                // Invoke the tool via MCP
                var arguments = body.Arguments.ToDictionary(
                    kvp => kvp.Key, kvp => (object?)kvp.Value);
                var callResult = await tool.CallAsync(arguments);
                return Results.Ok(new ToolInvocationResponse
                {
                    Result = callResult,
                    IsError = false,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to invoke tool {ToolName} on server {ServerId}", toolName, serverId);
                return Results.Ok(new ToolInvocationResponse
                {
                    IsError = true,
                    ErrorMessage = ex.Message,
                });
            }
        }).WithName("InvokeTool");

        return app;
    }
}

/// <summary>
/// Tool server information returned by the listing endpoint.
/// </summary>
public sealed class ToolServerInfo
{
    /// <summary>
    /// Server ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Server display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Server endpoint URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Publisher of the tool server.
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// Whether this server uses V2 per-audience authentication.
    /// </summary>
    public bool HasV2Auth { get; set; }

    /// <summary>
    /// The server's audience (V2 only).
    /// </summary>
    public string? Audience { get; set; }
}
