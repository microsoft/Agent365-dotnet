// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Models;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Services;

/// <summary>
/// Interface for sending Windows Push Notification Service (WNS) notifications.
/// </summary>
/// <remarks>
/// Security: WNS notification payloads do NOT contain callback URLs. Instead, they contain
/// the agent host, session ID, and a session token. The desktop client constructs the callback
/// URL from its registered agent base URL combined with the session ID, and presents the session
/// token when connecting. This prevents URL injection/replacement attacks.
/// </remarks>
public interface IWnsNotificationService
{
    /// <summary>
    /// Gets an access token for WNS.
    /// </summary>
    /// <returns>The access token.</returns>
    Task<string> GetAccessTokenAsync();

    /// <summary>
    /// Sends a WNS raw notification to trigger an MCP connection.
    /// </summary>
    /// <param name="channelUri">The WNS channel URI to send to.</param>
    /// <param name="agentHost">The agent host name (e.g., "agent.azurewebsites.net"). The desktop uses this to identify which registered agent to connect to.</param>
    /// <param name="sessionId">The unique session ID for the WebSocket connection.</param>
    /// <param name="sessionToken">The session token the desktop must present when connecting to the WebSocket.</param>
    /// <param name="serverId">Optional MCP server ID to include in the payload.</param>
    /// <param name="agentAppId">Optional Agent Application ID (Azure AD Client ID) of the calling agent.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> SendNotificationAsync(
        string channelUri,
        string agentHost,
        string sessionId,
        string sessionToken,
        string? serverId = null,
        string? agentAppId = null);

    /// <summary>
    /// Sends a WNS notification for a cloud MCP server that requires desktop proxy.
    /// The desktop client will connect to the cloud server with Intune-managed credentials.
    /// </summary>
    /// <param name="channelUri">The WNS channel URI to send to.</param>
    /// <param name="agentHost">The agent host name. The desktop uses this to identify which registered agent to connect to.</param>
    /// <param name="sessionId">The unique session ID for the WebSocket connection.</param>
    /// <param name="sessionToken">The session token the desktop must present when connecting to the WebSocket.</param>
    /// <param name="cloudConfig">Configuration for the cloud MCP server to proxy.</param>
    /// <param name="agentAppId">Optional Agent Application ID (Azure AD Client ID) of the calling agent.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> SendCloudMcpNotificationAsync(
        string channelUri,
        string agentHost,
        string sessionId,
        string sessionToken,
        CloudMcpProxyConfig cloudConfig,
        string? agentAppId = null);

    /// <summary>
    /// Sends a WNS discovery notification to request the list of local MCP servers.
    /// </summary>
    /// <param name="channelUri">The WNS channel URI to send to.</param>
    /// <param name="agentHost">The agent host name. The desktop uses this to identify which registered agent to send results to.</param>
    /// <param name="requestId">The unique request ID for this discovery operation.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> SendDiscoveryNotificationAsync(
        string channelUri,
        string agentHost,
        string requestId);

    /// <summary>
    /// Sends a WNS notification to check the Intune management status of the device.
    /// </summary>
    /// <param name="channelUri">The WNS channel URI to send to.</param>
    /// <param name="agentHost">The agent host name. The desktop uses this to identify which registered agent to send results to.</param>
    /// <param name="requestId">The unique request ID for this Intune check operation.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> SendIntuneCheckNotificationAsync(
        string channelUri,
        string agentHost,
        string requestId);

    /// <summary>
    /// Gets a value indicating whether the WNS service is properly configured.
    /// </summary>
    bool IsConfigured { get; }
}
