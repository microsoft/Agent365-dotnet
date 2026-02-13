// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Tooling.Services;

/// <summary>
/// Validates that the agent blueprint has admin consent for local MCP server scopes.
/// 
/// For remote MCP servers, scope validation happens implicitly:
/// 1. Agent requests token with MCP server's scope
/// 2. Token acquisition fails if admin hasn't granted consent
/// 3. Remote MCP server validates the token's scope claim
/// 
/// For local MCP servers (via WNS), no token is sent to the local server,
/// so we need explicit validation:
/// 1. Read required scope from ToolingManifest.json localMcpServers section
/// 2. Check if blueprint has consent for that scope from the resource app
/// 3. Block invocation if consent is not granted
/// </summary>
public class LocalMcpScopeValidator : ILocalMcpScopeValidator
{
    private readonly ILogger<LocalMcpScopeValidator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    // Cache of local MCP server configs from manifest
    private List<LocalMcpServerManifestConfig>? _cachedLocalServers;
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of the LocalMcpScopeValidator.
    /// </summary>
    public LocalMcpScopeValidator(
        ILogger<LocalMcpScopeValidator> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<LocalMcpScopeValidationResult> ValidateScopeAsync(
        string localServerName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[LocalMcpScope] Validating scope for local server '{ServerName}'", localServerName);

        // 1. Get required scope from manifest
        var localServers = await LoadLocalMcpServersFromManifestAsync();

        // Try to find matching server - check both exact match and normalized names
        var serverConfig = FindMatchingServerConfig(localServers, localServerName);

        if (serverConfig == null)
        {
            // Server not in manifest - check configuration for whether to allow
            var requireManifestEntry = _configuration.GetValue("LocalMcp:RequireManifestEntry", true);
            if (requireManifestEntry)
            {
                _logger.LogWarning("[LocalMcpScope] Server '{ServerName}' not found in localMcpServers manifest and RequireManifestEntry=true", localServerName);
                return LocalMcpScopeValidationResult.Failed(
                    $"Local MCP server '{localServerName}' is not declared in ToolingManifest.json localMcpServers section. " +
                    "Add it to the manifest with the required scope to enable access.");
            }

            _logger.LogInformation("[LocalMcpScope] Server '{ServerName}' not in manifest, RequireManifestEntry=false - allowing", localServerName);
            return LocalMcpScopeValidationResult.NotInManifest(localServerName);
        }

        // 2. Check if scope is required
        if (string.IsNullOrWhiteSpace(serverConfig.Scope))
        {
            _logger.LogInformation("[LocalMcpScope] Server '{ServerName}' has no scope requirement - allowing", localServerName);
            return LocalMcpScopeValidationResult.Success(string.Empty, serverConfig.Audience);
        }

        // 3. Check consent for the required scope
        var blueprintAppId = _configuration["AzureAd:ClientId"]
            ?? _configuration["MicrosoftAppId"]
            ?? _configuration["AgentBlueprint:AppId"]
            ?? _configuration["Connections:ServiceConnection:Settings:ClientId"];

        if (string.IsNullOrWhiteSpace(blueprintAppId))
        {
            _logger.LogWarning("[LocalMcpScope] Cannot validate scope - blueprint app ID not configured. " +
                "Set AzureAd:ClientId, MicrosoftAppId, AgentBlueprint:AppId, or Connections:ServiceConnection:Settings:ClientId");
            // Block if we can't validate - security first
            return LocalMcpScopeValidationResult.Failed(
                "Cannot validate scope - blueprint app ID not configured in appsettings.json");
        }

        _logger.LogInformation("[LocalMcpScope] Found blueprint app ID: {BlueprintAppId}", blueprintAppId);

        var tenantId = _configuration["AzureAd:TenantId"]
            ?? _configuration["MicrosoftAppTenantId"]
            ?? _configuration["AgentBlueprint:TenantId"]
            ?? _configuration["TokenValidation:TenantId"]
            ?? _configuration["WnsConfiguration:TenantId"]
            ?? _configuration["Connections:ServiceConnection:Settings:TenantId"];

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning("[LocalMcpScope] Cannot validate scope - tenant ID not configured");
            return LocalMcpScopeValidationResult.Failed(
                "Cannot validate scope - tenant ID not configured in appsettings.json");
        }

        _logger.LogInformation("[LocalMcpScope] Found tenant ID: {TenantId}", tenantId);

        _logger.LogInformation("[LocalMcpScope] Checking consent for blueprint '{BlueprintId}' in tenant '{TenantId}' for scope '{Scope}'",
            blueprintAppId, tenantId, serverConfig.Scope);

        // 4. Check if consent has been granted
        var hasConsent = await CheckConsentGrantedAsync(
            tenantId,
            blueprintAppId,
            serverConfig.Audience,
            serverConfig.Scope,
            cancellationToken);

        if (hasConsent)
        {
            _logger.LogInformation("[LocalMcpScope] Consent verified for scope '{Scope}' on server '{ServerName}'",
                serverConfig.Scope, localServerName);
            return LocalMcpScopeValidationResult.Success(serverConfig.Scope, serverConfig.Audience);
        }

        _logger.LogWarning("[LocalMcpScope] Admin consent NOT granted for scope '{Scope}' required by server '{ServerName}'",
            serverConfig.Scope, localServerName);

        return LocalMcpScopeValidationResult.Failed(
            $"Admin consent has not been granted for scope '{serverConfig.Scope}' required by local MCP server '{localServerName}'. " +
            $"An administrator must grant consent for the blueprint to access this scope from resource app '{serverConfig.Audience}'.",
            serverConfig.Scope,
            serverConfig.Audience);
    }

