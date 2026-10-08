// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Defender
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Agents.Authentication;

    /// <summary>Token resolvers for <see cref="DefenderRtpClient"/>.</summary>
    public static class DefenderRtpTokenResolvers
    {
        private const string ClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

        /// <summary>
        /// The agent identity's own app-only token, in the agent's tenant, issued through the agent's
        /// Agents SDK connection: the connection's blueprint credential (secret, certificate, federated or
        /// managed identity) issues the agent identity's assertion, which is exchanged for the Defender
        /// API token. This is the same authority Observability S2S export uses.
        /// </summary>
        /// <param name="connection">
        /// The agent's connection, for example <c>connections.GetDefaultConnection()</c> cast to
        /// <see cref="IAgenticTokenProvider"/> (MSAL connections implement it).
        /// </param>
        /// <param name="httpClient">
        /// The HTTP client for the token endpoint, for example from <c>IHttpClientFactory</c>; defaults to a shared
        /// client that does not follow redirects. A client passed here must not follow them either, since a 307 or 308
        /// would replay the client assertion to another host; a redirected request fails.
        /// </param>
        /// <param name="authority">
        /// The Entra authority, an absolute HTTPS URL; defaults to <c>https://login.microsoftonline.com</c>.
        /// </param>
        /// <returns>A resolver for <see cref="DefenderRtpClient.EvaluateHookContextAsync"/>.</returns>
        /// <exception cref="ArgumentException">The authority is not an absolute HTTPS URL.</exception>
        public static DefenderRtpTokenResolver FromAgenticConnection(
            IAgenticTokenProvider connection,
            HttpClient? httpClient = null,
            string authority = "https://login.microsoftonline.com")
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            // The client assertion and the token must never travel in clear text.
            if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUrl) || !DefenderRtpOptions.IsHttpsUrl(authorityUrl))
            {
                throw new ArgumentException("The authority must be an absolute HTTPS URL.", nameof(authority));
            }

            var http = httpClient ?? DefenderRtpClient.SharedHttpClient;
            var baseAuthority = authorityUrl.AbsoluteUri.TrimEnd('/');
            return async (agentId, tenantId, scopes, cancellationToken) =>
            {
                var assertion = await connection.GetAgenticApplicationTokenAsync(tenantId, agentId, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(assertion))
                {
                    throw new InvalidOperationException("The agent connection returned no agent identity assertion.");
                }

                var tokenUri = new Uri($"{baseAuthority}/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token");
                using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "client_credentials",
                        ["client_id"] = agentId,
                        ["client_assertion_type"] = ClientAssertionType,
                        ["client_assertion"] = assertion,
                        ["scope"] = string.Join(" ", scopes),
                    }),
                };
                using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // A client that follows redirects has sent the assertion to another host; a token from there is never used.
                if (request.RequestUri != tokenUri)
                {
                    throw new InvalidOperationException("The Defender token request was redirected.");
                }

                // Never surface the response body: it can echo the assertion.
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"The Defender token request failed with HTTP {(int)response.StatusCode}.");
                }

                JsonNode? payload;
                try
                {
                    payload = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException("The Defender token response was not valid JSON.");
                }

                return (payload as JsonObject)?["access_token"] is JsonValue token && token.TryGetValue<string>(out var accessToken)
                    ? accessToken
                    : throw new InvalidOperationException("The Defender token response had no access_token.");
            };
        }
    }
}
