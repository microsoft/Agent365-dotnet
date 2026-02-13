// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Exceptions
{
    /// <summary>
    /// Exception thrown when a local MCP desktop client needs to register with the agent.
    /// This is thrown during discovery when no desktop client is registered.
    /// </summary>
    public class LocalMcpDesktopRegistrationRequiredException : Exception
    {
        /// <summary>
        /// Gets the client name that requires registration.
        /// </summary>
        public string ClientName { get; }

        /// <summary>
        /// Gets the protocol URL that the user can click to register their desktop.
        /// Format: locaproto:?action=register&amp;callback=https://agent.com/api/channels/register
        /// </summary>
        public string RegistrationProtocolUrl { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalMcpDesktopRegistrationRequiredException"/> class.
        /// </summary>
        /// <param name="clientName">The client name that requires registration.</param>
        /// <param name="registrationProtocolUrl">The protocol URL for registration.</param>
        /// <param name="message">The error message.</param>
        public LocalMcpDesktopRegistrationRequiredException(
            string clientName,
            string registrationProtocolUrl,
            string? message = null)
            : base(message ?? $"Desktop client '{clientName}' is not registered. Registration required to access local files.")
        {
            ClientName = clientName;
            RegistrationProtocolUrl = registrationProtocolUrl;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalMcpDesktopRegistrationRequiredException"/> class.
        /// </summary>
        /// <param name="clientName">The client name that requires registration.</param>
        /// <param name="registrationProtocolUrl">The protocol URL for registration.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public LocalMcpDesktopRegistrationRequiredException(
            string clientName,
            string registrationProtocolUrl,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            ClientName = clientName;
            RegistrationProtocolUrl = registrationProtocolUrl;
        }
    }
}
