// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Microsoft.Agents.A365.Tooling.Services
{
    internal class TokenProviderCollection : IMcpTokenProvider
    {
        readonly SortedDictionary<int, IMcpTokenProvider> _providers;
        readonly ILogger _logger;

        public TokenProviderCollection(
            ILogger logger,
            params IMcpTokenProvider[] providers)
        {
            _logger = logger;
            _providers = new SortedDictionary<int, IMcpTokenProvider>();
            for (int i = 0; i < providers.Length; i++)
            {
                _providers.Add(i, providers[i]);
            }

        }

        public async Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ( _providers != null && _providers.Count == 0)
            {
                throw new InvalidOperationException("No token providers are configured.");
            }

            if (_providers!.Values.Any(p => p == null))
            {
                throw new InvalidOperationException("One or more token providers are null.");
            }

            List<Exception> exceptions = new List<Exception>();
            // Try each provider in turn, then return the first successful token. If no providers can return a token, throw an exception.
            foreach (var provider in _providers.Values)
            {
                try
                {
                    var token = await provider.GetTokenAsync(server, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        return token;
                    }
                }
                catch(Exception ex)
                {
                    exceptions.Add(new Exception($"Provider {provider.GetType().Name} failed to obtain a token." , ex));
                }
            }
            throw new AggregateException("No valid token could be obtained from any provider.", exceptions);
        }
    }
}
