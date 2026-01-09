// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.OpenAI;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry;
using System.Diagnostics;
using System.Linq;

internal class OpenAISpanProcessor : BaseProcessor<Activity>
{
    private static readonly string TargetSourceName = OpenAITelemetryConstants.OpenAISource;
    private readonly OpenAISpanProcessorOptions _options;

    public OpenAISpanProcessor(OpenAISpanProcessorOptions options)
    {
        _options = options ?? new OpenAISpanProcessorOptions();
    }

    public override void OnStart(Activity activity)
    {
    }

    public override void OnEnd(Activity activity)
    {
        if (activity.Source.Name.StartsWith(TargetSourceName))
        {
            var tags = activity.Tags.ToDictionary(kv => kv.Key, kv => kv.Value);
            if (tags.TryGetValue(OpenTelemetryConstants.GenAiOperationNameKey, out var operationName))
            {
                switch (operationName)
                {
                    case OpenAITelemetryConstants.ChatOperation:
                        // Span emitted by OpenAI SDK follows Microsoft Agent 365 schema, so no modification needed.
                        // Placeholder for any plumbing if needed in the future.
                        break;
                }
            }
        }

        // Remove prompt data from InvokeAgent scopes if configured
        if (!_options.SendPromptInInvokeAgentScopes)
        {
            if (activity.OperationName == "invoke_agent" ||
                (activity.DisplayName != null && activity.DisplayName.StartsWith("invoke_agent")))
            {
                // Remove the gen_ai.input.messages tag to prevent sending prompt content
                var tagToRemove = activity.Tags.FirstOrDefault(tag => tag.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
                if (!string.IsNullOrEmpty(tagToRemove.Key))
                {
                    activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, null);
                }
            }
        }
    }
}