    /// <summary>
    /// Finds a matching server configuration by comparing server names.
    /// Handles the case where ODR-discovered servers have different naming than manifest.
    /// For example:
    /// - ODR: "MicrosoftWindows.Client.Core_cw5n1h2txyewy/file-mcp-server" or "file-mcp-server"
    /// - Manifest: "LocalFileMcpServer" with serverId pattern "file"
    /// </summary>
    private LocalMcpServerManifestConfig? FindMatchingServerConfig(
        List<LocalMcpServerManifestConfig> localServers,
        string serverName)
    {
        // 1. Try exact match first
        var exactMatch = localServers.FirstOrDefault(s =>
            s.McpServerName.Equals(serverName, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            _logger.LogDebug("[LocalMcpScope] Found exact match for '{ServerName}'", serverName);
            return exactMatch;
        }

        // 2. Try matching by serverId pattern if configured
        foreach (var server in localServers)
        {
            if (!string.IsNullOrEmpty(server.ServerIdPattern))
            {
                if (serverName.Contains(server.ServerIdPattern, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("[LocalMcpScope] Found pattern match for '{ServerName}' via pattern '{Pattern}'",
                        serverName, server.ServerIdPattern);
                    return server;
                }
            }
        }

        // 3. Try normalized name matching (extract meaningful part from ODR names)
        var normalizedServerName = NormalizeServerName(serverName);
        foreach (var server in localServers)
        {
            var normalizedManifestName = NormalizeServerName(server.McpServerName);
            if (normalizedServerName.Equals(normalizedManifestName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[LocalMcpScope] Found normalized match: '{ServerName}' -> '{ManifestName}'",
                    serverName, server.McpServerName);
                return server;
            }

            // Also try if normalized name contains or is contained by manifest name
            if (normalizedServerName.Contains(normalizedManifestName, StringComparison.OrdinalIgnoreCase) ||
                normalizedManifestName.Contains(normalizedServerName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[LocalMcpScope] Found partial normalized match: '{ServerName}' <-> '{ManifestName}'",
                    serverName, server.McpServerName);
                return server;
            }
        }

        _logger.LogDebug("[LocalMcpScope] No match found for '{ServerName}'", serverName);
        return null;
    }

    /// <summary>
    /// Normalizes a server name by extracting meaningful parts.
    /// Handles ODR-style names like "MicrosoftWindows.Client.Core_cw5n1h2txyewy/file-mcp-server"
    /// </summary>
    private static string NormalizeServerName(string serverName)
    {
        if (string.IsNullOrEmpty(serverName))
            return string.Empty;

        // Extract part after last "/" (e.g., "file-mcp-server" from full path)
        var lastSlash = serverName.LastIndexOf('/');
        var name = lastSlash >= 0 ? serverName.Substring(lastSlash + 1) : serverName;

        // Remove common suffixes/prefixes
        name = name.Replace("-mcp-server", "", StringComparison.OrdinalIgnoreCase)
                   .Replace("MCP", "", StringComparison.OrdinalIgnoreCase)
                   .Replace("Server", "", StringComparison.OrdinalIgnoreCase)
                   .Replace("Local", "", StringComparison.OrdinalIgnoreCase)
                   .Replace("-", "")
                   .Replace("_", "");

        return name.ToLowerInvariant();
    }

    /// <inheritdoc/>
    public string? GetRequiredScope(string localServerName)
    {
        lock (_cacheLock)
        {
            if (_cachedLocalServers == null)
            {
                return null;
            }

            var config = FindMatchingServerConfig(_cachedLocalServers, localServerName);
            return config?.Scope;
        }
    }

    /// <inheritdoc/>
    public async Task<List<LocalMcpServerManifestConfig>> LoadLocalMcpServersFromManifestAsync()
    {
        lock (_cacheLock)
        {
            if (_cachedLocalServers != null)
            {
                return _cachedLocalServers;
            }
        }

        var localServers = new List<LocalMcpServerManifestConfig>();

        try
        {
            // Find ToolingManifest.json
            var manifestPath = FindToolingManifestPath();
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                _logger.LogDebug("[LocalMcpScope] ToolingManifest.json not found");
                return localServers;
            }

            var json = await File.ReadAllTextAsync(manifestPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("localMcpServers", out var localServersElement))
            {
                _logger.LogDebug("[LocalMcpScope] No 'localMcpServers' section in ToolingManifest.json");
                return localServers;
            }

            if (localServersElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("[LocalMcpScope] 'localMcpServers' is not an array in ToolingManifest.json");
                return localServers;
            }

            foreach (var serverElement in localServersElement.EnumerateArray())
            {
                var config = new LocalMcpServerManifestConfig();

                if (serverElement.TryGetProperty("mcpServerName", out var nameElement))
                {
                    config.McpServerName = nameElement.GetString() ?? string.Empty;
                }

                if (serverElement.TryGetProperty("scope", out var scopeElement))
                {
                    config.Scope = scopeElement.GetString() ?? string.Empty;
                }

                if (serverElement.TryGetProperty("audience", out var audienceElement))
                {
                    config.Audience = audienceElement.GetString() ?? string.Empty;
                }

                if (serverElement.TryGetProperty("transportType", out var transportElement))
                {
                    config.TransportType = transportElement.GetString() ?? "wns";
                }

                if (serverElement.TryGetProperty("description", out var descElement))
                {
                    config.Description = descElement.GetString();
                }

                if (serverElement.TryGetProperty("serverIdPattern", out var patternElement))
                {
                    config.ServerIdPattern = patternElement.GetString();
                }

                if (!string.IsNullOrEmpty(config.McpServerName))
                {
                    localServers.Add(config);
                    _logger.LogDebug("[LocalMcpScope] Loaded local server config: {Name} (scope: {Scope}, pattern: {Pattern})",
                        config.McpServerName, config.Scope, config.ServerIdPattern ?? "none");
                }
            }

            lock (_cacheLock)
            {
                _cachedLocalServers = localServers;
            }

            _logger.LogInformation("[LocalMcpScope] Loaded {Count} local MCP server configs from manifest", localServers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LocalMcpScope] Failed to load local MCP servers from manifest");
        }

        return localServers;
    }

    /// <summary>
    /// Checks if the blueprint has been granted consent for the specified scope.
    /// This queries Graph API for oauth2PermissionGrants.
    /// </summary>
    private async Task<bool> CheckConsentGrantedAsync(
        string tenantId,
        string blueprintAppId,
        string resourceAppId,
        string scope,
        CancellationToken cancellationToken)
    {
        // Check if validation should be skipped in development
        var skipValidation = _configuration.GetValue("LocalMcp:SkipScopeValidation", false);
        if (skipValidation)
        {
            _logger.LogWarning("[LocalMcpScope] Scope validation SKIPPED (LocalMcp:SkipScopeValidation=true) - this should only be used in development!");
            return true;
        }

        // Try to verify consent via Graph API using Local MCP Resource App credentials
        // The Local MCP Resource App has Directory.Read.All permission to query consent
        try
        {
            var consentGranted = await CheckConsentViaGraphApiAsync(tenantId, blueprintAppId, resourceAppId, scope, cancellationToken);
            if (consentGranted)
            {
                _logger.LogInformation("[LocalMcpScope] Consent verified via Graph API for scope '{Scope}'", scope);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LocalMcpScope] Failed to verify consent via Graph API, falling back to configuration check");
        }

        // Fall back to checking configuration (for cases where Graph API is not accessible)
        var consentConfigKey = $"LocalMcp:ConsentedScopes:{resourceAppId}:{scope}";
        var configConsent = _configuration.GetValue(consentConfigKey, false);

        if (configConsent)
        {
            _logger.LogDebug("[LocalMcpScope] Consent found in configuration for scope '{Scope}'", scope);
            return true;
        }

        // Fall back to checking environment variable (set by DevTools or admin)
        var envKey = $"LOCALMCP_CONSENT_{scope.Replace(".", "_").ToUpperInvariant()}";
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrEmpty(envValue) && envValue.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("[LocalMcpScope] Consent found in environment variable for scope '{Scope}'", scope);
            return true;
        }

        _logger.LogDebug("[LocalMcpScope] No consent found for scope '{Scope}' from resource '{Resource}'", scope, resourceAppId);
        return false;
    }

