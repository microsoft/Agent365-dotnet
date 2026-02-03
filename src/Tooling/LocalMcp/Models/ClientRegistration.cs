// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Models;

/// <summary>
/// Represents a registered WNS client (desktop machine).
/// </summary>
public class ClientRegistration
{
    /// <summary>
    /// Gets or sets the client name (identifier).
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the WNS channel URI for push notifications.
    /// </summary>
    public string ChannelUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name of the desktop client.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the client was registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Gets or sets when the client was last seen.
    /// </summary>
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// Request model for registering a new WNS client.
/// </summary>
public class ChannelRegistrationRequest
{
    /// <summary>
    /// Gets or sets the client name (identifier).
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the WNS channel URI for push notifications.
    /// </summary>
    public string ChannelUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name of the desktop client.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the client was registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; }
}
