// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.SemanticKernel.Agents;

/// <summary>
/// Extension methods for enabling tracing on SemanticKernel agents.
/// </summary>
public static class ChatCompletionAgentExtensions
{
    /// <summary>
    /// Wraps a ChatCompletionAgent with tracing capabilities.
    /// </summary>
    /// <param name="agent">The ChatCompletionAgent to wrap.</param>
    /// <returns>A new TracingChatCompletionAgent that provides automatic tracing.</returns>
    public static ChatCompletionAgent WithTracing(this ChatCompletionAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var filters = agent.Kernel.FunctionInvocationFilters;
        if (!filters.OfType<FunctionInvocationFilter>().Any())
        {
            filters.Add(new FunctionInvocationFilter());
        }

        return agent;
    }
}