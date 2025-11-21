// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;
using OpenTelemetry;
using System.Diagnostics;
using System.Linq;

internal class SemanticKernelSpanProcessor : BaseProcessor<Activity>
{
    private static readonly string TargetSourceName = SemanticKernelTelemetryConstants.SemanticKernelSource;

    const string GenAiUserMessageEventName = "gen_ai.user.message";
    const string GenAiChoiceEventName = "gen_ai.choice";
    const string GenAiInputMessagesTagKey = "gen_ai.input.messages";
    const string GenAiOutputMessagesTagKey = "gen_ai.output.messages";

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
                        SemanticKernelSpanProcessorHelper.ProcessInvocationInputTag(activity);
                        break;

                    case SemanticKernelTelemetryConstants.ExecuteToolOperation:
                        // Span emitted by SK SDK follows Microsoft Agents A365 schema, so no modification needed.
                        // FunctionInvocationFilter already adds other relevant tags.
                        // Placeholder for any plumbing if needed in the future.
                        break;

                    case SemanticKernelTelemetryConstants.ChatCompletionsOperation:
                        activity.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, InferenceOperationType.Chat.ToString());
                        activity.DisplayName = activity.DisplayName.ToString().Replace(SemanticKernelTelemetryConstants.ChatCompletionsOperation, InferenceOperationType.Chat.ToString());

                        var userAndChoiceMessages = SemanticKernelSpanProcessorHelper.GetGenAiUserAndChoiceMessageContent(activity);
                        if (userAndChoiceMessages[GenAiUserMessageEventName] is List<string> userMessages && userMessages.Count > 0)
                        {
                            activity.SetTag(GenAiInputMessagesTagKey, string.Join(", ", userMessages));
                        }
                        if (userAndChoiceMessages[GenAiChoiceEventName] is List<string> choiceMessages && choiceMessages.Count > 0)
                        {
                            activity.SetTag(GenAiOutputMessagesTagKey, string.Join(", ", choiceMessages));
                        }
                        // Other tags set by SK SDK follow Microsoft Agent A365 schema.
                        break;
                }
            }
        }
    }
}
