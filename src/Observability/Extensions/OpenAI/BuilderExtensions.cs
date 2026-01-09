// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.OpenAI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Runtime;
using OpenTelemetry.Trace;

/// <summary>
/// Extension methods for configuring Builder with OpenAI integration.
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    /// Adds OpenAI integration to the builder.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="enableRelatedSources">Whether to enable related tracing sources for OpenTelemetry.</param>
    /// <param name="options">Configuration options for OpenAI span processing. If null, default options will be used.</param>
    /// <returns>The configured builder for method chaining.</returns>
    public static Builder WithOpenAI(this Builder builder, bool enableRelatedSources = true, OpenAISpanProcessorOptions? options = null)
    {
        if (enableRelatedSources)
        {
            AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource(OpenAITelemetryConstants.OpenAISourceWildcard)
                    .AddProcessor(new OpenAISpanProcessor(options ?? new OpenAISpanProcessorOptions())));
        }

        return builder;
    }
}