    /// <summary>
    /// Checks consent by querying Graph API using the Local MCP Resource App credentials.
    /// The Local MCP Resource App (not the blueprint) has Directory.Read.All permission
    /// to query oauth2PermissionGrants and verify consent.
    /// </summary>
    private async Task<bool> CheckConsentViaGraphApiAsync(
        string tenantId,
        string blueprintAppId,
        string resourceAppId,
        string scope,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("GraphApi");

        // Get Local MCP Resource App credentials from config
        // This is the app that exposes the local MCP scopes and has Graph API permissions
        var localMcpClientId = _configuration["LocalMcp:ResourceApp:ClientId"];
        var localMcpClientSecret = _configuration["LocalMcp:ResourceApp:ClientSecret"];

        if (string.IsNullOrEmpty(localMcpClientId) || string.IsNullOrEmpty(localMcpClientSecret))
        {
            _logger.LogWarning("[LocalMcpScope] Cannot validate consent via Graph API - missing Local MCP Resource App credentials. " +
                "Configure LocalMcp:ResourceApp:ClientId and LocalMcp:ResourceApp:ClientSecret");
            return false;
        }

        _logger.LogInformation("[LocalMcpScope] Using Local MCP Resource App '{ClientId}' to query Graph API", localMcpClientId);

        // Get access token for Graph API using Local MCP Resource App credentials
        var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = localMcpClientId,
            ["client_secret"] = localMcpClientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        using var tokenResponse = await httpClient.PostAsync(tokenEndpoint, tokenRequest, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[LocalMcpScope] Failed to get Graph API token: {Status} - {Error}", tokenResponse.StatusCode, errorContent);
            return false;
        }

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        using var tokenDoc = JsonDocument.Parse(tokenJson);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("[LocalMcpScope] Graph API token response missing access_token");
            return false;
        }

