// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors
{
    using Microsoft.Agents.A365.Observability.Contracts.Details;
    using Microsoft.Agents.A365.Observability.Runtime.Common;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
    using OpenTelemetry;
    using System.Diagnostics;
    using static Microsoft.Agents.A365.Observability.Contracts.OpenTelemetryConstants;

    /// <summary>
    /// Processes activity telemetry data by adding contextual baggage information.
    /// </summary>
    public sealed class ActivityProcessor : BaseProcessor<Activity>
    {
        private static readonly string[] AttributeKeys = new[]
        {
            GenAiAgentIdKey,
            GenAiAgentNameKey,
            GenAiAgentDescriptionKey,
            GenAiAgentUPNKey,
            GenAiAgentBlueprintIdKey,
            GenAiAgentAUIDKey,
            TenantIdKey,
            GenAiConversationIdKey,
            GenAiConversationItemLinkKey,
            CorrelationIdKey,
            OperationSourceKey,
            GenAiInputMessagesKey,
            GenAiOutputMessagesKey,
            GenAiEventContent,
            GenAiToolNameKey,
            GenAiToolCallIdKey,
            GenAiToolDescriptionKey,
            GenAiToolArgumentsKey,
            GenAiToolTypeKey,
            GenAiProviderNameKey,
            GenAiSystemKey
        };

        private static readonly string[] InvokeAgentAttributeKeys = new[]
        {
            GenAiCallerIdKey,
            GenAiCallerNameKey,
            GenAiCallerUpnKey,
            GenAiCallerUserIdKey,
            GenAiCallerTenantIdKey,
            GenAiExecutionTypeKey,
            GenAiChannelNameKey,
            GenAiChannelLinkKey
        };

        /// <summary>
        /// Called when an activity starts, adds tags for attributes listed in AttributeKeys.
        /// </summary>
        /// <param name="activity">The activity that is starting.</param>
        public override void OnStart(Activity activity)
        {
            activity.CoalesceTag(OperationSourceKey, Baggage.Current.GetBaggage(OperationSourceKey), OperationSource.SDK.ToString());

            foreach (var key in AttributeKeys)
            {
                activity.CoalesceTag(key, Baggage.Current.GetBaggage(key));
            }

            if (activity.OperationName == InvokeAgentScope.OperationName ||
                (activity.DisplayName != null && activity.DisplayName.StartsWith(InvokeAgentScope.OperationName)))
            {
                foreach (var key in InvokeAgentAttributeKeys)
                {
                    activity.CoalesceTag(key, Baggage.Current.GetBaggage(key));
                }
            }

            base.OnStart(activity);
        }
    }
}
