// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling
{
    /// <summary>
    /// Enumeration of tools operation modes.
    /// </summary>
    public enum ToolsMode
    {
        /// <summary>
        /// Use a mock MCP server for testing purposes.
        /// </summary>
        MockMCPServer,
        
        /// <summary>
        /// Use the MCP platform for production scenarios.
        /// </summary>
        MCPPlatform
    }
}