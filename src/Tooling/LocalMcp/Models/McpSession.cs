// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Models;

/// <summary>
/// Represents an MCP session with WebSocket connection tracking.
/// </summary>
public class McpSession
{
    /// <summary>
    /// Gets or sets the unique session identifier.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the WebSocket connection for this session.
    /// </summary>
    public WebSocket? WebSocket { get; set; }

    /// <summary>
    /// Gets or sets when the session was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last activity time for this session.
    /// </summary>
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets a value indicating whether the WebSocket is connected.
    /// </summary>
    public bool IsConnected => WebSocket?.State == WebSocketState.Open;

    /// <summary>
    /// Gets the pending requests awaiting responses, keyed by request ID.
    /// </summary>
    public ConcurrentDictionary<int, TaskCompletionSource<string>> PendingRequests { get; } = new();

    /// <summary>
    /// Updates the last activity time to now.
    /// </summary>
    public void UpdateActivity()
    {
        LastActivity = DateTime.UtcNow;
    }
}
