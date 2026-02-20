// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Models;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Services;

/// <summary>
/// Interface for sending Windows Push Notification Service (WNS) notifications.
/// </summary>
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
    /// <param name="callbackUrl">The callback URL for the client to connect to.</param>
    /// <param name="serverId">Optional MCP server ID to include in the payload.</param>
    /// <param name="agentAppId">Optional Agent Application ID (Azure AD Client ID) of the calling agent.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> SendNotificationAsync(string channelUri, string callbackUrl, string? serverId = null, string? agentAppId = null);

    /// <summary>
    /// Sends a WNS notification for a cloud MCP server that requires desktop proxy.
    /// The desktop client will connect to the cloud server with Intune-managed credentials.
    /// </summary>
    /// <param name="channelUri">The WNS channel URI to send to.</param>
    /// <param name="callbackUrl">The callback URL for the client to connect to.</param>
    /// <param name="cloudConfig">Configuration for the cloud MCP server to proxy.</param>
    /// <param name="agentAppId">Optional Agent Application ID (Azure AD Client ID) of the calling agent.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> SendCloudMcpNotificationAsync(
        string channelUri, 
        string callbackUrl, 
        CloudMcpProxyConfig cloudConfig,
        string? agentAppId = null);

    /// <summary>
    /// Sends a WNS discovery notification to request the list of local MCP servers.
    /// </summary>
    /// <param name="channelUri">The WNS channel URI to send to.</param>
    /// <param name="requestId">The unique request ID for this discovery operation.</param>
    /// <param name="callbackUrl">The callback URL where the client should POST the server list.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> SendDiscoveryNotificationAsync(string channelUri, string requestId, string callbackUrl);

    /// <summary>
    /// Sends a WNS notification to check the Intune management status of the device.
    /// </summary>
    /// <param name="channelUri">The WNS channel URI to send to.</param>
    /// <param name="requestId">The unique request ID for this Intune check operation.</param>
    /// <param name="callbackUrl">The callback URL where the client should POST the Intune status.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> SendIntuneCheckNotificationAsync(string channelUri, string requestId, string callbackUrl);

    /// <summary>
    /// Gets a value indicating whether the WNS service is properly configured.
    /// </summary>
    bool IsConfigured { get; }
}
