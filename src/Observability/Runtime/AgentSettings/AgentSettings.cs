// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.AgentSettings
{
    /// <summary>
    /// Represents agent settings for a specific agent instance.
    /// </summary>
    public class AgentSettings
    {
        /// <summary>
        /// Gets or sets the agent instance identifier.
        /// </summary>
        public string AgentInstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the agent type identifier.
        /// </summary>
        public string AgentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the settings as key-value pairs.
        /// </summary>
        public Dictionary<string, object?> Settings { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// Gets or sets optional metadata for the settings.
        /// </summary>
        public Dictionary<string, object?>? Metadata { get; set; }
    }
}
