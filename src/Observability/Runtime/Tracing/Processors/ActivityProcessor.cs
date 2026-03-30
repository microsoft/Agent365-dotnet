// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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
        private static readonly string[] AttributeKeys = new[]
        {
            OpenTelemetryConstants.GenAiAgentIdKey,
            OpenTelemetryConstants.GenAiAgentNameKey,
            OpenTelemetryConstants.GenAiAgentDescriptionKey,
            OpenTelemetryConstants.AgentEmailKey,
            OpenTelemetryConstants.AgentBlueprintIdKey,
            OpenTelemetryConstants.AgentAUIDKey,
            OpenTelemetryConstants.AgentPlatformIdKey,
            OpenTelemetryConstants.TenantIdKey,
            OpenTelemetryConstants.GenAiConversationIdKey,
            OpenTelemetryConstants.GenAiConversationItemLinkKey,
            OpenTelemetryConstants.GenAiInputMessagesKey,
            OpenTelemetryConstants.GenAiOutputMessagesKey,
            OpenTelemetryConstants.GenAiToolCallResultKey,
            OpenTelemetryConstants.GenAiToolNameKey,
            OpenTelemetryConstants.GenAiToolCallIdKey,
            OpenTelemetryConstants.GenAiToolDescriptionKey,
            OpenTelemetryConstants.GenAiToolArgumentsKey,
            OpenTelemetryConstants.GenAiToolTypeKey,
            OpenTelemetryConstants.GenAiProviderNameKey,
            OpenTelemetryConstants.SessionIdKey,
            OpenTelemetryConstants.SessionDescriptionKey,
            OpenTelemetryConstants.ChannelNameKey,
            OpenTelemetryConstants.ChannelLinkKey,
            OpenTelemetryConstants.UserIdKey,
            OpenTelemetryConstants.UserNameKey,
            OpenTelemetryConstants.UserEmailKey,
            OpenTelemetryConstants.CallerClientIpKey,
        };

        private static readonly string[] InvokeAgentAttributeKeys = new[]
        {
            OpenTelemetryConstants.ServerAddressKey,
            OpenTelemetryConstants.ServerPortKey,
        };

        /// <summary>
        /// Called when an activity starts, adds tags for attributes listed in AttributeKeys.
        /// </summary>
        /// <param name="activity">The activity that is starting.</param>
        public override void OnStart(Activity activity)
        {
            // Set telemetry SDK attributes
            activity.CoalesceTag(OpenTelemetryConstants.TelemetrySdkNameKey, OpenTelemetryConstants.TelemetrySdkNameValue);
            activity.CoalesceTag(OpenTelemetryConstants.TelemetrySdkLanguageKey, OpenTelemetryConstants.TelemetrySdkLanguageValue);
            activity.CoalesceTag(OpenTelemetryConstants.TelemetrySdkVersionKey, OpenTelemetryConstants.TelemetrySdkVersionValue);

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
