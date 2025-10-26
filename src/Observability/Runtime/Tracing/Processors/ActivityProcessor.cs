// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors
{
    using Microsoft.Agents.A365.Observability.Runtime.Common;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
    using OpenTelemetry;
    using System.Diagnostics;

    /// <summary>
    /// Processes activity telemetry data by adding contextual baggage information.
    /// </summary>
    public sealed class ActivityProcessor : BaseProcessor<Activity>
    {
        private static readonly string[] AttributeKeys = new[]
        {
            OpenTelemetryConstants.GenAiAgentIdKey,
            OpenTelemetryConstants.GenAiAgentNameKey,
            OpenTelemetryConstants.GenAiAgentDescriptionKey,
            OpenTelemetryConstants.GenAiAgentUPNKey,
            OpenTelemetryConstants.GenAiAgentBlueprintIdKey,
            OpenTelemetryConstants.GenAiAgentAUIDKey,
            OpenTelemetryConstants.TenantIdKey,
            OpenTelemetryConstants.GenAiConversationIdKey,
            OpenTelemetryConstants.GenAiConversationItemLinkKey,
            OpenTelemetryConstants.CorrelationIdKey,
            OpenTelemetryConstants.OperationSourceKey,
            OpenTelemetryConstants.GenAiInputMessagesKey,
            OpenTelemetryConstants.GenAiOutputMessagesKey,
            OpenTelemetryConstants.GenAiEventContent,
            OpenTelemetryConstants.GenAiToolNameKey,
            OpenTelemetryConstants.GenAiToolCallIdKey,
            OpenTelemetryConstants.GenAiToolDescriptionKey,
            OpenTelemetryConstants.GenAiToolArgumentsKey,
            OpenTelemetryConstants.GenAiToolTypeKey,
            OpenTelemetryConstants.GenAiProviderNameKey,
            OpenTelemetryConstants.GenAiSystemKey
        };

        private static readonly string[] InvokeAgentAttributeKeys = new[]
        {
            OpenTelemetryConstants.GenAiCallerIdKey,
            OpenTelemetryConstants.GenAiCallerNameKey,
            OpenTelemetryConstants.GenAiCallerUpnKey,
            OpenTelemetryConstants.GenAiCallerUserIdKey,
            OpenTelemetryConstants.GenAiCallerTenantIdKey,
            OpenTelemetryConstants.GenAiExecutionTypeKey,
            OpenTelemetryConstants.GenAiExecutionSourceIdKey,
            OpenTelemetryConstants.GenAiExecutionSourceNameKey,
            OpenTelemetryConstants.GenAiExecutionSourceDescriptionKey
        };

        /// <summary>
        /// Called when an activity starts, adds tags for attributes listed in AttributeKeys.
        /// </summary>
        /// <param name="activity">The activity that is starting.</param>
        public override void OnStart(Activity activity)
        {
            activity.CoalesceTag(OpenTelemetryConstants.OperationSourceKey, Baggage.Current.GetBaggage(OpenTelemetryConstants.OperationSourceKey), OperationSource.SDK.ToString());

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
