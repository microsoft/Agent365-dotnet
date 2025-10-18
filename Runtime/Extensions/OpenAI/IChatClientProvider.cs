// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using OpenAI.Chat;

namespace Microsoft.Agents.A365.Runtime.Extensions.OpenAI
{
    /// <summary>
    /// Provides access to <see cref="ChatClient"/> instances for specific tenants and workers.
    /// </summary>
    public interface IChatClientProvider
    {
        /// <summary>
        /// Gets a <see cref="ChatClient"/> instance for the specified tenant and worker.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="workerId">The worker identifier.</param>
        /// <returns>A <see cref="ChatClient"/> instance.</returns>
        ChatClient GetChatClient(string tenantId, string workerId);
    }
}