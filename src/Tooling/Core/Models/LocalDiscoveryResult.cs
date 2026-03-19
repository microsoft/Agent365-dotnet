// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Models;

/// <summary>
/// Result of local MCP server discovery by user identity.
/// Contains the discovered servers and metadata about which desktop(s) were used.
/// </summary>
public class LocalDiscoveryResult
{
    /// <summary>
    /// Gets or sets the combined list of all discovered MCP servers (cloud + local).
    /// </summary>
    public List<MCPServerConfig> Servers { get; set; } = new();

    /// <summary>
    /// Gets or sets the user identifier (email/UPN) used for discovery.
    /// </summary>
    public string? UserIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the desktop client that was used for local server discovery.
    /// This is the most recently active desktop if multiple are registered.
    /// </summary>
    public DesktopClientInfo? ActiveDesktop { get; set; }

    /// <summary>
    /// Gets or sets all registered desktops for this user.
    /// Useful when the user has multiple desktops and may want to switch.
    /// </summary>
    public List<DesktopClientInfo> AllRegisteredDesktops { get; set; } = new();

    /// <summary>
    /// Gets or sets whether a desktop registration is required.
    /// True if no desktops are registered for this user.
    /// </summary>
    public bool RequiresRegistration { get; set; }

    /// <summary>
    /// Gets or sets any error message from the discovery process.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets whether local desktop tools (WNS transport) were discovered on this turn.
    /// This is true when at least one server uses WNS transport for local desktop access.
    /// Used for detecting tool set changes between turns.
    /// </summary>
    public bool HasLocalTools => Servers.Any(s => s.transportType == McpTransportType.Wns);

    /// <summary>
    /// Gets the names of all discovered servers, useful for tracking tool set changes between turns.
    /// </summary>
    public IReadOnlyList<string> ServerNames => Servers.Select(s => s.mcpServerName).ToList();
}

/// <summary>
/// Information about a registered desktop client.
/// </summary>
public class DesktopClientInfo
{
    /// <summary>
    /// Gets or sets the client name (identifier).
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the client was registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Gets or sets when the client was last seen/active.
    /// </summary>
    public DateTime LastSeen { get; set; }
}
