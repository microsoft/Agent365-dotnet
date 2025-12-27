// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Microsoft.Agents.A365.Runtime
{
    /// <summary>
    /// Default implementation of <see cref="IUserAgentConfiguration"/> for the Agent365 SDK.
    /// For orchestrator-specific configurations, use derived classes such as
    /// <c>Agent365AgentFrameworkSdkUserAgentConfiguration</c>,
    /// <c>Agent365SemanticKernelSdkUserAgentConfiguration</c>, or
    /// <c>Agent365AzureAIFoundrySdkUserAgentConfiguration</c>.
    /// </summary>
    public class Agent365SdkUserAgentConfiguration : IUserAgentConfiguration
    {
        private static readonly Lazy<Agent365SdkUserAgentConfiguration> _instance = new(() => new Agent365SdkUserAgentConfiguration());

        private static readonly Lazy<string> _version = new(() =>
            typeof(Agent365SdkUserAgentConfiguration)
                .Assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown");

        /// <summary>
        /// Gets the singleton instance of the default <see cref="Agent365SdkUserAgentConfiguration"/>.
        /// </summary>
        public static Agent365SdkUserAgentConfiguration Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365SdkUserAgentConfiguration"/> class.
        /// This constructor is protected to ensure instances are created through derived classes.
        /// </summary>
        /// <param name="orchestratorName">Optional orchestrator name to include in the User-Agent header.</param>
        protected Agent365SdkUserAgentConfiguration(string? orchestratorName = null)
        {
            OrchestratorName = orchestratorName;
        }

        /// <inheritdoc/>
        public string ProductName => "Agent365SDK";

        /// <inheritdoc/>
        public string Version => _version.Value;

        /// <inheritdoc/>
        public virtual string? OrchestratorName { get; }
    }
}
