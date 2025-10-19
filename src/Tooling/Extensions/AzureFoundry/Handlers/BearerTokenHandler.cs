// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Handlers;

using Microsoft.Agents.A365.Tooling.Utils;

/// <summary>
/// Custom HTTP handler for adding Bearer token authentication
/// </summary>
public class BearerTokenHandler(string token) : DelegatingHandler
{
    private readonly string _token = token;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_token))
        {
            var authToken = _token.StartsWith($"{Constants.Headers.BearerPrefix} ", StringComparison.OrdinalIgnoreCase)
                ? _token
                : $"{Constants.Headers.BearerPrefix} {_token}";

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(Constants.Headers.BearerPrefix,
                    authToken.StartsWith($"{Constants.Headers.BearerPrefix} ", StringComparison.OrdinalIgnoreCase) ? authToken.Substring(7) : authToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}