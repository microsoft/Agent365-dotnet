// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    #pragma warning disable CS1591 // XML documentation not required for constant definitions.
    /// <summary>
    /// OpenTelemetry constant keys and values used across the Microsoft Agent 365 SDK.
    /// </summary>
    public static class OpenTelemetryConstants
    {
        public const string EnableOpenTelemetrySwitch = "Azure.Experimental.EnableActivitySource";
        public const string SourceName = "Agent365Sdk";

        public const string ServerAddressKey = "server.address";
        public const string ServerPortKey = "server.port";
        public const string SessionIdKey = "microsoft.session.id";
        public const string SessionDescriptionKey = "microsoft.session.description";
        public const string TenantIdKey = "microsoft.tenant.id";

        public const string GenAiClientOperationDurationMetricName = "gen_ai.client.operation.duration";
        public const string GenAiRequestModelKey = "gen_ai.request.model";
        public const string GenAiResponseFinishReasonsKey = "gen_ai.response.finish_reasons";

        public const string GenAiConversationIdKey = "gen_ai.conversation.id";
        public const string GenAiConversationItemLinkKey = "microsoft.conversation.item.link";
        public const string GenAiUsageInputTokensKey = "gen_ai.usage.input_tokens";
        public const string GenAiUsageOutputTokensKey = "gen_ai.usage.output_tokens";
        public const string GenAiProviderNameKey = "gen_ai.provider.name";
        public const string GenAiInputMessagesKey = "gen_ai.input.messages";
        public const string GenAiOutputMessagesKey = "gen_ai.output.messages";
        public const string GenAiUserMessageEventName = "gen_ai.user.message";
        public const string GenAiChoiceEventName = "gen_ai.choice";
        public const string GenAiAgentInvocationInputKey = "gen_ai.agent.invocation_input";
        public const string GenAiAgentInvocationOutputKey = "gen_ai.agent.invocation_output";

        [DataContract]
        public enum OperationNames
        {
            [EnumMember(Value = "InvokeAgent")]
            InvokeAgent,

            [EnumMember(Value = "ExecuteInference")]
            ExecuteInference,

            [EnumMember(Value = "ExecuteTool")]
            ExecuteTool,

            [EnumMember(Value = "OutputMessages")]
            OutputMessages,

            [EnumMember(Value = "ApplyGuardrail")]
            ApplyGuardrail
        }

        /// <summary>
        /// The operation name value for apply_guardrail spans.
        /// </summary>
        public const string ApplyGuardrailOperationName = "apply_guardrail";

        // Channel dimensions (renamed from gen_ai.channel.* to microsoft.channel.*)
        public const string ChannelNameKey = "microsoft.channel.name";
        public const string ChannelLinkKey = "microsoft.channel.link";

        // Target agent dimensions
        public const string GenAiAgentIdKey = "gen_ai.agent.id";
        public const string GenAiAgentNameKey = "gen_ai.agent.name";
        public const string GenAiAgentDescriptionKey = "gen_ai.agent.description";
        public const string GenAiAgentVersionKey = "gen_ai.agent.version";
        public const string AgentAUIDKey = "microsoft.agent.user.id";
        public const string AgentEmailKey = "microsoft.agent.user.email";
        public const string AgentBlueprintIdKey = "microsoft.a365.agent.blueprint.id";
        public const string AgentPlatformIdKey = "microsoft.a365.agent.platform.id";

        // Human caller dimensions (OTel user.* namespace)
        public const string UserIdKey = "user.id";
        public const string UserEmailKey = "user.email";
        public const string UserNameKey = "user.name";
        public const string CallerClientIpKey = "client.address";

        // Caller agent dimensions (renamed from gen_ai.caller.agent.* to microsoft.a365.caller.agent.*)
        public const string CallerAgentNameKey = "microsoft.a365.caller.agent.name";
        public const string CallerAgentIdKey = "microsoft.a365.caller.agent.id";
        public const string CallerAgentBlueprintIdKey = "microsoft.a365.caller.agent.blueprint.id";
        public const string CallerAgentAUIDKey = "microsoft.a365.caller.agent.user.id";
        public const string CallerAgentEmailKey = "microsoft.a365.caller.agent.user.email";
        public const string CallerAgentPlatformIdKey = "microsoft.a365.caller.agent.platform.id";
        public const string CallerAgentVersionKey = "microsoft.a365.caller.agent.version";

        // Service attributes
        public const string ServiceNameKey = "service.name";

        // Telemetry SDK attributes
        public const string TelemetrySdkNameKey = "telemetry.sdk.name";
        public const string TelemetrySdkLanguageKey = "telemetry.sdk.language";
        public const string TelemetrySdkVersionKey = "telemetry.sdk.version";
        public const string TelemetrySdkNameValue = "A365ObservabilitySDK";
        public const string TelemetrySdkLanguageValue = "dotnet";
        
        /// <summary>
        /// Gets the telemetry SDK version dynamically from the assembly.
        /// </summary>
        public static string TelemetrySdkVersionValue => 
            typeof(OpenTelemetryConstants).Assembly.GetName().Version?.ToString() ?? "unknown";

        #region Public Constants
        /// <summary>
        ///  The GenAI operation name key.
        /// </summary>
        public const string GenAiOperationNameKey = "gen_ai.operation.name";
        
        /// <summary>
        /// The error message key.
        /// </summary>
        public const string ErrorMessageKey = "error.message";
        
        /// <summary>
        /// The error type key.
        /// </summary>
        public const string ErrorTypeKey = "error.type";

        #region tool call keys
        /// <summary>
        /// The GenAI tool name key.
        /// </summary>
        public const string GenAiToolNameKey = "gen_ai.tool.name";
        
        /// <summary>
        /// The GenAI tool call identifier key.
        /// </summary>
        public const string GenAiToolCallIdKey = "gen_ai.tool.call.id";
        
        /// <summary>
        /// The GenAI tool description key.
        /// </summary>
        public const string GenAiToolDescriptionKey = "gen_ai.tool.description";
        
        /// <summary>
        /// The GenAI tool call arguments key.
        /// </summary>
        public const string GenAiToolArgumentsKey = "gen_ai.tool.call.arguments";
        
        /// <summary>
        /// The GenAI tool type key.
        /// </summary>
        public const string GenAiToolTypeKey = "gen_ai.tool.type";

        /// <summary>
        /// The GenAI tool server name key.
        /// </summary>
        public const string GenAiToolServerNameKey = "gen_ai.tool.server.name";

        /// <summary>
        /// The GenAI tool call result key.
        /// </summary>
        public const string GenAiToolCallResultKey = "gen_ai.tool.call.result";
        #endregion

        /// <summary>
        /// The threat diagnostics summary key.
        /// </summary>
        public const string ThreatDiagnosticsSummaryKey = "threat.diagnostics.summary";
        
        /// <summary>
        /// The GenAI agent thought process key.
        /// </summary>
        public const string GenAiAgentThoughtProcessKey = "microsoft.a365.agent.thought.process";

        #endregion

        #region guardrail keys
        /// <summary>
        /// The guardian identifier key.
        /// </summary>
        public const string GenAiGuardianIdKey = "microsoft.guardian.id";

        /// <summary>
        /// The guardian name key.
        /// </summary>
        public const string GenAiGuardianNameKey = "microsoft.guardian.name";

        /// <summary>
        /// The guardian provider name key.
        /// </summary>
        public const string GenAiGuardianProviderNameKey = "microsoft.guardian.provider.name";

        /// <summary>
        /// The guardian version key.
        /// </summary>
        public const string GenAiGuardianVersionKey = "microsoft.guardian.version";

        /// <summary>
        /// The security decision type key.
        /// </summary>
        public const string GenAiSecurityDecisionTypeKey = "microsoft.security.decision.type";

        /// <summary>
        /// The security decision reason key.
        /// </summary>
        public const string GenAiSecurityDecisionReasonKey = "microsoft.security.decision.reason";

        /// <summary>
        /// The security decision code key.
        /// </summary>
        public const string GenAiSecurityDecisionCodeKey = "microsoft.security.decision.code";

        /// <summary>
        /// The security target type key.
        /// </summary>
        public const string GenAiSecurityTargetTypeKey = "microsoft.security.target.type";

        /// <summary>
        /// The security target identifier key.
        /// </summary>
        public const string GenAiSecurityTargetIdKey = "microsoft.security.target.id";

        /// <summary>
        /// The security policy identifier key.
        /// </summary>
        public const string GenAiSecurityPolicyIdKey = "microsoft.security.policy.id";

        /// <summary>
        /// The security policy name key.
        /// </summary>
        public const string GenAiSecurityPolicyNameKey = "microsoft.security.policy.name";

        /// <summary>
        /// The security policy version key.
        /// </summary>
        public const string GenAiSecurityPolicyVersionKey = "microsoft.security.policy.version";

        /// <summary>
        /// The security content input hash key.
        /// </summary>
        public const string GenAiSecurityContentInputHashKey = "microsoft.security.content.input.hash";

        /// <summary>
        /// The security content modified key.
        /// </summary>
        public const string GenAiSecurityContentModifiedKey = "microsoft.security.content.modified";

        /// <summary>
        /// The security external event identifier key for SIEM correlation.
        /// </summary>
        public const string GenAiSecurityExternalEventIdKey = "microsoft.security.external_event_id";

        /// <summary>
        /// The security content input value key (opt-in).
        /// </summary>
        public const string GenAiSecurityContentInputValueKey = "microsoft.security.content.input.value";

        /// <summary>
        /// The security content output value key (opt-in).
        /// </summary>
        public const string GenAiSecurityContentOutputValueKey = "microsoft.security.content.output.value";

        /// <summary>
        /// The security finding event name.
        /// </summary>
        public const string GenAiSecurityFindingEventName = "microsoft.security.finding";

        /// <summary>
        /// The security risk category key.
        /// </summary>
        public const string GenAiSecurityRiskCategoryKey = "microsoft.security.risk.category";

        /// <summary>
        /// The security risk severity key.
        /// </summary>
        public const string GenAiSecurityRiskSeverityKey = "microsoft.security.risk.severity";

        /// <summary>
        /// The security risk score key.
        /// </summary>
        public const string GenAiSecurityRiskScoreKey = "microsoft.security.risk.score";

        /// <summary>
        /// The security risk metadata key.
        /// </summary>
        public const string GenAiSecurityRiskMetadataKey = "microsoft.security.risk.metadata";

        /// <summary>
        /// The security policy decision type key (per-finding decision).
        /// </summary>
        public const string GenAiSecurityPolicyDecisionTypeKey = "microsoft.security.policy.decision.type";
        #endregion
    }
    #pragma warning restore CS1591
}
