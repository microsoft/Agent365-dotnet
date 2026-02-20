// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services;

/// <summary>
/// Result of policy enforcement check.
/// </summary>
public enum PolicyEnforcementAction
{
    /// <summary>
    /// Allow the tool call to proceed directly to cloud MCP.
    /// </summary>
    AllowDirect,

    /// <summary>
    /// Route the tool call through the local desktop (locaproto) for policy enforcement.
    /// </summary>
    RouteToDesktop,

    /// <summary>
    /// Block the tool call - user needs to register their desktop first.
    /// </summary>
    BlockRequiresRegistration,

    /// <summary>
    /// Block the tool call - policy denied access.
    /// </summary>
    BlockPolicyDenied
}

/// <summary>
/// Configuration for cloud MCP servers that require desktop proxy routing.
/// </summary>
public class CloudServerRegistration
{
    /// <summary>
    /// Gets or sets the server name/ID (e.g., "MailTools").
    /// </summary>
    public required string ServerName { get; set; }

    /// <summary>
    /// Gets or sets the cloud endpoint URL.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the transport type (e.g., "sse", "websocket").
    /// </summary>
    public string Transport { get; set; } = "sse";

    /// <summary>
    /// Gets or sets the OAuth scope for the server.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the audience for the access token.
    /// </summary>
    public string? Audience { get; set; }
}

/// <summary>
/// Result of evaluating policy enforcement for a tool call.
/// </summary>
public class PolicyEnforcementResult
{
    /// <summary>
    /// The action to take for this tool call.
    /// </summary>
    public PolicyEnforcementAction Action { get; set; }

    /// <summary>
    /// Error message when the action is Block*.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Protocol URL for desktop registration (when Action is BlockRequiresRegistration).
    /// </summary>
    public string? RegistrationProtocolUrl { get; set; }

    /// <summary>
    /// The client name of the registered desktop (when Action is RouteToDesktop).
    /// </summary>
    public string? DesktopClientName { get; set; }

    /// <summary>
    /// Base URL for routing through desktopproxy (when Action is RouteToDesktop).
    /// </summary>
    public string? DesktopProxyBaseUrl { get; set; }

    /// <summary>
    /// Cloud server configuration for desktop proxy routing.
    /// </summary>
    public CloudServerRegistration? CloudServerConfig { get; set; }
}

/// <summary>
/// Service for enforcing MCP tool call policies.
/// Used to implement Scenario 2: Force cloud MCP calls through local desktop for Intune policy enforcement.
/// </summary>
public interface IMcpPolicyEnforcementService
{
    /// <summary>
    /// Evaluates whether a tool call should be allowed, routed through desktop, or blocked.
    /// </summary>
    /// <param name="serverName">The MCP server name (e.g., "mcp_MailTools").</param>
    /// <param name="toolName">The tool being called (e.g., "SendEmail").</param>
    /// <param name="userIdentifier">The user's identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Policy enforcement result indicating what action to take.</returns>
    Task<PolicyEnforcementResult> EvaluatePolicyAsync(
        string serverName,
        string toolName,
        string userIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a server is registered for policy enforcement.
    /// </summary>
    /// <param name="serverName">The MCP server name.</param>
    /// <returns>True if the server is registered for policy enforcement.</returns>
    bool ServerRequiresDevicePath(string serverName);

    /// <summary>
    /// Registers a server for device path routing and policy enforcement.
    /// </summary>
    /// <param name="serverName">The MCP server name.</param>
    void RegisterDevicePathServer(string serverName);

    /// <summary>
    /// Registers a cloud server that requires device path routing with its configuration.
    /// This is used to pass cloud server details (endpoint, auth, etc.) to the desktop for proxying.
    /// </summary>
    /// <param name="serverName">The MCP server name (e.g., "MailTools").</param>
    /// <param name="cloudConfig">The cloud server configuration.</param>
    void RegisterDevicePathServer(string serverName, CloudServerRegistration cloudConfig);

    /// <summary>
    /// Gets the cloud server configuration for a registered device path server.
    /// </summary>
    /// <param name="serverName">The MCP server name.</param>
    /// <returns>The cloud server configuration, or null if not registered or not a cloud server.</returns>
    CloudServerRegistration? GetCloudServerConfig(string serverName);

    /// <summary>
    /// Invalidates the cached desktop registration status for a user.
    /// Should be called when a user registers or unregisters their desktop.
    /// </summary>
    /// <param name="userIdentifier">The user's identity.</param>
    void InvalidateUserCache(string userIdentifier);
}
