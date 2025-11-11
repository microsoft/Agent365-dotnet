// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using OpenTelemetry;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

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
                        // Span emitted by SK SDK follows Microsoft Agents A365 schema, so no modification needed.
                        // Placeholder for any plumbing if needed in the future.
                        break;

                    case SemanticKernelTelemetryConstants.ExecuteToolOperation:
                        // Span emitted by SK SDK follows Microsoft Agents A365 schema, so no modification needed.
                        // FunctionInvocationFilter already adds other relevant tags.
                        // Placeholder for any plumbing if needed in the future.
                        break;

                    case SemanticKernelTelemetryConstants.ChatCompletionsOperation:
                        var chatOperationName = GetEnumMemberValue(InferenceOperationType.Chat);
                        activity.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, chatOperationName);
                        activity.DisplayName = activity.DisplayName.ToString().Replace(SemanticKernelTelemetryConstants.ChatCompletionsOperation, chatOperationName);
                        // Other tags set by SK SDK follow Microsoft Agents A365 schema.
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Gets the string value for an InferenceOperationType enum member.
    /// </summary>
    private static string GetEnumMemberValue(InferenceOperationType operationType)
    {
        var memberInfo = typeof(InferenceOperationType).GetMember(operationType.ToString()).FirstOrDefault();
        var enumMemberAttribute = memberInfo?.GetCustomAttribute<EnumMemberAttribute>();
        return enumMemberAttribute?.Value ?? operationType.ToString();
    }
}
