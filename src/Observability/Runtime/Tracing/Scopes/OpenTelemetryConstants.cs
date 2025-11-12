// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Runtime.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    #pragma warning disable CS1591 // XML documentation not required for constant definitions.
    /// <summary>
    /// OpensTelemetry constant keys and values used across the Microsoft Agent 365 SDK.
    /// </summary>
    public static class OpenTelemetryConstants
    {
        public const string EnableOpenTelemetrySwitch = "Azure.Experimental.EnableActivitySource";
        public const string SourceName = "Agent365Sdk";

        public const string ServerAddressKey = "server.address";
        public const string ServerPortKey = "server.port";
        public const string SessionIdKey = "session.id";
        public const string TenantIdKey = "tenant.id";
        public const string OperationSourceKey = "operation.source";
        public const string CorrelationIdKey = "correlation.id";

        public const string GenAiClientOperationDurationMetricName = "gen_ai.client.operation.duration";
        public const string GenAiRequestModelKey = "gen_ai.request.model";
        public const string GenAiResponseIdKey = "gen_ai.response.id";
        public const string GenAiResponseFinishReasonsKey = "gen_ai.response.finish_reasons";
        public const string GenAiSystemKey = "gen_ai.system";

        public const string GenAiConversationIdKey = "gen_ai.conversation.id";
        public const string GenAiConversationItemLinkKey = "gen_ai.conversation.itemLink";
        public const string GenAiUsageInputTokensKey = "gen_ai.usage.input_tokens";
        public const string GenAiUsageOutputTokensKey = "gen_ai.usage.output_tokens";
        public const string GenAiProviderNameKey = "gen_ai.provider.name";
        public const string GenAiInputMessagesKey = "gen_ai.input.messages";
        public const string GenAiOutputMessagesKey = "gen_ai.output.messages";

        [DataContract]
        public enum OperationNames
        {
            [EnumMember(Value = "InvokeAgent")]
            InvokeAgent,

            [EnumMember(Value = "ExecuteInference")]
            ExecuteInference,

            [EnumMember(Value = "ExecuteTool")]
            ExecuteTool
        }

        // AI invocation context dimensions
        public const string GenAiExecutionTypeKey = "gen_ai.execution.type";

        // AI channel metadata dimensions
        public const string GenAiChannelNameKey = "gen_ai.channel.name";
        public const string GenAiChannelLinkKey = "gen_ai.channel.link";

        // Target agent dimensions
        public const string GenAiAgentIdKey = "gen_ai.agent.id";
        public const string GenAiAgentNameKey = "gen_ai.agent.name";
        public const string GenAiAgentDescriptionKey = "gen_ai.agent.description";
        public const string GenAiAgentAUIDKey = "gen_ai.agent.userid";
        public const string GenAiAgentUPNKey = "gen_ai.agent.upn";
        public const string GenAiAgentBlueprintIdKey = "gen_ai.agent.applicationid";

        // Caller dimensions
        public const string GenAiCallerIdKey = "gen_ai.caller.id";
        public const string GenAiCallerUpnKey = "gen_ai.caller.upn";
        public const string GenAiCallerNameKey = "gen_ai.caller.name";
        public const string GenAiCallerUserIdKey = "gen_ai.caller.userid";
        public const string GenAiCallerTenantIdKey = "gen_ai.caller.tenantid";

        // Caller agent dimensions
        public const string GenAiCallerAgentNameKey = "gen_ai.caller.agent.name";
        public const string GenAiCallerAgentIdKey = "gen_ai.caller.agent.id";
        public const string GenAiCallerAgentApplicationIdKey = "gen_ai.caller.agent.applicationid";
        public const string GenAiCallerAgentAUIDKey = "gen_ai.caller.agent.userid";
        public const string GenAiCallerAgentUPNKey = "gen_ai.caller.agent.upn";
        public const string GenAiCallerAgentTenantKey = "gen_ai.caller.agent.tenantid";

        #region Public Constants
        /// <summary>
        ///  The GenAI operation name key.
        /// </summary>
        public const string GenAiOperationNameKey = "gen_ai.operation.name";

        /// <summary>
        /// The GenAI event content key.
        /// </summary>
        public const string GenAiEventContent = "gen_ai.event.content";
        
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
        #endregion

        #endregion
    }
    #pragma warning restore CS1591
}
