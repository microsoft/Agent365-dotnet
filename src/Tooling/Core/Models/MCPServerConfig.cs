// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Microsoft.Agents.A365.Tooling.Models
{
    /// <summary>
    /// Represents the configuration for an MCP server, including its name, endpoint, and additional metadata.
    /// </summary>
    public class MCPServerConfig
    {
        /// <summary>
        /// Gets or sets the name of the MCP server.
        /// </summary>
        public required string mcpServerName { get; set; }

        /// <summary>
        /// Gets or sets the id of the MCP server.
        /// </summary>
        public required string id { get; set; }

        /// <summary>
        /// Gets or sets the url of the MCP server.
        /// </summary>
        public required string url { get; set; }

        /// <summary>
        /// Gets or sets the scope of the MCP server.
        /// </summary>
        public required string scope { get; set; }

        /// <summary>
        /// Gets or sets the audience of the MCP server.
        /// </summary>
        public required string audience { get; set; }

        /// <summary>
        /// Gets or sets the publisher of the MCP server.
        /// </summary>
        public required string publisher { get; set; }

        /// <summary>
        /// Gets or sets the transport type for this MCP server.
        /// Defaults to SSE for backward compatibility with cloud-based servers.
        /// </summary>
        public McpTransportType transportType { get; set; } = McpTransportType.Sse;

        /// <summary>
        /// Gets or sets WNS-specific configuration for local desktop MCP servers.
        /// Only applicable when <see cref="transportType"/> is <see cref="McpTransportType.Wns"/>.
        /// </summary>
        public WnsTransportConfig? wnsConfig { get; set; }

        /// <summary>
        /// Gets or sets the static tools list from the discovery response.
        /// When available, this allows skipping the tools/list MCP call.
        /// This is a JSON array of tool definitions from odr mcp list's static_responses.
        /// </summary>
        public JsonElement? staticToolsList { get; set; }

        /// <summary>
        /// Gets whether this server has pre-cached static tool definitions.
        /// </summary>
        public bool HasStaticTools => staticToolsList != null && staticToolsList.Value.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// Configuration for WNS (Windows Push Notification Service) transport.
    /// Used for MCP servers running on local Windows desktops.
    /// </summary>
    public class WnsTransportConfig
    {
        /// <summary>
        /// Gets or sets the client name used to identify the desktop client for WNS notifications.
        /// </summary>
        public required string clientName { get; set; }

        /// <summary>
        /// Gets or sets the WNS channel URI for sending push notifications.
        /// This is obtained from the desktop client during registration.
        /// </summary>
        public string? channelUri { get; set; }

        /// <summary>
        /// Gets or sets the base URL for the agent's WNS proxy endpoints.
        /// For example: "https://myagent.azurewebsites.net"
        /// </summary>
        public string? proxyBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the connection timeout in seconds for waiting for the desktop client to connect.
        /// Defaults to 30 seconds.
        /// </summary>
        public int connectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the MCP server ID on the local Windows desktop.
        /// This is used to identify which local MCP server to connect to.
        /// </summary>
        public string? localServerId { get; set; }

        /// <summary>
        /// Gets or sets the Agent Application ID (Azure AD Client ID) of the calling agent.
        /// This is sent to the desktop client so it can identify and log which agent
        /// is requesting access to local MCP servers.
        /// </summary>
        public string? agentAppId { get; set; }
    }
}
