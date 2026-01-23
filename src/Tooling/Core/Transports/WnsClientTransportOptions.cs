// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Transports
{
    /// <summary>
    /// Options for configuring the WNS client transport.
    /// </summary>
    public class WnsClientTransportOptions
    {
        /// <summary>
        /// Gets or sets the name of the WNS client to connect to.
        /// This should match the client name used during device registration.
        /// </summary>
        public required string ClientName { get; set; }

        /// <summary>
        /// Gets or sets the base URL of the WNS proxy service.
        /// This is the endpoint that handles WNS notifications and WebSocket connections.
        /// Example: "https://myagent.azurewebsites.net"
        /// </summary>
        public required string ProxyBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the desktop client to connect.
        /// Default is 30 seconds.
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the local MCP server ID on the desktop.
        /// This is passed to the desktop client so it knows which MCP server to activate.
        /// Example: "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server"
        /// </summary>
        public string? LocalServerId { get; set; }
    }
}