        _logger.LogInformation("[LocalMcpScope] Got Graph API access token successfully");

        // Step 1: Get service principal ID for the blueprint app
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var blueprintSpResponse = await httpClient.GetAsync(
            $"https://graph.microsoft.com/v1.0/servicePrincipals?$filter=appId eq '{blueprintAppId}'&$select=id",
            cancellationToken);

        if (!blueprintSpResponse.IsSuccessStatusCode)
        {
            var errorContent = await blueprintSpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[LocalMcpScope] Failed to get blueprint service principal: {Status} - {Error}", blueprintSpResponse.StatusCode, errorContent);
            return false;
        }

        var blueprintSpJson = await blueprintSpResponse.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("[LocalMcpScope] Blueprint SP response: {Response}", blueprintSpJson);
        using var blueprintSpDoc = JsonDocument.Parse(blueprintSpJson);

        var blueprintSpArray = blueprintSpDoc.RootElement.GetProperty("value");
        if (blueprintSpArray.GetArrayLength() == 0)
        {
            _logger.LogWarning("[LocalMcpScope] Blueprint service principal not found for appId '{AppId}'", blueprintAppId);
            return false;
        }

        var blueprintSpId = blueprintSpArray[0].GetProperty("id").GetString();
        _logger.LogInformation("[LocalMcpScope] Blueprint service principal ID: {SpId}", blueprintSpId);

