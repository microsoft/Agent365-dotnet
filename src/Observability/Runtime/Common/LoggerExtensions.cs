using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Provides helper methods to log our scopes
    /// </summary>
    public static partial class LoggerExtensions
    {
        /// <summary>
        /// Logs an invoke_agent event.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="invokeAgentDetails"></param>
        /// <param name="tenantDetails"></param>
        /// <param name="request"></param>
        /// <param name="callerAgentDetails"></param>
        /// <param name="callerDetails"></param>
        /// <param name="conversationId"></param>
        public static void LogInvokeAgent(
            this ILogger logger,
            InvokeAgentDetails invokeAgentDetails, 
            TenantDetails tenantDetails, 
            Request? request = null, 
            AgentDetails? callerAgentDetails = null, 
            CallerDetails? callerDetails = null, 
            string? conversationId = null)
        {
            logger.LogInformation(
                new EventId(1001, "invoke_agent"),
                null,
                // TODO: Prepare a struct with the OpenTelemetryConstants keys like it's done in InvokeAgentScope
                new
                {
                    InvokeAgent = invokeAgentDetails,
                    Tenant = tenantDetails,
                    Request = request,
                    CallerAgent = callerAgentDetails,
                    Caller = callerDetails,
                    ConversationId = conversationId
                });
        }
        
        /// <summary>
        /// Logs an inference call event.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="inferenceCallDetails"></param>
        /// <param name="agentDetails"></param>
        /// <param name="tenantDetails"></param>
        [LoggerMessage(LogLevel.Information)]
        public static void LogInferenceCall(
            this ILogger logger,
            [LogProperties(OmitReferenceName = true)] in InferenceCallDetails inferenceCallDetails,
            [LogProperties(OmitReferenceName = true)] in AgentDetails agentDetails,
            [LogProperties(OmitReferenceName = true)] in TenantDetails tenantDetails)
        {
            // TODO
        }

        /// <summary>
        /// Logs a tool call event.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="toolCallDetails"></param>
        /// <param name="agentDetails"></param>
        /// <param name="tenantDetails"></param>
        [LoggerMessage(LogLevel.Information)]
        public static void LogToolCall(
            this ILogger logger,
            [LogProperties(OmitReferenceName = true)] in ToolCallDetails toolCallDetails,
            [LogProperties(OmitReferenceName = true)] in AgentDetails agentDetails,
            [LogProperties(OmitReferenceName = true)] in TenantDetails tenantDetails)
        {
            // TODO
        }
    }
}
