using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Microsoft.Agents.A365.Observability.Extensions.AgentFramework
{
    internal class AgentFrameworkSpanProcessor : BaseProcessor<Activity>
    {
        private const string ExecuteToolOperation = "execute_tool";
        private const string ToolCallResultTag = "gen_ai.tool.call.result";
        private const string EventContentTag = "gen_ai.event.content";

        public override void OnStart(Activity activity)
        {
        }

        public override void OnEnd(Activity activity)
        {
            if (activity == null)
                return;

            if (activity.Source.Name.StartsWith(BuilderExtensions.AgentFrameworkSource))
            {
                var tags = activity.Tags.ToDictionary(kv => kv.Key, kv => kv.Value);
                if (tags.TryGetValue(OpenTelemetryConstants.GenAiOperationNameKey, out var operationName))
                {
                    switch (operationName)
                    {
                        case ExecuteToolOperation:
                            var toolCallResult = activity.GetTagItem(ToolCallResultTag);
                            if (toolCallResult != null)
                            {
                                activity.SetTag(EventContentTag, toolCallResult);
                            }
                            break;
                    }
                }
            }
            
        }
    }
}
