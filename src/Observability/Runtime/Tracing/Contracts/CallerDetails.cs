// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------

using System.Net;

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
        /// Gets the UPN (User Principal Name) of the caller.
        /// </summary>
        public string CallerUpn { get; }

        /// <summary>
        /// Gets the client IP address of the caller.
        /// </summary>
        public IPAddress? CallerClientIP { get; }

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
        /// <param name="callerClientIP">The client IP address of the caller.</param>
        /// <param name="tenantId">The tenant ID of the caller.</param>
        public CallerDetails(
            string callerId,
            string callerName,
            string callerUpn,
            IPAddress? callerClientIP = null,
            string? tenantId = null)
        {
            CallerId = callerId;
            CallerName = callerName;
            CallerUpn = callerUpn;
            CallerClientIP = callerClientIP;
            TenantId = tenantId;
        }
    }
}
