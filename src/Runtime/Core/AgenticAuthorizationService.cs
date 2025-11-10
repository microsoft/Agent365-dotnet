// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Runtime.Authentication
{
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Agents.A365.Runtime.Utils;

    /// <summary>
    /// Provides authentication services for agentic user scenarios.
    /// </summary>
    public class AgenticAuthenticationService
    {
        /// <summary>
        /// Gets an agentic user token using the provided <see cref="UserAuthorization"/> and <see cref="ITurnContext"/>.
        /// </summary>
        /// <param name="userAuthorization">The user authorization instance.</param>
        /// <param name="authHandlerName">Authentication Handler Name for use with the UserAuthorization System</param>
        /// <param name="turnContext">The turn context for the current conversation.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The agentic user token as a string.</returns>
        public static async Task<string> GetAgenticUserTokenAsync(UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, IConfiguration configuration)
        {
            var scopes = new List<string> { Utility.GetMcpPlatformAuthenticationScope(configuration) };

            return await userAuthorization.ExchangeTurnTokenAsync(turnContext, authHandlerName, exchangeScopes: scopes);
        }
    }
}
