// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using System;

namespace Microsoft.Agents.A365.Observability.Hosting.Caching
{
    /// <summary>
    /// Struct containing UserAuthorization and TurnContext for token generation.
    /// </summary>
    public class AgenticTokenStruct
    {
        /// <summary>
        /// UserAuthorization instance used to acquire tokens.
        /// </summary>
        public UserAuthorization UserAuthorization { get; set; }

        /// <summary>
        /// ITurnContext instance used to acquire tokens.
        /// </summary>
        public ITurnContext TurnContext { get; set; }

        /// <summary>
        /// Handler name to use with the UserAuthorization system.
        /// </summary>
        public string AuthHandlerName { get; set; }

        /// <summary>
        /// Connection name, if applicable, to use for the exchange. 
        /// </summary>
        public string? ConnectionName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AgenticTokenStruct"/> class.
        /// </summary>
        /// <param name="userAuthorization"></param>
        /// <param name="turnContext"></param>
        /// <param name="authHandlerName"></param>
        /// <param name="connectionName"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public AgenticTokenStruct(
            UserAuthorization userAuthorization,
            ITurnContext turnContext,
            string authHandlerName,
            string? connectionName = null)
        {
            UserAuthorization = userAuthorization ?? throw new ArgumentNullException(nameof(userAuthorization));
            TurnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            AuthHandlerName = authHandlerName ?? throw new ArgumentNullException(nameof(authHandlerName));
            ConnectionName = connectionName;
        }
    }
}