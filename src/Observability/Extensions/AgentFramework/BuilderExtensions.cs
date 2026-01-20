// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
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
    /// <param name="additionalSources">Optional additional activity source names to include in tracing.</param>
    /// <returns>The configured builder for method chaining.</returns>
    public static Builder WithAgentFramework(this Builder builder, bool enableRelatedSources = true, params string[] additionalSources)
    {
        if (enableRelatedSources)
        {
            var telmConfig = builder.Services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource(AgentFrameworkSource)
                        .AddSource(AgentFrameworkAgentSource)
                        .AddSource(AgentFrameworkChatClientSource)
                        .AddProcessor(new AgentFrameworkSpanProcessor(additionalSources));

                    // Add any custom sources provided by the caller
                    foreach (var source in additionalSources)
                    {
                        if (!string.IsNullOrWhiteSpace(source))
                        {
                            tracing.AddSource(source);
                        }
                    }
                });

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