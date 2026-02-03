// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Models;

/// <summary>
/// Represents the result of a local MCP server discovery request.
/// </summary>
public class DiscoveryResult
{
    /// <summary>
    /// Gets or sets the unique request ID for this discovery operation.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status of the discovery: "pending", "completed", or "error".
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Gets or sets the raw JSON response from the desktop client (odr mcp list output).
    /// </summary>
    public string RawResponse { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message if Status is "error".
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets when the result was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
