using Microsoft.Agents.A365.Tooling.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Tooling.Services
{
    internal class TokenProviderCollection : IMcpTokenProvider
    {
        SortedDictionary<int, IMcpTokenProvider> _providers;
        public TokenProviderCollection(params IMcpTokenProvider[] providers)
        {
           _providers = new SortedDictionary<int, IMcpTokenProvider>();
           for (int i = 0; i < providers.Length; i++)
           {
               _providers.Add(i, providers[i]);
           }
        }

        public async Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default)
        {
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
