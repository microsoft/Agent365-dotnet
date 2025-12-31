// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry;
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
                var operationName = activity.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey);
                if (operationName is string opName && opName == ExecuteToolOperation)
                {
                    var toolCallResult = activity.GetTagItem(ToolCallResultTag);
                    activity.SetTag(EventContentTag, toolCallResult);
                }
            }
        }
    }
}
