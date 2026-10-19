// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.OpenAI;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using OpenTelemetry;
using System.Diagnostics;
using System.Linq;

internal class OpenAISpanProcessor : BaseProcessor<Activity>
{
    private static readonly string TargetSourceName = OpenAITelemetryConstants.OpenAISource;

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
                        ModelObservationSchema.NormalizeChatSpan(activity);
                        break;
                }
            }
        }
    }
}
