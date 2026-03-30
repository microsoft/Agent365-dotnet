// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Details about the human user that invoked an agent.
    /// </summary>
    public sealed class UserDetails : IEquatable<UserDetails>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserDetails"/> class.
        /// </summary>
        /// <param name="userId">The unique identifier for the user.</param>
        /// <param name="userEmail">The email address of the user.</param>
        /// <param name="userName">The human-readable name of the user.</param>
        /// <param name="userClientIP">The client IP address of the user.</param>
        public UserDetails(
            string? userId = null,
            string? userEmail = null,
            string? userName = null,
            IPAddress? userClientIP = null)
        {
            UserId = userId;
            UserEmail = userEmail;
            UserName = userName;
            UserClientIP = userClientIP;
        }

        /// <summary>
        /// Gets the unique identifier for the user.
        /// </summary>
        public string? UserId { get; }

        /// <summary>
        /// Gets the email address of the user.
        /// </summary>
        public string? UserEmail { get; }

        /// <summary>
        /// Gets the human-readable name of the user.
        /// </summary>
        public string? UserName { get; }

        /// <summary>
        /// Gets the client IP address of the user.
        /// </summary>
        public IPAddress? UserClientIP { get; }

        /// <inheritdoc/>
        public bool Equals(UserDetails? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(UserId, other.UserId, StringComparison.Ordinal) &&
                   string.Equals(UserEmail, other.UserEmail, StringComparison.Ordinal) &&
                   string.Equals(UserName, other.UserName, StringComparison.Ordinal) &&
                   Equals(UserClientIP, other.UserClientIP);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as UserDetails);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (UserId != null ? StringComparer.Ordinal.GetHashCode(UserId) : 0);
                hash = (hash * 31) + (UserEmail != null ? StringComparer.Ordinal.GetHashCode(UserEmail) : 0);
                hash = (hash * 31) + (UserName != null ? StringComparer.Ordinal.GetHashCode(UserName) : 0);
                hash = (hash * 31) + (UserClientIP?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
