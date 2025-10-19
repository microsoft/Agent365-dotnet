// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel;

/// <summary>
/// Factory for creating governance delegates that can be executed on cache misses.
/// This breaks the circular dependency between KernelProvider and GovernanceExtensions.
/// </summary>
public interface IGovernanceDelegateFactory
{
    /// <summary>
    /// Creates a governance delegate that can be executed on kernel cache misses.
    /// </summary>
    /// <param name="logger">Optional logger for audit trail</param>
    /// <returns>Governance delegate function</returns>
    Func<Kernel, Task> CreateGovernanceDelegate(ILogger? logger = null);
}