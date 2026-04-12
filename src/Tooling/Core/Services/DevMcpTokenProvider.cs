// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using Microsoft.Agents.A365.Tooling.Models;
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

            var perServerValue = _configuration[perServerKey];
            var token = !string.IsNullOrWhiteSpace(perServerValue)
                ? perServerValue
                : _configuration["BEARER_TOKEN"];

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    $"No dev token found for MCP server '{server.mcpServerName}'. " +
                    $"Set environment variable '{perServerKey}' or 'BEARER_TOKEN'.");
            }

            _logger.LogDebug(
                "Using dev token for MCP server '{ServerName}' (key: '{EnvKey}')",
                server.mcpServerName,
                !string.IsNullOrWhiteSpace(perServerValue) ? perServerKey : "BEARER_TOKEN");

            return Task.FromResult(token);
        }
    }
}
