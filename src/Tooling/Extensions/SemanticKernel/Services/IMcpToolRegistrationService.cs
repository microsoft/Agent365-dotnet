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
        /// <exception cref="InvalidOperationException">
        /// Another <see cref="AddToolServersToAgentAsync"/> or <see cref="RemoveToolServersAsync"/> call for <paramref name="kernel"/> is in progress.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Each MCP plugin calls its server through an MCP client that sends the request headers, including the
        /// <c>Authorization</c> token, of the call that added it. MCP plugins added to <paramref name="kernel"/> by an earlier call,
        /// including plugins carried over by <c>Kernel.Clone()</c>, are removed before this call adds its own, and the MCP clients
        /// of the plugins that an earlier call added to <paramref name="kernel"/> itself are disposed. Plugins added by the caller are kept.
        /// </para>
        /// <para>
        /// Register tools for each turn, and call <see cref="RemoveToolServersAsync"/> when the turn ends, for example in a
        /// <c>finally</c> block. A common pattern is to clone a base kernel for each message, register the tools on the copy,
        /// run the turn, and then remove the tools from the copy. Turns that run concurrently must each use their own kernel.
        /// </para>
        /// <para>
        /// Use the MCP functions only through the plugins that this method adds. Don't copy them into other plugins or kernels:
        /// a copy keeps calling the server through the client of the call that created it, and fails once that client is disposed.
        /// </para>
        /// </remarks>
        Task AddToolServersToAgentAsync(Kernel kernel, UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? authToken = null);

        /// <summary>
        /// Removes the MCP plugins that <see cref="AddToolServersToAgentAsync"/> added to the kernel, and disposes the MCP clients that serve them.
        /// </summary>
        /// <param name="kernel">The kernel to remove the MCP plugins from.</param>
        /// <returns>A task that completes when the plugins are removed and their MCP clients are disposed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="kernel"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Another <see cref="AddToolServersToAgentAsync"/> or <see cref="RemoveToolServersAsync"/> call for <paramref name="kernel"/> is in progress.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Call it when the turn that registered the tools ends, for example in a <c>finally</c> block. Disposing the MCP clients
        /// closes their connections and ends their MCP sessions. Plugins added by the caller are kept.
        /// </para>
        /// <para>
        /// The MCP plugins are removed from <paramref name="kernel"/> only. Their MCP clients are disposed only if the plugins were
        /// added to <paramref name="kernel"/> itself: for a <c>Kernel.Clone()</c> copy, the plugins it shares with the kernel it was
        /// cloned from are removed from the copy but keep working on that kernel.
        /// </para>
        /// <para>
        /// The default implementation removes the MCP plugins added by <see cref="McpToolRegistrationService"/>.
        /// </para>
        /// </remarks>
        Task RemoveToolServersAsync(Kernel kernel) => McpToolRegistrationService.RemoveToolServersFromKernelAsync(kernel);

        /// <summary>
        /// Sends chat history to the MCP platform for real-time threat protection.
        /// </summary>
        /// <param name="turnContext">The turn context containing conversation information.</param>
        /// <param name="chatHistory">The chat history to send.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="turnContext"/> or <paramref name="chatHistory"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
        /// <remarks>
        /// Note: The <see cref="ChatHistory"/> class does not include timestamp information for individual messages.
        /// As a result, all messages in the converted chat history will be timestamped with the current UTC time at the moment of conversion.
        /// Original message creation times are not preserved.
        /// </remarks>
        Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistory chatHistory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends chat history to the MCP platform for real-time threat protection.
        /// </summary>
        /// <param name="turnContext">The turn context containing conversation information.</param>
        /// <param name="chatHistory">The chat history to send.</param>
        /// <param name="toolOptions">Tool options for sending chat history.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="turnContext"/>, <paramref name="chatHistory"/>, or <paramref name="toolOptions"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
        /// <remarks>
        /// Note: The <see cref="ChatHistory"/> class does not include timestamp information for individual messages.
        /// As a result, all messages in the converted chat history will be timestamped with the current UTC time at the moment of conversion.
        /// Original message creation times are not preserved.
        /// </remarks>
        Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistory chatHistory, ToolOptions toolOptions, CancellationToken cancellationToken = default);
    }
}
