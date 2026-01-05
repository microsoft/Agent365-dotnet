// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.AgentSettings
{
    /// <summary>
    /// Represents an agent setting template for a specific agent type.
    /// </summary>
    public class AgentSettingTemplate
    {
        /// <summary>
        /// Gets or sets the agent type identifier.
        /// </summary>
        public string AgentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the settings as key-value pairs.
        /// </summary>
        public Dictionary<string, object?> Settings { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// Gets or sets optional metadata for the template.
        /// </summary>
        public Dictionary<string, object?>? Metadata { get; set; }
    }
}
