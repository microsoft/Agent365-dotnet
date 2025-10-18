// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors
{
    using Microsoft.Agents.A365.Observability.Runtime.Common;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
    using OpenTelemetry;
    using System.Diagnostics;

    /// <summary>
    /// Processes activity telemetry data by adding contextual baggage information.
    /// </summary>
    public sealed class ActivityProcessor : BaseProcessor<Activity>
    {
        /// <summary>
        /// Called when an activity starts, adding baggage tags for agent context, tenant context, and conversation context.
        /// </summary>
        /// <param name="activity">The activity that is starting.</param>
        public override void OnStart(Activity activity)
        {
            activity.CoalesceTag(OpenTelemetryConstants.GenAiAgentIdKey, Baggage.Current.GetBaggage(OpenTelemetryConstants.GenAiAgentIdKey));
            activity.CoalesceTag(OpenTelemetryConstants.TenantIdKey, Baggage.Current.GetBaggage(OpenTelemetryConstants.TenantIdKey));
            activity.CoalesceTag(OpenTelemetryConstants.GenAiConversationIdKey, Baggage.Current.GetBaggage(OpenTelemetryConstants.GenAiConversationIdKey));
            base.OnStart(activity);
        }
    }
}
