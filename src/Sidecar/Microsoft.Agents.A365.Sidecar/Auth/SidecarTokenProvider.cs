// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Sidecar.Auth;

/// <summary>
/// Provides Azure AD token acquisition for the sidecar's outbound calls
/// (Observability exporter and Tooling gateway).
/// Supports managed identity, client credentials, and DefaultAzureCredential modes.
/// </summary>
public sealed class SidecarTokenProvider
{
    private readonly TokenCredential _credential;
    private readonly string[] _observabilityScopes;
    private readonly ILogger<SidecarTokenProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SidecarTokenProvider"/> class.
    /// </summary>
    public SidecarTokenProvider(IOptions<SidecarOptions> options, ILogger<SidecarTokenProvider> logger)
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
            _ => new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeInteractiveBrowserCredential = true })
        };

        _observabilityScopes = EnvironmentUtils.GetObservabilityAuthenticationScope();
        _logger.LogInformation(
            "SidecarTokenProvider initialized with mode '{Mode}'. Observability scope: {Scope}",
            auth.Mode, _observabilityScopes[0]);
    }

    /// <summary>
    /// Resolves an auth token for the A365 Observability API.
    /// Signature matches <c>AsyncAuthTokenResolver(string agentId, string tenantId)</c>.
    /// </summary>
    public async Task<string?> ResolveObservabilityTokenAsync(string agentId, string tenantId)
    {
        try
        {
            var context = new TokenRequestContext(_observabilityScopes);
            var token = await _credential.GetTokenAsync(context, CancellationToken.None).ConfigureAwait(false);
            _logger.LogDebug(
                "Acquired observability token for agent {AgentId}, tenant {TenantId}. Expires: {Expiry}",
                agentId, tenantId, token.ExpiresOn);
            return token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to acquire observability token for agent {AgentId}, tenant {TenantId}.",
                agentId, tenantId);
            return null;
        }
    }

    /// <summary>
    /// Resolves an auth token for the Tooling Gateway.
    /// Uses the same credential but with the tooling scope (derived from gateway endpoint).
    /// </summary>
    public async Task<string?> ResolveToolingTokenAsync(string scope)
    {
        try
        {
            var context = new TokenRequestContext(new[] { scope });
            var token = await _credential.GetTokenAsync(context, CancellationToken.None).ConfigureAwait(false);
            _logger.LogDebug("Acquired tooling token. Expires: {Expiry}", token.ExpiresOn);
            return token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire tooling token for scope {Scope}.", scope);
            return null;
        }
    }
}
