// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Runtime;

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel
{
    /// <summary>
    /// Semantic Kernel-specific implementation of <see cref="IUserAgentConfiguration"/>.
    /// This class provides a singleton instance optimized for the Semantic Kernel orchestrator.
    /// </summary>
    public sealed class Agent365SemanticKernelSdkUserAgentConfiguration : Agent365SdkUserAgentConfiguration
    {
        private static readonly Lazy<Agent365SemanticKernelSdkUserAgentConfiguration> _instance =
            new(() => new Agent365SemanticKernelSdkUserAgentConfiguration());

        /// <summary>
        /// Gets the singleton instance of <see cref="Agent365SemanticKernelSdkUserAgentConfiguration"/>.
        /// This property intentionally uses the <c>new</c> keyword to hide the base <c>Instance</c>
        /// property and expose a more specific strongly-typed singleton for this derived configuration.
        /// </summary>
        public static new Agent365SemanticKernelSdkUserAgentConfiguration Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365SemanticKernelSdkUserAgentConfiguration"/> class.
        /// </summary>
        private Agent365SemanticKernelSdkUserAgentConfiguration() : base("SemanticKernel")
        {
        }
    }
}
