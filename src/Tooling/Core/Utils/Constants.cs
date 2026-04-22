// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Utils
{
    /// <summary>
    /// Provides constant values used throughout the Tooling components.
    /// </summary>
    public class Constants
    {
        /// <summary>
        /// Provides constant header values used for authentication.
        /// </summary>
        public class Headers
        {
            /// <summary>
            /// The prefix used for Bearer authentication tokens in HTTP headers.
            /// </summary>
            public const string BearerPrefix = "Bearer";

            /// <summary>
            /// Header name for sending the agent identifier to MCP platform for logging/analytics.
            /// </summary>
            public const string AgentIdHeader = "x-ms-agentid";

            /// <summary>
            /// The HTTP Authorization header name.
            /// </summary>
            public const string Authorization = "Authorization";
        }

        /// <summary>
        /// Provides constants used for MCP server authentication and audience resolution.
        /// </summary>
        public class Authentication
        {
            /// <summary>
            /// The Application ID of the Agent Tooling Gateway (ATG).
            /// MCP servers that carry this audience — or no audience — are V1 servers
            /// and share a single ATG-scoped token.
            /// </summary>
            public const string AtgAppId = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1";
        }
    }
}
