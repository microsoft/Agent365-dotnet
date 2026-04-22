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
            OutputMessages
        }

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
        /// The GenAI tool arguments key.
        /// </summary>
        public const string GenAiToolArgumentsKey = "gen_ai.tool.arguments";
        
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
    }
    #pragma warning restore CS1591
}
