// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.SemanticKernel;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods for managing tools in the Semantic Kernel.
    /// </summary>
    public interface IMcpToolRegistrationService
    {
        /// <summary>
        /// Adds the A365 MCP Tool Servers
        /// </summary>
        /// <param name="kernel">The kernel to which the tools will be added.</param>
        /// <param name="userAuthorization">Agents SDK UserAuthorization System</param>
        /// <param name="authHandlerName">Authentication Handler Name for use with the UserAuthorization System</param>
        /// <param name="turnContext"></param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <returns>Returns a new object of the kernel</returns>
        /// <exception cref="ArgumentNullException"></exception>
        Task AddToolServersToAgentAsync(Kernel kernel, UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? authToken = null);
    }
}
