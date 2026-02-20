// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Tooling.Services;

/// <summary>
/// Implementation of MCP policy enforcement service.
/// Checks user's registered desktops and determines routing for tool calls.
/// </summary>
public class McpPolicyEnforcementService : IMcpPolicyEnforcementService
{
    private readonly ILogger<McpPolicyEnforcementService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    
    /// <summary>
    /// Cache of servers that require device path routing.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _devicePathServers = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Cache of cloud server configurations for device path servers.
    /// </summary>
    private readonly ConcurrentDictionary<string, CloudServerRegistration> _cloudServerConfigs = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Cache of user desktop registrations to avoid repeated HTTP calls.
    /// Key: userIdentifier, Value: (hasDesktop, clientName, expiry)
    /// </summary>
    private readonly ConcurrentDictionary<string, (bool HasDesktop, string? ClientName, DateTime Expiry)> _userDesktopCache = new();
    
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="McpPolicyEnforcementService"/> class.
    /// </summary>
    public McpPolicyEnforcementService(
        ILogger<McpPolicyEnforcementService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public bool ServerRequiresDevicePath(string serverName)
    {
        return _devicePathServers.ContainsKey(serverName);
    }

    /// <inheritdoc />
    public void RegisterDevicePathServer(string serverName)
    {
        _devicePathServers[serverName] = true;
        _logger.LogInformation("[Policy] Registered server '{ServerName}' as requiring device path routing", serverName);
    }

    /// <inheritdoc />
    public void RegisterDevicePathServer(string serverName, CloudServerRegistration cloudConfig)
    {
        _devicePathServers[serverName] = true;
        _cloudServerConfigs[serverName] = cloudConfig;
        _logger.LogInformation("[Policy] Registered cloud server '{ServerName}' for device path routing. Endpoint: {Endpoint}", 
            serverName, cloudConfig.Endpoint);
    }

    /// <inheritdoc />
    public CloudServerRegistration? GetCloudServerConfig(string serverName)
    {
        return _cloudServerConfigs.TryGetValue(serverName, out var config) ? config : null;
    }

    /// <inheritdoc />
    public async Task<PolicyEnforcementResult> EvaluatePolicyAsync(
        string serverName,
        string toolName,
        string userIdentifier,
        CancellationToken cancellationToken = default)
    {
        // If server doesn't require device path, allow direct access
        if (!ServerRequiresDevicePath(serverName))
        {
            _logger.LogDebug("[Policy] Server '{ServerName}' does not require device path, allowing direct access", serverName);
            return new PolicyEnforcementResult { Action = PolicyEnforcementAction.AllowDirect };
        }

        _logger.LogInformation("[Policy] Server '{ServerName}' requires device path routing. Checking user '{User}' desktop registration...", 
            serverName, userIdentifier);

        // Check if user has a registered desktop
        var (hasDesktop, clientName) = await CheckUserHasDesktopAsync(userIdentifier, cancellationToken);

        if (!hasDesktop)
        {
            var proxyBaseUrl = _configuration["LocalMcp:BaseUrl"] ?? _configuration["LocalMcpProxy:BaseUrl"];
            var encodedUser = System.Net.WebUtility.UrlEncode(userIdentifier);
            var registrationUrl = $"locaproto:?action=register&callback={proxyBaseUrl}/api/channels/register&user={encodedUser}";

            _logger.LogWarning("[Policy] User '{User}' has no registered desktop. Tool '{Tool}' on '{Server}' blocked.", 
                userIdentifier, toolName, serverName);

            return new PolicyEnforcementResult
            {
                Action = PolicyEnforcementAction.BlockRequiresRegistration,
                ErrorMessage = $"This action requires a registered desktop to enforce security policies. Please register your desktop application to use {toolName}.",
                RegistrationProtocolUrl = registrationUrl
            };
        }

        // User has desktop - route through locaproto for policy enforcement
        var desktopProxyBaseUrl = _configuration["LocalMcp:BaseUrl"] ?? _configuration["LocalMcpProxy:BaseUrl"];
        
        _logger.LogInformation("[Policy] Routing tool '{Tool}' on '{Server}' through desktop '{Desktop}' for policy enforcement", 
            toolName, serverName, clientName);

        // Include cloud server config if available (for cloud MCP servers)
        var cloudConfig = GetCloudServerConfig(serverName);

        return new PolicyEnforcementResult
        {
            Action = PolicyEnforcementAction.RouteToDesktop,
            DesktopClientName = clientName,
            DesktopProxyBaseUrl = desktopProxyBaseUrl,
            CloudServerConfig = cloudConfig
        };
    }

    /// <summary>
    /// Checks if a user has a registered desktop by querying the LocalMcp proxy.
    /// </summary>
    private async Task<(bool HasDesktop, string? ClientName)> CheckUserHasDesktopAsync(
        string userIdentifier, 
        CancellationToken cancellationToken)
    {
        // Check cache first
        if (_userDesktopCache.TryGetValue(userIdentifier, out var cached) && cached.Expiry > DateTime.UtcNow)
        {
            _logger.LogDebug("[Policy] Using cached desktop registration status for user '{User}'", userIdentifier);
            return (cached.HasDesktop, cached.ClientName);
        }

        var proxyBaseUrl = _configuration["LocalMcp:BaseUrl"] ?? _configuration["LocalMcpProxy:BaseUrl"];
        if (string.IsNullOrEmpty(proxyBaseUrl))
        {
            _logger.LogWarning("[Policy] LocalMcp:BaseUrl not configured. Cannot check desktop registration.");
            return (false, null);
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("LocalMcpDiscovery");
            var encodedUser = System.Net.WebUtility.UrlEncode(userIdentifier);
            
            using var response = await httpClient.GetAsync(
                $"{proxyBaseUrl}/api/channels/by-user/{encodedUser}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No desktop registered
                _userDesktopCache[userIdentifier] = (false, null, DateTime.UtcNow.Add(CacheDuration));
                return (false, null);
            }

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var json = JsonSerializer.Deserialize<JsonElement>(responseBody);
                
                if (json.TryGetProperty("clients", out var clientsArray) && 
                    clientsArray.ValueKind == JsonValueKind.Array &&
                    clientsArray.GetArrayLength() > 0)
                {
                    // Get the most recently active client
                    string? bestClientName = null;
                    DateTime bestLastSeen = DateTime.MinValue;

                    foreach (var client in clientsArray.EnumerateArray())
                    {
                        var clientName = client.GetProperty("clientName").GetString();
                        var lastSeenStr = client.TryGetProperty("lastSeen", out var ls) ? ls.GetString() : null;
                        var lastSeen = DateTime.TryParse(lastSeenStr, out var dt) ? dt : DateTime.MinValue;

                        if (lastSeen > bestLastSeen)
                        {
                            bestLastSeen = lastSeen;
                            bestClientName = clientName;
                        }
                    }

                    _userDesktopCache[userIdentifier] = (true, bestClientName, DateTime.UtcNow.Add(CacheDuration));
                    return (true, bestClientName);
                }
            }

            _userDesktopCache[userIdentifier] = (false, null, DateTime.UtcNow.Add(CacheDuration));
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Policy] Error checking desktop registration for user '{User}'", userIdentifier);
            return (false, null);
        }
    }

    /// <inheritdoc />
    public void InvalidateUserCache(string userIdentifier)
    {
        if (_userDesktopCache.TryRemove(userIdentifier, out _))
        {
            _logger.LogInformation("[Policy] Invalidated desktop registration cache for user '{User}'", userIdentifier);
        }
    }
}
