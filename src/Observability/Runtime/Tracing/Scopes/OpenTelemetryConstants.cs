// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    #pragma warning disable CS1591 // XML documentation not required for constant definitions.
    /// <summary>
    /// OpensTelemetry constant keys and values used across the Kairo SDK.
    /// </summary>
    public static class OpenTelemetryConstants
    {
        public const string AzNamespaceKey = "az.namespace";
        public const string ServerAddressKey = "server.address";
        public const string ServerPortKey = "server.port";
        public const string AzureRpNamespaceValue = "Microsoft.CognitiveServices";
        public const string SourceName = "KairoSdk";
        public const string EnableOpenTelemetrySwitch = "Azure.Experimental.EnableActivitySource";
        public const string TraceContentsSwitch = "Azure.Experimental.TraceGenAIMessageContent";
        public const string TraceContentsEnvironmentVariable = "AZURE_TRACING_GEN_AI_CONTENT_RECORDING_ENABLED";
        public const string EnableOpenTelemetryEnvironmentVariable = "AZURE_EXPERIMENTAL_ENABLE_ACTIVITY_SOURCE";
        public const string SessionIdKey = "session.id";
        public const string TenantIdKey = "tenant.id";
        public const string OperationSourceKey = "operation.source";

        public const string GenAiAgentAUIDKey = "gen_ai.agent.user.id";
        public const string GenAiAgentUPNKey = "gen_ai.agent.upn";
        public const string GenAiAgentBlueprintIdKey = "gen_ai.agent.applicationid";
        public const string CorrelationIdKey = "correlation.id";
        public const string GenAiCallerIdKey = "gen_ai.caller.id";
        public const string HiringManagerIdKey = "hiring.manager.id";

        public const string GenAiClientOperationDurationMetricName = "gen_ai.client.operation.duration";
        public const string GenAiClientTokenUsageMetricName = "gen_ai.client.token.usage";
        public const string GenAiRequestContentKey = "gen_ai.request.content";
        public const string GenAiRequestMaxTokensKey = "gen_ai.request.max_tokens";
        public const string GenAiRequestModelKey = "gen_ai.request.model";
        public const string GenAiRequestTemperatureKey = "gen_ai.request.temperature";
        public const string GenAiRequestTopPKey = "gen_ai.request.top_p";
        public const string GenAiResponseIdKey = "gen_ai.response.id";
        public const string GenAiResponseFinishReasonsKey = "gen_ai.response.finish_reasons";
        public const string GenAiResponseModelKey = "gen_ai.response.model";
        public const string GenAiSystemKey = "gen_ai.system";
        public const string GenAiSystemValue = "az.ai.agent365";

        public const string GenAiAgentIdKey = "gen_ai.agent.id";
        public const string GenAiAgentNameKey = "gen_ai.agent.name";
        public const string GenAiAgentDescriptionKey = "gen_ai.agent.description";
        public const string GenAiConversationIdKey = "gen_ai.conversation.id";
        public const string GenAiIconUriKey = "gen_ai.agent365.icon_uri";
        public const string GenAiTokenTypeKey = "gen_ai.token.type";
        public const string GenAiUsageInputTokensKey = "gen_ai.usage.input_tokens";
        public const string GenAiUsageOutputTokensKey = "gen_ai.usage.output_tokens";
        public const string GenAiChoice = "gen_ai.choice";
        public const string GenAiProviderNameKey = "gen_ai.provider.name";
        public const string GenAiInputMessagesKey = "gen_ai.input.messages";
        public const string GenAiOutputMessagesKey = "gen_ai.output.messages";

        // AI-specific dimensions
        public const string GenAiTaskIdKey = "gen_ai.task.id";

        // AI invocation context dimensions
        public const string GenAiExecutionTypeKey = "gen_ai.execution.type";
        public const string GenAiExecutionPayloadKey = "gen_ai.execution.payload";

        // AI source metadata dimensions
        public const string GenAiExecutionSourceIdKey = "gen_ai.execution.sourceMetadata.id";
        public const string GenAiExecutionSourceNameKey = "gen_ai.execution.sourceMetadata.name";
        public const string GenAiExecutionSourceDescriptionKey = "gen_ai.execution.sourceMetadata.description";

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
