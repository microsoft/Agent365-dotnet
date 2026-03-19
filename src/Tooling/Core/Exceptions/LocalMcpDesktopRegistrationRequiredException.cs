// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Exceptions
{
    /// <summary>
    /// Exception thrown when a local MCP desktop client needs to register with the agent.
    /// This is thrown during discovery when no desktop client is registered.
    /// The user should open the LocaProto desktop app and sign in with their Microsoft account.
    /// </summary>
    public class LocalMcpDesktopRegistrationRequiredException : Exception
    {
        /// <summary>
        /// Gets the client name that requires registration.
        /// </summary>
        public string ClientName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalMcpDesktopRegistrationRequiredException"/> class.
        /// </summary>
        /// <param name="clientName">The client name that requires registration.</param>
        /// <param name="message">The error message.</param>
        public LocalMcpDesktopRegistrationRequiredException(
            string clientName,
            string? message = null)
            : base(message ?? $"Desktop client '{clientName}' is not registered. Please open the LocaProto app on your Windows device and sign in with your Microsoft account.")
        {
            ClientName = clientName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalMcpDesktopRegistrationRequiredException"/> class.
        /// </summary>
        /// <param name="clientName">The client name that requires registration.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public LocalMcpDesktopRegistrationRequiredException(
            string clientName,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            ClientName = clientName;
        }
    }
}
