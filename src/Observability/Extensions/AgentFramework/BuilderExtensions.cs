namespace Microsoft.Agents.A365.Observability.Extensions.AgentFramework;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Runtime;
using OpenTelemetry.Trace;

/// <summary>
/// Extension methods for configuring Builder with OpenAI integration.
/// </summary>
public static class BuilderExtensions
{
    // Activity Source Names
    /// <summary>
    /// The activity source name for Agent Framework tracing.
    /// </summary>
    public const string AgentFrameworkSource = "Experimental.Microsoft.Agents.AI";

    /// <summary>
    /// The activity source name for Agent Framework tracing with wildcards.
    /// </summary>
    public const string AgentFrameworkSourceWildCard = "*Microsoft.Agents.AI";

    /// <summary>
    /// Adds OpenAI integration to the builder.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="enableRelatedSources">Whether to enable related tracing sources for OpenTelemetry.</param>
    /// <returns>The configured builder for method chaining.</returns>
    public static Builder WithAgentFramework(this Builder builder, bool enableRelatedSources = true)
    {
        if (enableRelatedSources)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource(AgentFrameworkSourceWildCard));
        }

        return builder;
    }
}