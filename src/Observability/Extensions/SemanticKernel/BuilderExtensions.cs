// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Runtime;
using Microsoft.SemanticKernel;
using OpenTelemetry.Trace;
using OpenTelemetry;

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
            if (builder.Configuration != null
                && !string.IsNullOrEmpty(builder.Configuration["EnableOtlpExporter"])
                && bool.TryParse(builder.Configuration["EnableOtlpExporter"], out bool enabled) && enabled)
            {
                builder.Services.AddOpenTelemetry()
                    .WithTracing(tracing => tracing
                        .AddSource(SemanticKernelTelemetryConstants.SemanticKernelSourceWildcard)
                        .AddProcessor(new SemanticKernelSpanProcessor()))
                    .UseOtlpExporter();
            }
            else
            {
                builder.Services.AddOpenTelemetry()
                    .WithTracing(tracing => tracing
                        .AddSource(SemanticKernelTelemetryConstants.SemanticKernelSourceWildcard)
                        .AddProcessor(new SemanticKernelSpanProcessor()));
            }
        }

        return builder;
    }
}