// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Models;

/// <summary>
/// Represents a registered WNS client (desktop machine).
/// </summary>
public class ClientRegistration
{
    /// <summary>
    /// Gets or sets the client name (identifier).
    /// This is typically the machine name or a user-chosen name.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user identifier (email/UPN) who owns this desktop registration.
    /// This allows the agent to query "which desktops does this user have registered?"
    /// </summary>
    public string? UserIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the WNS channel URI for push notifications.
    /// </summary>
    public string ChannelUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name of the desktop client.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD Device ID of the desktop client.
    /// This is a platform-verified device identity from dsregcmd on AAD-joined devices.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets when the client was registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Gets or sets when the client was last seen.
    /// </summary>
    public DateTime LastSeen { get; set; }

    /// <summary>
    /// Gets or sets the list of local MCP server IDs available on this desktop.
    /// These are provisioned during registration via the provision-agent-user flow.
    /// </summary>
    public List<string> ServerIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the Agent User ID provisioned on this desktop.
    /// This is the identity used by the agent to interact with local MCP servers.
    /// </summary>
    public string? AgentUserId { get; set; }
}

/// <summary>
/// Request model for registering a new WNS client.
/// </summary>
public class ChannelRegistrationRequest
{
    /// <summary>
    /// Gets or sets the client name (identifier).
    /// If not provided, MachineName will be used as the client name.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Gets or sets the user identifier (email/UPN) who owns this desktop.
    /// This allows the agent to find the user's registered desktops.
    /// </summary>
    public string? UserIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the WNS channel URI for push notifications.
    /// </summary>
    public string ChannelUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name of the desktop client.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD Device ID of the desktop client.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets when the client was registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Gets or sets the list of local MCP server IDs provisioned on this desktop.
    /// </summary>
    public List<string> ServerIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the Agent User ID provisioned on this desktop.
    /// </summary>
    public string? AgentUserId { get; set; }
}
