// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

namespace Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel;

/// <summary>
/// Implementation of governance delegate factory that creates expensive governance logic
/// to be executed only on kernel cache misses for performance optimization.
/// </summary>
public class GovernanceDelegateFactory : IGovernanceDelegateFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GovernanceDelegateFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used for dependency resolution.</param>
    public GovernanceDelegateFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a governance delegate that can be passed to KernelProvider for execution only on cache misses.
    /// This optimizes performance by running expensive governance logic only when new kernels are created.
    /// </summary>
    /// <param name="logger">Optional logger for audit trail</param>
    /// <returns>Governance delegate function</returns>
    public Func<Kernel, Task> CreateGovernanceDelegate(ILogger? logger = null)
    {
        return async (kernel) =>
        {
            try
            {
                logger?.LogInformation("=== APPLYING KERNEL FUNCTION GOVERNANCE (Cache Miss) ===");

                // Add governance filter for all function invocations
                kernel.FunctionInvocationFilters.Add(new FunctionInvocationFilter());

                // Add any governance at the kernel level if needed. Refer to A365POC repo for examples.

                // Audit trail
                await LogGovernanceAuditTrail(
                    logger,
                    kernel.Plugins.Count,
                    kernel.Plugins.SelectMany(p => p).Count());

                logger?.LogInformation("=== KERNEL FUNCTION GOVERNANCE APPLIED SUCCESSFULLY (Cache Miss) ===");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to apply kernel function governance on cache miss");
                throw;
            }
        };
    }

    /// <summary>
    /// Log detailed audit trail of governance decisions
    /// </summary>
    private static Task LogGovernanceAuditTrail(
        ILogger? logger,
        int postGovernancePluginCount,
        int postGovernanceFunctionCount)
    {
        if (logger == null) return Task.CompletedTask;

        return Task.Run(() =>
        {

            logger.LogInformation("=== GOVERNANCE AUDIT TRAIL ===");

            // Log plugin and function counts after governance
            logger.LogInformation("Post-Governance Plugin Count: {Count}", postGovernancePluginCount);
            logger.LogInformation("Post-Governance Function Count: {Count}", postGovernanceFunctionCount);

        });
    }
}