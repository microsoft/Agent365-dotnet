// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Agents.A365.Observability.Contracts;
using Microsoft.Agents.A365.Observability.Contracts.Details;
using OpenTelemetry;
using System.Diagnostics;
using System.Linq;

internal class SemanticKernelSpanProcessor : BaseProcessor<Activity>
{
    private static readonly string TargetSourceName = SemanticKernelTelemetryConstants.SemanticKernelSource;

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
                    case SemanticKernelTelemetryConstants.InvokeAgentOperation:
                        // Span emitted by SK SDK follows Microsoft Agent 365 schema, so no modification needed.
                        // Placeholder for any plumbing if needed in the future.
                        break;

                    case SemanticKernelTelemetryConstants.ExecuteToolOperation:
                        // Span emitted by SK SDK follows Microsoft Agent 365 schema, so no modification needed.
                        // FunctionInvocationFilter already adds other relevant tags.
                        // Placeholder for any plumbing if needed in the future.
                        break;

                    case SemanticKernelTelemetryConstants.ChatCompletionsOperation:
                        activity.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, InferenceOperationType.Chat.ToString());
                        activity.DisplayName = activity.DisplayName.ToString().Replace(SemanticKernelTelemetryConstants.ChatCompletionsOperation, InferenceOperationType.Chat.ToString());
                        // Other tags set by SK SDK follow Microsoft Agent 365 schema.
                        break;
                }
            }
        }
    }
}
