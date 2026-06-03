// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Utils;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Acquires per-server Bearer tokens for MCP servers in local development scenarios.
    /// Reads tokens from environment variables instead of performing an OBO flow,
    /// enabling local testing without a full auth setup.
    /// </summary>
    /// <remarks>
    /// Token resolution priority per server:
    /// <list type="number">
    ///   <item>Per-server variable: <c>BEARER_TOKEN_{SERVERNAME_UPPER}</c>
    ///       (e.g. <c>BEARER_TOKEN_MCP_MAILTOOLS</c> for server <c>mcp_MailTools</c>).</item>
    ///   <item>Shared fallback variable: <c>BEARER_TOKEN</c>.</item>
    /// </list>
    /// Hyphens in the server name are normalised to underscores before the lookup.
    /// </remarks>
    internal sealed class DevMcpTokenProvider : IMcpTokenProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="DevMcpTokenProvider"/>.
        /// </summary>
        /// <param name="configuration">Application configuration (env vars, appsettings, etc.).</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        public DevMcpTokenProvider(IConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Normalise: upper-case and replace hyphens with underscores to match env-var naming.
            // e.g. "mcp_MailTools" → "BEARER_TOKEN_MCP_MAILTOOLS"
            var normalizedName = server.mcpServerName.ToUpperInvariant().Replace('-', '_');
            var perServerKey = $"BEARER_TOKEN_{normalizedName}";

            var token = Utility.GetDevBearerToken(server.mcpServerName, _configuration);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    $"No dev token found for MCP server '{server.mcpServerName}'. " +
                    $"Set environment variable '{perServerKey}' or 'BEARER_TOKEN'.");
            }

            // Warn when a V2 server (distinct audience) falls back to the shared BEARER_TOKEN.
            // The ATG-scoped shared token will cause a 401 at the V2 server; the developer should
            // set BEARER_TOKEN_<SERVERNAME> to a token scoped for the server's own audience.
            bool usingSharedFallback = string.IsNullOrWhiteSpace(_configuration[perServerKey]);
            if (usingSharedFallback &&
                !string.IsNullOrWhiteSpace(server.audience) &&
                !Utility.IsAtgAudience(server.audience))
            {
                _logger.LogWarning(
                    "Dev token for V2 MCP server '{ServerName}' (audience: '{Audience}') is using " +
                    "the shared BEARER_TOKEN fallback. This token is ATG-scoped and may cause a 401. " +
                    "Set '{PerServerKey}' to a token scoped for this server's audience.",
                    server.mcpServerName, server.audience, perServerKey);
            }

            _logger.LogDebug(
                "Using dev token for MCP server '{ServerName}' (key: '{EnvKey}')",
                server.mcpServerName,
                usingSharedFallback ? "BEARER_TOKEN" : perServerKey);

            return Task.FromResult(token);
        }
    }
}
