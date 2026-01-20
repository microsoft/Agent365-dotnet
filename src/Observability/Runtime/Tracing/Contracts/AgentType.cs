// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Supported agent types for generative AI.
    /// </summary>
    public enum AgentType
    {
        /// <summary>
        /// Entra embodied agent.
        /// </summary>
        EntraEmbodied,

        /// <summary>
        /// Entra non-embodied agent.
        /// </summary>
        EntraNonEmbodied,

        /// <summary>
        /// Microsoft Copilot agent.
        /// </summary>
        MicrosoftCopilot,

        /// <summary>
        /// Declarative agent.
        /// </summary>
        DeclarativeAgent,

        /// <summary>
        /// Foundry agent.
        /// </summary>
        Foundry,
        
        /// <summary>
        /// Copilot Studio agent.
        /// </summary>
        CopilotStudio
    }
}
