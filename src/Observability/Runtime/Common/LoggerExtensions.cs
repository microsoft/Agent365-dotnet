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
        public static void LogInvokeAgent(
            this ILogger logger, 
            [LogProperties(OmitReferenceName = true)] in InvokeAgentDetails invokeAgentDetails,
            [LogProperties(OmitReferenceName = true)] in TenantDetails tenantDetails)
        {
            // TODO
        }

        /// <summary>
        /// Logs an inference call event.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="inferenceCallDetails"></param>
        /// <param name="agentDetails"></param>
        /// <param name="tenantDetails"></param>
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
