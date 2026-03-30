// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Composite caller details for agent-to-agent (A2A) scenarios.
    /// Groups the human caller identity and the calling agent identity together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Certification Requirements:</b> The <see cref="UserDetails"/> must be provided (not <c>null</c>) with at least
    /// <see cref="Contracts.UserDetails.UserId"/>, <see cref="Contracts.UserDetails.UserName"/>, and
    /// <see cref="Contracts.UserDetails.UserEmail"/> set to meet certification requirements.
    /// </para>
    /// <para>
    /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
    /// </para>
    /// </remarks>
    public sealed class CallerDetails
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CallerDetails"/> class.
        /// </summary>
        /// <param name="userDetails">Details about the human user in the call chain.</param>
        /// <param name="callerAgentDetails">Details about the calling agent in A2A scenarios.</param>
        public CallerDetails(
            UserDetails? userDetails = null,
            AgentDetails? callerAgentDetails = null)
        {
            UserDetails = userDetails;
            CallerAgentDetails = callerAgentDetails;
        }

        /// <summary>
        /// Gets the details about the human user in the call chain.
        /// </summary>
        public UserDetails? UserDetails { get; }

        /// <summary>
        /// Gets the details about the calling agent in A2A scenarios.
        /// </summary>
        public AgentDetails? CallerAgentDetails { get; }
    }
}
