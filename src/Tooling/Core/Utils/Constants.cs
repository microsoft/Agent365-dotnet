// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Utils
{
    /// <summary>
    /// Provides constant values used throughout the Tooling components.
    /// </summary>
    public class Constants
    {
        /// <summary>
        /// Provides constant header values used for authentication and environment identification.
        /// </summary>
        public class Headers
        {
            /// <summary>
            /// The prefix used for Bearer authentication tokens in HTTP headers.
            /// </summary>
            public const string BearerPrefix = "Bearer";

            /// <summary>
            /// The header name used to specify the environment identifier in HTTP requests.
            /// </summary>
            public const string EnvironmentId = "x-ms-environment-id";
        }
    }
}
