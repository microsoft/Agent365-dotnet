// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Text.Json.Nodes;
using OpenAI.Chat;

namespace Microsoft.Agents.A365.Runtime.Extensions.OpenAI
{
    /// <summary>
    /// Provides access to OpenAI functions/tools for specific tenants and workers.
    /// </summary>
    public interface IOpenAIFunctionProvider
    {
        /// <summary>
        /// Gets the available tools for the specified tenant and worker.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="workerId">The worker identifier.</param>
        /// <returns>A list of available <see cref="ChatTool"/> instances.</returns>
        List<ChatTool> GetAvailableTools(string tenantId, string workerId);

        /// <summary>
        /// Executes a function for the specified tenant and worker.
        /// </summary>
        /// <param name="functionName">The name of the function to execute.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="workerId">The worker identifier.</param>
        /// <param name="arguments">Optional function arguments as JSON.</param>
        /// <returns>A task representing the function execution result.</returns>
        Task<string> ExecuteFunctionAsync(string functionName, string tenantId, string workerId, JsonNode? arguments = null);
    }
}