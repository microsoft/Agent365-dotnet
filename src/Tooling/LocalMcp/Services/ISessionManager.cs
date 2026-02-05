// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Models;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Services;

/// <summary>
/// Interface for managing MCP sessions and client registrations.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to provide custom storage for Local MCP session management.
/// The default implementation (<see cref="InMemorySessionManager"/>) uses in-memory storage
/// which is suitable for development but not production.
/// </para>
/// <para>
/// <strong>Storage Requirements:</strong>
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Data Type</term>
///     <description>Persistence Requirements</description>
///   </listheader>
///   <item>
///     <term>ClientRegistration</term>
///     <description>
///       <strong>Must be persistent.</strong> Survives app restarts. Should have TTL of ~30 days
///       to match WNS channel expiration. Shared across all instances.
///     </description>
///   </item>
///   <item>
///     <term>McpSession</term>
///     <description>
///       <strong>Can be in-memory.</strong> Contains WebSocket which cannot be serialized.
///       Each session is tied to a specific server instance. Lost on restart is acceptable.
///     </description>
///   </item>
///   <item>
///     <term>DiscoveryResult</term>
///     <description>
///       <strong>Short-lived, can be persistent.</strong> Should have TTL of ~5 minutes.
///       If using distributed storage, allows discovery polling across instances.
///     </description>
///   </item>
/// </list>
/// <para>
/// <strong>Example Implementations:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Azure Cosmos DB - Multi-region, serverless pricing</description></item>
///   <item><description>Azure Table Storage - Simple, very cheap</description></item>
///   <item><description>Redis - Fast, good for caching</description></item>
///   <item><description>SQL Server - Enterprise, transactional</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// public class CosmosDbSessionManager : ISessionManager
/// {
///     private readonly Container _clientsContainer;
///     private readonly ConcurrentDictionary&lt;string, McpSession&gt; _sessions = new(); // In-memory for WebSocket
///     
///     public ClientRegistration RegisterClient(ChannelRegistrationRequest request)
///     {
///         var registration = new ClientRegistration { ... };
///         _clientsContainer.UpsertItemAsync(registration).GetAwaiter().GetResult();
///         return registration;
///     }
///     
///     // Sessions stay in-memory (WebSocket cannot be serialized)
///     public McpSession CreateSession(string sessionId)
///     {
///         var session = new McpSession { SessionId = sessionId };
///         _sessions.TryAdd(sessionId, session);
///         return session;
///     }
/// }
/// </code>
/// </example>
public interface ISessionManager
{
    /// <summary>
    /// Registers a new client or updates an existing registration.
    /// </summary>
    /// <remarks>
    /// This should be stored persistently to survive app restarts.
    /// Consider a TTL of ~30 days to match WNS channel expiration.
    /// </remarks>
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
    /// Gets all registered clients.
    /// </summary>
    /// <returns>All client registrations.</returns>
    IEnumerable<ClientRegistration> GetAllClients();

    /// <summary>
    /// Creates a new MCP session.
    /// </summary>
    /// <remarks>
    /// Sessions can be stored in-memory since they contain WebSocket objects
    /// which cannot be serialized. Sessions are tied to the server instance
    /// that created them.
    /// </remarks>
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
    /// <remarks>
    /// Discovery results are short-lived and should have a TTL of ~5 minutes.
    /// They can be stored in distributed storage to allow polling across instances.
    /// </remarks>
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
}
