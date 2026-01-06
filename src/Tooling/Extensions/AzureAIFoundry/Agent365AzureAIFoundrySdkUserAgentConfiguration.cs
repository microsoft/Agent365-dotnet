// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Runtime;

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry
{
    /// <summary>
    /// Azure AI Foundry-specific implementation of <see cref="IUserAgentConfiguration"/>.
    /// This class provides a singleton instance optimized for the Azure AI Foundry orchestrator.
    /// </summary>
    public sealed class Agent365AzureAIFoundrySdkUserAgentConfiguration : Agent365SdkUserAgentConfiguration
    {
        private static readonly Lazy<Agent365AzureAIFoundrySdkUserAgentConfiguration> _instance =
            new(() => new Agent365AzureAIFoundrySdkUserAgentConfiguration());

        /// <summary>
        /// Gets the singleton instance of <see cref="Agent365AzureAIFoundrySdkUserAgentConfiguration"/>.
        /// This property intentionally uses <c>new</c> to hide the base <c>Instance</c> property
        /// so that callers receive this more specific configuration type for Azure AI Foundry.
        /// </summary>
        public static new Agent365AzureAIFoundrySdkUserAgentConfiguration Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365AzureAIFoundrySdkUserAgentConfiguration"/> class.
        /// </summary>
        private Agent365AzureAIFoundrySdkUserAgentConfiguration() : base("AzureAIFoundry")
        {
        }
    }
}
