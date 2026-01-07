// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using Microsoft.Agents.A365.Runtime;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.ChatCompletion;
    using System.Threading;
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

        /// <summary>
        /// Sends chat history to the MCP platform for real-time threat protection.
        /// </summary>
        /// <param name="turnContext">The turn context containing conversation information.</param>
        /// <param name="chatHistory">The chat history to send.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
        Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistory chatHistory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends chat history to the MCP platform for real-time threat protection.
        /// </summary>
        /// <param name="turnContext">The turn context containing conversation information.</param>
        /// <param name="chatHistory">The chat history to send.</param>
        /// <param name="toolOptions">Tool options for sending chat history.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
        Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistory chatHistory, ToolOptions toolOptions, CancellationToken cancellationToken = default);
    }
}
