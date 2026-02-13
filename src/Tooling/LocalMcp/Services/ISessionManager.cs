// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Models;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Services;

/// <summary>
/// Interface for managing MCP sessions and client registrations.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Registers a new client or updates an existing registration.
    /// </summary>
    /// <param name="request">The registration request.</param>
    /// <returns>The client registration.</returns>
    ClientRegistration RegisterClient(ChannelRegistrationRequest request);

    /// <summary>
    /// Gets a registered client by name.
    /// </summary>
    /// <param name="clientName">The client name.</param>
    /// <returns>The client registration, or null if not found.</returns>
    ClientRegistration? GetClient(string clientName);

    /// <summary>
    /// Gets all registered clients for a specific user.
    /// </summary>
    /// <param name="userIdentifier">The user identifier (email/UPN).</param>
    /// <returns>All client registrations for this user.</returns>
    IEnumerable<ClientRegistration> GetClientsByUser(string userIdentifier);

    /// <summary>
    /// Gets all registered clients.
    /// </summary>
    /// <returns>All client registrations.</returns>
    IEnumerable<ClientRegistration> GetAllClients();

    /// <summary>
    /// Creates a new MCP session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>The new session.</returns>
    McpSession CreateSession(string sessionId);

    /// <summary>
    /// Gets an MCP session by ID.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>The session, or null if not found.</returns>
    McpSession? GetSession(string sessionId);

    /// <summary>
    /// Removes an MCP session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>True if removed, false if not found.</returns>
    bool RemoveSession(string sessionId);

    /// <summary>
    /// Gets all active sessions.
    /// </summary>
    /// <returns>All sessions.</returns>
    IEnumerable<McpSession> GetAllSessions();

    /// <summary>
    /// Stores a discovery result.
    /// </summary>
    /// <param name="result">The discovery result to store.</param>
    void StoreDiscoveryResult(DiscoveryResult result);

    /// <summary>
    /// Gets a discovery result by request ID.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <returns>The discovery result, or null if not found.</returns>
    DiscoveryResult? GetDiscoveryResult(string requestId);

    /// <summary>
    /// Creates a pending discovery result entry.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <returns>The pending discovery result.</returns>
    DiscoveryResult CreatePendingDiscoveryResult(string requestId);

    /// <summary>
    /// Creates a pending Intune status result entry.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <returns>The pending Intune status result.</returns>
    IntuneStatusResult CreatePendingIntuneStatusResult(string requestId);

    /// <summary>
    /// Stores an Intune status result.
    /// </summary>
    /// <param name="result">The Intune status result to store.</param>
    void StoreIntuneStatusResult(IntuneStatusResult result);

    /// <summary>
    /// Gets an Intune status result by request ID.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <returns>The Intune status result, or null if not found.</returns>
    IntuneStatusResult? GetIntuneStatusResult(string requestId);
}
