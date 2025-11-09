// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.SemanticKernel;

    /// <summary>
    /// Provides methods for managing tools in the Semantic Kernel.
    /// </summary>
    public interface IMcpToolRegistrationService
    {
        /// <summary>
        /// Adds the A365 MCP Tool Servers
        /// </summary>
        /// <param name="kernel">The kernel to which the tools will be added.</param>
        /// <param name="environmentId">Environment Id for the environment</param>
        /// <param name="userAuthorization"></param>
        /// <param name="turnContext"></param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <param name="configuration"></param>
        /// <returns>Returns a new object of the kernel</returns>
        /// <exception cref="ArgumentNullException"></exception>
        Task AddToolServersToAgentAsync(Kernel kernel, IConfiguration configuration, string environmentId, UserAuthorization userAuthorization, ITurnContext turnContext, string? authToken = null);
    }
}
