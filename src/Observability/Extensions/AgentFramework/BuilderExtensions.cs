// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Extensions.AgentFramework;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Runtime;
using OpenTelemetry.Trace;
using OpenTelemetry;

/// <summary>
/// Extension methods for configuring Builder with Agent Framework integration.
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    /// The activity source name for Agent Framework tracing.
    /// </summary>
    public const string AgentFrameworkSource = "Experimental.Microsoft.Agents.AI";

    /// <summary>
    /// The activity source name for Agent Framework agent tracing.
    /// </summary>
    public const string AgentFrameworkAgentSource = "Experimental.Microsoft.Agents.AI.Agent";

    /// <summary>
    /// The activity source name for Agent Framework chat client tracing.
    /// </summary>
    public const string AgentFrameworkChatClientSource = "Experimental.Microsoft.Agents.AI.ChatClient";

    /// <summary>
    /// Adds Agent Framework integration to the builder.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="enableRelatedSources">If true, enables Agent Framework activity source tracing for OpenTelemetry.</param>
    /// <returns>The configured builder for method chaining.</returns>
    public static Builder WithAgentFramework(this Builder builder, bool enableRelatedSources = true)
    {
        if (enableRelatedSources)
        {
            var telmConfig = builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource(AgentFrameworkSource)
                    .AddSource(AgentFrameworkAgentSource)
                    .AddSource(AgentFrameworkChatClientSource)
                    .AddProcessor(new AgentFrameworkSpanProcessor()));

            if (builder.Configuration != null
                && !string.IsNullOrEmpty(builder.Configuration["EnableOtlpExporter"])
                && bool.TryParse(builder.Configuration["EnableOtlpExporter"], out bool enabled) && enabled)
            {
                telmConfig.UseOtlpExporter();
            }
        }

        return builder;
    }
}