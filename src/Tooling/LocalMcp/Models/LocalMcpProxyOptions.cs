// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Models;

/// <summary>
/// Configuration options for the Local MCP Proxy.
/// </summary>
public class LocalMcpProxyOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "LocalMcpProxy";

    /// <summary>
    /// Gets or sets the idle timeout in seconds for MCP sessions.
    /// Sessions idle longer than this will be cleaned up.
    /// Default is 120 seconds.
    /// </summary>
    public int IdleTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the timeout for pending sessions (not yet connected) in minutes.
    /// Default is 5 minutes.
    /// </summary>
    public int PendingSessionTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the cleanup interval in seconds.
    /// Default is 10 seconds.
    /// </summary>
    public int CleanupIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the MCP request timeout in seconds.
    /// Default is 120 seconds.
    /// </summary>
    public int McpRequestTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the default MCP server ID if none is specified.
    /// </summary>
    public string DefaultServerId { get; set; } = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server";

    /// <summary>
    /// Gets or sets the base URL for the LocalMcp proxy. This is used for generating callback URLs.
    /// If not set, the proxy will use the request's Host header to determine the URL.
    /// </summary>
    public string? BaseUrl { get; set; }
}
