// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Settings.Models
{
    /// <summary>
    /// Represents a single setting property for an agent.
    /// </summary>
    public class AgentSettingProperty
    {
        /// <summary>
        /// Gets or sets the name of the setting.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value of the setting.
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the setting value.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        /// <summary>
        /// Gets or sets whether the setting is required.
        /// </summary>
        [JsonPropertyName("required")]
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the description of the setting.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the default value of the setting.
        /// </summary>
        [JsonPropertyName("defaultValue")]
        public string? DefaultValue { get; set; }
    }
}
