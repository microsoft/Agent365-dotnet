using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Extensions.Logging;
using System;

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
        /// <param name="inputMessages">Optional input messages to include in the log.</param>
        /// <param name="outputMessages">Optional output messages to include in the log.</param>
        public static void LogInvokeAgent(
            this ILogger logger,
            InvokeAgentDetails invokeAgentDetails, 
            TenantDetails tenantDetails, 
            Request? request = null, 
            AgentDetails? callerAgentDetails = null, 
            CallerDetails? callerDetails = null, 
            string? conversationId = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null)
        {
            var invokeAgentData = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                request,
                callerAgentDetails,
                callerDetails,
                conversationId,
                inputMessages,
                outputMessages);

            logger.Log(
                LogLevel.Information,
                new EventId(1001, "InvokeAgent"),
                invokeAgentData,
                null,
                LogFormatter
            );
        }
        
        ///// <summary>
        ///// Logs an inference call event.
        ///// </summary>
        ///// <param name="logger"></param>
        ///// <param name="inferenceCallDetails"></param>
        ///// <param name="agentDetails"></param>
        ///// <param name="tenantDetails"></param>
        //[LoggerMessage(LogLevel.Information)]
        //public static void LogInferenceCall(
        //    this ILogger logger,
        //    [LogProperties(OmitReferenceName = true)] in InferenceCallDetails inferenceCallDetails,
        //    [LogProperties(OmitReferenceName = true)] in AgentDetails agentDetails,
        //    [LogProperties(OmitReferenceName = true)] in TenantDetails tenantDetails)
        //{
        //    // TODO
        //}

        ///// <summary>
        ///// Logs a tool call event.
        ///// </summary>
        ///// <param name="logger"></param>
        ///// <param name="toolCallDetails"></param>
        ///// <param name="agentDetails"></param>
        ///// <param name="tenantDetails"></param>
        //[LoggerMessage(LogLevel.Information)]
        //public static void LogToolCall(
        //    this ILogger logger,
        //    [LogProperties(OmitReferenceName = true)] in ToolCallDetails toolCallDetails,
        //    [LogProperties(OmitReferenceName = true)] in AgentDetails agentDetails,
        //    [LogProperties(OmitReferenceName = true)] in TenantDetails tenantDetails)
        //{
        //    // TODO
        //}

        private static string LogFormatter(InvokeAgentData data, Exception? ex)
        {
            return ExportFormatter.FormatLogData(data);
        }
    }
}
