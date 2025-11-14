// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;

namespace Microsoft.Agents.A365.Observability.Caching;

/// <summary>
/// Struct containing UserAuthorization and TurnContext for token generation.
/// </summary>
public class AgenticTokenStruct
{
    /// <summary>
    /// UserAuthorization instance used to acquire tokens.
    /// </summary>
    public required UserAuthorization UserAuthorization { get; set;  }

    /// <summary>
    /// ITurnContext instance used to acquire tokens.
    /// </summary>
    public required ITurnContext TurnContext { get; set;  }

    /// <summary>
    /// Handler name to use with the UserAuthorization system.
    /// </summary>
    public required string AuthHandlerName { get; set; }

    /// <summary>
    /// Connection name, if applicable, to use for the exchange. 
    /// </summary>
    public string? ConnectionName { get; set; }
}
