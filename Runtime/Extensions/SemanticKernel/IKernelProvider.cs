// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.SemanticKernel;

namespace Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel
{
    /// <summary>
    /// Provides access to <see cref="Kernel"/> instances for specific tenants and workers.
    /// </summary>
    public interface IKernelProvider
    {
        /// <summary>
        /// Gets a <see cref="Kernel"/> instance for the specified tenant and worker.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="workerId">The worker identifier.</param>
        /// <returns>A <see cref="Kernel"/> instance.</returns>
        Kernel GetKernel(string tenantId, string workerId);
    }
}