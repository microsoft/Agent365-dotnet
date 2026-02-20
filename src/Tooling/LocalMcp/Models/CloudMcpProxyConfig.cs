// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Models;

/// <summary>
/// Configuration for proxying cloud MCP server requests through a local desktop.
/// This enables Intune policy enforcement for cloud-based MCP servers by routing
/// requests through Intune-managed devices.
/// </summary>
public class CloudMcpProxyConfig
{
    /// <summary>
    /// Gets or sets the logical server identifier (e.g., "MailTools", "CalendarTools").
    /// This is used for logging and identifying the server, not for routing.
    /// </summary>
    public required string ServerId { get; set; }

    /// <summary>
    /// Gets or sets the cloud endpoint URL where the MCP server is hosted.
    /// This is the URL the desktop client will connect to after applying policy.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the transport type for connecting to the cloud server.
    /// Common values: "sse", "websocket", "http".
    /// </summary>
    public string Transport { get; set; } = "sse";

    /// <summary>
    /// Gets or sets the authentication type the desktop should use.
    /// Supported values:
    /// - "intune_managed": Use Intune-managed device credentials
    /// - "user_delegated": Use the signed-in user's credentials
    /// - "bearer_token": Use the provided bearer token
    /// </summary>
    public string AuthType { get; set; } = "intune_managed";

    /// <summary>
    /// Gets or sets the OAuth scope required to access the cloud MCP server.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the audience claim for the access token.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets an optional bearer token to forward to the cloud server.
    /// Only used when AuthType is "bearer_token".
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Gets or sets additional headers to include when connecting to the cloud server.
    /// Keys are header names, values are header values.
    /// </summary>
    public Dictionary<string, string>? AdditionalHeaders { get; set; }
}

/// <summary>
/// WNS notification payload for MCP server requests.
/// Supports both local and cloud MCP servers with extensible configuration.
/// </summary>
public class McpNotificationPayload
{
    /// <summary>
    /// Gets or sets the WebSocket callback URL for the desktop to connect to.
    /// </summary>
    public required string Callback { get; set; }

    /// <summary>
    /// Gets or sets the server identifier.
    /// For local servers: full server ID (e.g., "MicrosoftWindows.Client.Core_cw5n1h2txyewy_...").
    /// For cloud servers: logical ID (e.g., "MailTools").
    /// </summary>
    public required string ServerId { get; set; }

    /// <summary>
    /// Gets or sets the server type. Determines how the desktop client handles the request.
    /// </summary>
    public McpServerType ServerType { get; set; } = McpServerType.Local;

    /// <summary>
    /// Gets or sets the cloud server configuration.
    /// Only present when ServerType is Cloud.
    /// </summary>
    public CloudMcpProxyConfig? CloudConfig { get; set; }

    /// <summary>
    /// Gets or sets the notification timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Specifies the type of MCP server for routing purposes.
/// </summary>
public enum McpServerType
{
    /// <summary>
    /// Local MCP server running on the desktop (e.g., file-mcp-server).
    /// Desktop connects directly to the local server process.
    /// </summary>
    Local,

    /// <summary>
    /// Cloud MCP server that requires desktop proxy for policy enforcement.
    /// Desktop connects to the cloud endpoint with Intune-managed credentials.
    /// </summary>
    Cloud
}
