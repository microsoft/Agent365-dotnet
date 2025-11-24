// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Settings.Models
{
    /// <summary>
    /// Represents the settings for a specific agent instance.
    /// </summary>
    public class AgentSettings
    {
        /// <summary>
        /// Gets or sets the unique identifier of the settings.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the agent instance identifier these settings belong to.
        /// </summary>
        [JsonPropertyName("agentInstanceId")]
        public string AgentInstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the template identifier these settings are based on.
        /// </summary>
        [JsonPropertyName("templateId")]
        public string? TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the agent type.
        /// </summary>
        [JsonPropertyName("agentType")]
        public string AgentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the collection of setting properties and their values.
        /// </summary>
        [JsonPropertyName("properties")]
        public List<AgentSettingProperty> Properties { get; set; } = new List<AgentSettingProperty>();

        /// <summary>
        /// Gets or sets the date and time when these settings were created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the date and time when these settings were last modified.
        /// </summary>
        [JsonPropertyName("modifiedAt")]
        public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