        // Step 2: Get service principal ID for the resource app
        var resourceSpResponse = await httpClient.GetAsync(
            $"https://graph.microsoft.com/v1.0/servicePrincipals?$filter=appId eq '{resourceAppId}'&$select=id",
            cancellationToken);

        if (!resourceSpResponse.IsSuccessStatusCode)
        {
            var errorContent = await resourceSpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[LocalMcpScope] Failed to get resource service principal: {Status} - {Error}", resourceSpResponse.StatusCode, errorContent);
            return false;
        }

        var resourceSpJson = await resourceSpResponse.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("[LocalMcpScope] Resource SP response: {Response}", resourceSpJson);
        using var resourceSpDoc = JsonDocument.Parse(resourceSpJson);

        var resourceSpArray = resourceSpDoc.RootElement.GetProperty("value");
        if (resourceSpArray.GetArrayLength() == 0)
        {
            _logger.LogWarning("[LocalMcpScope] Resource service principal not found for appId '{AppId}'", resourceAppId);
            return false;
        }

        var resourceSpId = resourceSpArray[0].GetProperty("id").GetString();
        _logger.LogInformation("[LocalMcpScope] Resource service principal ID: {SpId}", resourceSpId);

        // Step 3: Query oauth2PermissionGrants to check if blueprint has consent for the scope
        var grantsUrl = $"https://graph.microsoft.com/v1.0/oauth2PermissionGrants?$filter=clientId eq '{blueprintSpId}'";
        _logger.LogInformation("[LocalMcpScope] Querying oauth2PermissionGrants: {Url}", grantsUrl);

        var grantsResponse = await httpClient.GetAsync(grantsUrl, cancellationToken);

        if (!grantsResponse.IsSuccessStatusCode)
        {
            var errorContent = await grantsResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[LocalMcpScope] Failed to get oauth2PermissionGrants: {Status} - {Error}", grantsResponse.StatusCode, errorContent);
            return false;
        }

        var grantsJson = await grantsResponse.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("[LocalMcpScope] oauth2PermissionGrants response: {Response}", grantsJson);
        using var grantsDoc = JsonDocument.Parse(grantsJson);

        var grantsArray = grantsDoc.RootElement.GetProperty("value");
        _logger.LogInformation("[LocalMcpScope] Found {Count} total oauth2PermissionGrants for blueprint", grantsArray.GetArrayLength());

        foreach (var grant in grantsArray.EnumerateArray())
        {
            // Check if this grant is for our target resource
            var grantResourceId = grant.TryGetProperty("resourceId", out var resourceIdElement)
                ? resourceIdElement.GetString() : null;

            _logger.LogDebug("[LocalMcpScope] Grant resourceId: '{ResourceId}' (looking for: '{ExpectedResourceId}')",
                grantResourceId, resourceSpId);

            if (!string.Equals(grantResourceId, resourceSpId, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Skip grants for other resources
            }

            if (grant.TryGetProperty("scope", out var scopeElement))
            {
                var grantedScopesStr = scopeElement.GetString() ?? string.Empty;
                _logger.LogInformation("[LocalMcpScope] Grant scopes for resource: '{Scopes}'", grantedScopesStr);

                var grantedScopes = grantedScopesStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (grantedScopes.Any(s => s.Equals(scope, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation("[LocalMcpScope] Found consent for scope '{Scope}' - access granted", scope);
                    return true;
                }
            }
        }

        _logger.LogWarning("[LocalMcpScope] Scope '{Scope}' not found in oauth2PermissionGrants", scope);
        return false;
    }

    /// <summary>
    /// Finds the path to ToolingManifest.json.
    /// </summary>
    private string? FindToolingManifestPath()
    {
        // Check configured path first
        var configuredPath = _configuration["ToolingManifestPath"];
        if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        // Check current directory
        var currentDir = Directory.GetCurrentDirectory();
        var manifestPath = Path.Combine(currentDir, "ToolingManifest.json");
        if (File.Exists(manifestPath))
        {
            return manifestPath;
        }

        // Check app domain base directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        manifestPath = Path.Combine(baseDir, "ToolingManifest.json");
        if (File.Exists(manifestPath))
        {
            return manifestPath;
        }

        return null;
    }
}
