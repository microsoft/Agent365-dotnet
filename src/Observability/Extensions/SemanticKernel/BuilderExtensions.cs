namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability;
using Microsoft.SemanticKernel;
using OpenTelemetry.Trace;

/// <summary>
/// Extension methods for configuring Builder with SemanticKernel integration.
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    /// Adds SemanticKernel integration to the builder with function invocation filtering.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="enableRelatedSources">Whether to enable related tracing sources for OpenTelemetry.</param>
    /// <returns>The configured builder for method chaining.</returns>
    public static Builder WithSemanticKernel(this Builder builder, bool enableRelatedSources = true)
    {
        builder.Services.AddSingleton<IFunctionInvocationFilter, FunctionInvocationFilter>();
        if (enableRelatedSources)
        {
            AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource(SemanticKernelTelemetryConstants.SemanticKernelSourceWildcard)
                    .AddProcessor(new SemanticKernelSpanProcessor()));
        }

        return builder;
    }
}