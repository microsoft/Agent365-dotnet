// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Microsoft.Agents.A365.Runtime
{
    /// <summary>
    /// Default implementation of <see cref="IUserAgentConfiguration"/> for the Agent365 SDK.
    /// </summary>
    public class Agent365SdkUserAgentConfiguration : IUserAgentConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365SdkUserAgentConfiguration"/> class.
        /// </summary>
        /// <param name="orchestratorName">Optional orchestrator name to include in the User-Agent header.</param>
        public Agent365SdkUserAgentConfiguration(string? orchestratorName = null)
        {
            OrchestratorName = orchestratorName;
        }

        /// <inheritdoc/>
        public string ProductName => "Agent365SDK";

        /// <inheritdoc/>
        public string Version => Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";

        /// <inheritdoc/>
        public string? OrchestratorName { get; }
    }
}
