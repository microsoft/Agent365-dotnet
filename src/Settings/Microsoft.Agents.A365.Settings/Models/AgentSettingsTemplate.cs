// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Settings.Models
{
    /// <summary>
    /// Represents a settings template for a specific agent type.
    /// </summary>
    public class AgentSettingsTemplate
    {
        /// <summary>
        /// Gets or sets the unique identifier of the template.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the agent type this template applies to.
        /// </summary>
        [JsonPropertyName("agentType")]
        public string AgentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the template.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the template.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the version of the template.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the collection of setting properties defined in this template.
        /// </summary>
        [JsonPropertyName("properties")]
        public List<AgentSettingProperty> Properties { get; set; } = new List<AgentSettingProperty>();
    }
}
