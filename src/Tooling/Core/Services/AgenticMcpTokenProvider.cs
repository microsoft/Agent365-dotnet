// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using System.Collections.Concurrent;
    using Microsoft.Agents.A365.Runtime.Authentication;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Utils;
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Acquires per-audience OAuth tokens for MCP servers using the agentic OBO flow.
    /// </summary>
    /// <remarks>
    /// This class is instantiated once per request (not registered in DI) so that
    /// the <c>userAuthorization</c> and <c>turnContext</c> per-request objects can be captured safely.
    /// Token results are cached by scope for the lifetime of this instance so that multiple
    /// V1 servers (which share the same ATG scope) do not trigger redundant OBO exchanges
    /// within a single request.
    /// </remarks>
    internal sealed class AgenticMcpTokenProvider : IMcpTokenProvider
    {
        private readonly UserAuthorization _userAuthorization;
        private readonly string _authHandlerName;
        private readonly ITurnContext _turnContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        // Scope → token cache for this request lifetime only.
        private readonly ConcurrentDictionary<string, string> _tokenCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of <see cref="AgenticMcpTokenProvider"/>.
        /// </summary>
        /// <param name="userAuthorization">Per-request user authorization object.</param>
        /// <param name="authHandlerName">Name of the registered auth handler.</param>
        /// <param name="turnContext">The current turn context.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        public AgenticMcpTokenProvider(
            UserAuthorization userAuthorization,
            string authHandlerName,
            ITurnContext turnContext,
            IConfiguration configuration,
            ILogger logger)
        {
            _userAuthorization = userAuthorization;
            _authHandlerName = authHandlerName;
            _turnContext = turnContext;
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scope = Utility.ResolveTokenScopeForServer(server, _configuration);

            // Return cached token for this scope if already acquired this request.
            if (_tokenCache.TryGetValue(scope, out var cached))
            {
                return cached;
            }

            _logger.LogDebug(
                "Acquiring token for MCP server '{ServerName}' with scope '{Scope}'",
                server.mcpServerName,
                scope);

            var token = await AgenticAuthenticationService
                .GetAgenticUserTokenAsync(_userAuthorization, _authHandlerName, _turnContext, new[] { scope })
                .ConfigureAwait(false);

            _tokenCache[scope] = token;
            return token;
        }
    }
}
