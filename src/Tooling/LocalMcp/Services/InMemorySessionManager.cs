// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Web;
using Microsoft.Agents.A365.Tooling.LocalMcp.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Services;

/// <summary>
/// In-memory implementation of <see cref="ISessionManager"/>.
/// Suitable for single-instance deployments. For horizontal scaling,
/// use a distributed implementation (Redis, Cosmos DB, etc.).
/// </summary>
public class InMemorySessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, McpSession> _sessions = new();
    private readonly ConcurrentDictionary<string, ClientRegistration> _clients = new();
    private readonly ConcurrentDictionary<string, DiscoveryResult> _discoveryResults = new();
    private readonly ConcurrentDictionary<string, IntuneStatusResult> _intuneStatusResults = new();
    private readonly ILogger<InMemorySessionManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySessionManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public InMemorySessionManager(ILogger<InMemorySessionManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ClientRegistration RegisterClient(ChannelRegistrationRequest request)
    {
        // Use MachineName as ClientName if not provided
        var clientName = !string.IsNullOrWhiteSpace(request.ClientName) 
            ? request.ClientName 
            : request.MachineName;
        
        // URL decode the user identifier to normalize it (e.g., "Bug+Bash+User+11" -> "Bug Bash User 11")
        var userIdentifier = !string.IsNullOrWhiteSpace(request.UserIdentifier)
            ? HttpUtility.UrlDecode(request.UserIdentifier)
            : null;
            
        var registration = new ClientRegistration
        {
            ClientName = clientName,
            UserIdentifier = userIdentifier,
            ChannelUri = request.ChannelUri,
            MachineName = request.MachineName,
            RegisteredAt = request.RegisteredAt,
            LastSeen = DateTime.UtcNow,
            ServerIds = request.ServerIds ?? new(),
            AgentUserId = request.AgentUserId
        };

        _clients.AddOrUpdate(clientName, registration, (key, old) => registration);

        _logger.LogInformation("[SESSION MANAGER] Client '{ClientName}' registered from {MachineName} for user '{UserIdentifier}'",
            clientName, request.MachineName, userIdentifier ?? "unknown");

        return registration;
    }

    /// <inheritdoc />
    public ClientRegistration? GetClient(string clientName)
    {
        _clients.TryGetValue(clientName, out var client);
        return client;
    }

    /// <inheritdoc />
    public IEnumerable<ClientRegistration> GetClientsByUser(string userIdentifier)
    {
        // Case-insensitive comparison for email addresses
        return _clients.Values
            .Where(c => string.Equals(c.UserIdentifier, userIdentifier, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc />
    public IEnumerable<ClientRegistration> GetAllClients()
    {
        return _clients.Values;
    }

    /// <inheritdoc />
    public McpSession CreateSession(string sessionId)
    {
        var session = new McpSession { SessionId = sessionId };
        _sessions.TryAdd(sessionId, session);

        _logger.LogDebug("[SESSION MANAGER] Session '{SessionId}' created", sessionId);

        return session;
    }

    /// <inheritdoc />
    public McpSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <inheritdoc />
    public bool RemoveSession(string sessionId)
    {
        var removed = _sessions.TryRemove(sessionId, out _);

        if (removed)
        {
            _logger.LogDebug("[SESSION MANAGER] Session '{SessionId}' removed", sessionId);
        }

        return removed;
    }

    /// <inheritdoc />
    public IEnumerable<McpSession> GetAllSessions()
    {
        return _sessions.Values;
    }

    /// <inheritdoc />
    public void StoreDiscoveryResult(DiscoveryResult result)
    {
        _discoveryResults.AddOrUpdate(result.RequestId, result, (key, old) => result);

        _logger.LogDebug("[SESSION MANAGER] Discovery result stored for '{RequestId}'", result.RequestId);
    }

    /// <inheritdoc />
    public DiscoveryResult? GetDiscoveryResult(string requestId)
    {
        _discoveryResults.TryGetValue(requestId, out var result);
        return result;
    }

    /// <inheritdoc />
    public DiscoveryResult CreatePendingDiscoveryResult(string requestId)
    {
        var result = new DiscoveryResult
        {
            RequestId = requestId,
            Status = "pending",
            ReceivedAt = DateTime.UtcNow
        };

        _discoveryResults.TryAdd(requestId, result);

        _logger.LogDebug("[SESSION MANAGER] Pending discovery result created for '{RequestId}'", requestId);

        return result;
    }

    /// <inheritdoc />
    public IntuneStatusResult CreatePendingIntuneStatusResult(string requestId)
    {
        var result = new IntuneStatusResult
        {
            RequestId = requestId,
            Status = "pending",
            ReceivedAt = DateTime.UtcNow
        };

        _intuneStatusResults.TryAdd(requestId, result);

        _logger.LogDebug("[SESSION MANAGER] Pending Intune status result created for '{RequestId}'", requestId);

        return result;
    }

    /// <inheritdoc />
    public void StoreIntuneStatusResult(IntuneStatusResult result)
    {
        _intuneStatusResults.AddOrUpdate(result.RequestId, result, (key, old) => result);

        _logger.LogDebug("[SESSION MANAGER] Intune status result stored for '{RequestId}'", result.RequestId);
    }

    /// <inheritdoc />
    public IntuneStatusResult? GetIntuneStatusResult(string requestId)
    {
        _intuneStatusResults.TryGetValue(requestId, out var result);
        return result;
    }
}
