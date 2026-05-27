// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Sidecar.Auth;

/// <summary>
/// Sidecar implementation of <see cref="IMcpTokenProvider"/> that acquires per-audience
/// tokens for MCP tool servers using the blueprint's client credentials.
/// This enables V2 tooling where each server has its own audience/scope.
/// </summary>
public sealed class SidecarMcpTokenProvider : IMcpTokenProvider
{
    private readonly TokenCredential _credential;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SidecarMcpTokenProvider"/> class.
    /// </summary>
    public SidecarMcpTokenProvider(IOptions<SidecarOptions> options, ILogger<SidecarMcpTokenProvider> logger)
    {
        _logger = logger;
        var auth = options.Value.Auth;

        _credential = auth.Mode.ToLowerInvariant() switch
        {
            "managed-identity" => new ManagedIdentityCredential(
                string.IsNullOrEmpty(auth.ClientId) ? null : auth.ClientId),
            "client-credentials" when !string.IsNullOrEmpty(auth.ClientId)
                && !string.IsNullOrEmpty(auth.ClientSecret)
                && !string.IsNullOrEmpty(auth.TenantId) =>
                new ClientSecretCredential(auth.TenantId, auth.ClientId, auth.ClientSecret),
            _ => new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true
            })
        };
    }

    /// <summary>
    /// Acquires a token for the given MCP server's audience.
    /// V2 servers specify their own audience; the scope is derived as audience/.default.
    /// </summary>
    public async Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default)
    {
        var scope = ResolveScope(server);
        _logger.LogDebug(
            "Acquiring token for MCP server '{ServerName}' with scope '{Scope}'",
            server.mcpServerName, scope);

        try
        {
            var context = new TokenRequestContext(new[] { scope });
            var token = await _credential.GetTokenAsync(context, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Acquired token for server '{ServerName}'. Expires: {Expiry}",
                server.mcpServerName, token.ExpiresOn);
            return token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to acquire token for MCP server '{ServerName}' with scope '{Scope}'",
                server.mcpServerName, scope);
            throw;
        }
    }

    private static string ResolveScope(MCPServerConfig server)
    {
        // V2: if server has explicit scope, use it
        if (!string.IsNullOrEmpty(server.scope))
        {
            return server.scope;
        }

        // V2: derive scope from audience
        if (!string.IsNullOrEmpty(server.audience))
        {
            return $"{server.audience.TrimEnd('/')}/.default";
        }

        // Fallback: should not happen for V2 servers
        throw new InvalidOperationException(
            $"MCP server '{server.mcpServerName}' has no audience or scope configured. V2 tooling requires per-server audience.");
    }
}
