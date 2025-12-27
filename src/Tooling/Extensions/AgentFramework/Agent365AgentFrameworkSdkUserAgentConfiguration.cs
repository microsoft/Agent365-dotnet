// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Runtime;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework
{
    /// <summary>
    /// Agent Framework-specific implementation of <see cref="IUserAgentConfiguration"/>.
    /// This class provides a singleton instance optimized for the Agent Framework orchestrator.
    /// </summary>
    public sealed class Agent365AgentFrameworkSdkUserAgentConfiguration : Agent365SdkUserAgentConfiguration
    {
        private static readonly Lazy<Agent365AgentFrameworkSdkUserAgentConfiguration> _instance =
            new(() => new Agent365AgentFrameworkSdkUserAgentConfiguration());

        /// <summary>
        /// Gets the singleton instance of <see cref="Agent365AgentFrameworkSdkUserAgentConfiguration"/>.
        /// </summary>
        public static new Agent365AgentFrameworkSdkUserAgentConfiguration Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365AgentFrameworkSdkUserAgentConfiguration"/> class.
        /// </summary>
        private Agent365AgentFrameworkSdkUserAgentConfiguration() : base("AgentFramework")
        {
        }
    }
}
