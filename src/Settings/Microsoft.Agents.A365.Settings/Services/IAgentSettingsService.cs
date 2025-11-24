// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Settings.Models;

namespace Microsoft.Agents.A365.Settings.Services
{
    /// <summary>
    /// Provides services for managing agent settings templates and agent instance settings.
    /// </summary>
    public interface IAgentSettingsService
    {
        /// <summary>
        /// Gets the settings template for the specified agent type.
        /// </summary>
        /// <param name="agentType">The type of agent to get the template for.</param>
        /// <param name="authToken">The authentication token for accessing the API.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The settings template for the specified agent type, or null if not found.</returns>
        Task<AgentSettingsTemplate?> GetSettingsTemplateByAgentTypeAsync(
            string agentType,
            string authToken,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets or updates the settings template for the specified agent type.
        /// </summary>
        /// <param name="agentType">The type of agent to set the template for.</param>
        /// <param name="template">The settings template to set.</param>
        /// <param name="authToken">The authentication token for accessing the API.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The updated settings template.</returns>
        Task<AgentSettingsTemplate> SetSettingsTemplateByAgentTypeAsync(
            string agentType,
            AgentSettingsTemplate template,
            string authToken,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the settings for the specified agent instance.
        /// </summary>
        /// <param name="agentInstanceId">The unique identifier of the agent instance.</param>
        /// <param name="authToken">The authentication token for accessing the API.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The settings for the specified agent instance, or null if not found.</returns>
        Task<AgentSettings?> GetSettingsByAgentInstanceAsync(
            string agentInstanceId,
            string authToken,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets or updates the settings for the specified agent instance.
        /// </summary>
        /// <param name="agentInstanceId">The unique identifier of the agent instance.</param>
        /// <param name="settings">The settings to set.</param>
        /// <param name="authToken">The authentication token for accessing the API.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The updated settings.</returns>
        Task<AgentSettings> SetSettingsByAgentInstanceAsync(
            string agentInstanceId,
            AgentSettings settings,
            string authToken,
            CancellationToken cancellationToken = default);
    }
}
