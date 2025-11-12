// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents details about the caller.
    /// </summary>
    public class CallerDetails
    {
        /// <summary>
        /// Gets the unique identifier of the caller.
        /// </summary>
        public string CallerId { get; }

        /// <summary>
        /// Gets the display name of the caller.
        /// </summary>
        public string CallerName { get; }

        /// <summary>
        /// Gets the user ID of the caller.
        /// </summary>
        public string? CallerUserId { get; }

        /// <summary>
        /// Gets the UPN (User Principal Name) of the caller.
        /// </summary>
        public string CallerUpn { get; }

        /// <summary>
        /// Gets the tenant ID of the caller.
        /// </summary>
        public string? TenantId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallerDetails"/> class.
        /// </summary>
        /// <param name="callerId">The unique identifier of the caller.</param>
        /// <param name="callerName">The display name of the caller.</param>
        /// <param name="callerUpn">The UPN (User Principal Name) of the caller.</param>
        /// <param name="callerUserId">The user ID of the caller.</param>
        /// <param name="tenantId">The tenant ID of the caller.</param>
        public CallerDetails(
            string callerId,
            string callerName,
            string callerUpn,
            string? callerUserId = null,
            string? tenantId = null)
        {
            CallerId = callerId;
            CallerName = callerName;
            CallerUpn = callerUpn;
            CallerUserId = callerUserId;
            TenantId = tenantId;
        }
    }
}
