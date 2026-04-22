// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using System.Diagnostics;

internal class SemanticKernelSpanProcessor : BaseProcessor<Activity>
{
    private static readonly string TargetSourceName = SemanticKernelTelemetryConstants.SemanticKernelSource;
    private readonly bool _suppressInvokeAgentInput;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticKernelSpanProcessor"/> class.
    /// </summary>
    /// <param name="configuration">The configuration instance for accessing settings.</param>
    public SemanticKernelSpanProcessor(IConfiguration? configuration = null)
    {
        this._suppressInvokeAgentInput = configuration != null && bool.TryParse(configuration[SemanticKernelTelemetryConstants.SuppressInvokeAgentInputConfigKey], out var suppress) && suppress;
    }

    public override void OnStart(Activity activity)
    {
    }

    public override void OnEnd(Activity activity)
    {
        if (activity.Source.Name.StartsWith(TargetSourceName))
        {
            var operationName = activity.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string;
            if (operationName != null)
            {
                switch (operationName)
                {
                    case SemanticKernelTelemetryConstants.InvokeAgentOperation:
                        SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity: activity, suppressInvocationInput: this._suppressInvokeAgentInput);
                        break;

                    case SemanticKernelTelemetryConstants.ExecuteToolOperation:
                        // Span emitted by SK SDK follows Microsoft Agent 365 schema, so no modification needed.
                        // FunctionInvocationFilter already adds other relevant tags.
                        // Placeholder for any plumbing if needed in the future.
                        break;

                    case SemanticKernelTelemetryConstants.ChatCompletionsOperation:
                    case SemanticKernelTelemetryConstants.ChatOperation:
                        activity.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, InferenceOperationType.Chat.ToString());
                        activity.DisplayName = activity.DisplayName
                            .Replace(SemanticKernelTelemetryConstants.ChatCompletionsOperation, InferenceOperationType.Chat.ToString())
                            .Replace(SemanticKernelTelemetryConstants.ChatOperation, InferenceOperationType.Chat.ToString());

                        var inputMessages = SemanticKernelMessageMapper.MapInputMessages(activity);
                        if (inputMessages != null)
                        {
                            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);
                        }

                        var outputMessages = SemanticKernelMessageMapper.MapOutputMessages(activity);
                        if (outputMessages != null)
                        {
                            activity.SetTag(OpenTelemetryConstants.GenAiOutputMessagesKey, outputMessages);
                        }
                        break;
                }
            }
        }
    }
}
