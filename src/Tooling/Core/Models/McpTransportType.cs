// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Models
{
    /// <summary>
    /// Specifies the transport mechanism used to communicate with an MCP server.
    /// </summary>
    public enum McpTransportType
    {
        /// <summary>
        /// Server-Sent Events (SSE) transport over HTTP/HTTPS.
        /// This is the default transport for cloud-based MCP servers.
        /// </summary>
        Sse = 0,

        /// <summary>
        /// WebSocket transport for bidirectional communication.
        /// Used for local MCP servers that require real-time bidirectional messaging.
        /// </summary>
        WebSocket = 1,

        /// <summary>
        /// Windows Push Notification Service (WNS) transport.
        /// Used for local desktop MCP servers that need to be woken up via push notifications
        /// and then communicate over a WebSocket callback channel.
        /// </summary>
        Wns = 2,

        /// <summary>
        /// Custom transport provided by the client.
        /// Allows third-party implementations of MCP transport.
        /// </summary>
        Custom = 99
    }
}
