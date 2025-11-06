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
        private const string InvokeAgentEventName = "InvokeAgent";
        private static readonly EventId InvokeAgentEventId = new EventId(1001, InvokeAgentEventName);
        private const string ExecuteInferenceEventName = "ExecuteInference";
        private static readonly EventId ExecuteInferenceEventId = new EventId(1002, ExecuteInferenceEventName);
        private const string ExecuteToolEventName = "ExecuteTool";
        private static readonly EventId ExecuteToolEventId = new EventId(1003, ExecuteToolEventName);

        /// <summary>
        /// Logs an invoke_agent event.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="invokeAgentDetails">The details of the agent invocation.</param>
        /// <param name="tenantDetails">The tenant details.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="conversationId">Optional conversation ID to include in the log.</param>
        /// <param name="inputMessages">Optional input messages to include in the log.</param>
        /// <param name="outputMessages">Optional output messages to include in the log.</param>
        /// <param name="startTime">Optional start time of the inference.</param>
        /// <param name="endTime">Optional end time of the inference.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        public static void LogInvokeAgent(
            this ILogger logger,
            InvokeAgentDetails invokeAgentDetails, 
            TenantDetails tenantDetails, 
            Request? request = null, 
            AgentDetails? callerAgentDetails = null, 
            CallerDetails? callerDetails = null, 
            string? conversationId = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null)
        {
            var data = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                request,
                callerAgentDetails,
                callerDetails,
                conversationId,
                inputMessages,
                outputMessages,
                startTime,
                endTime,
                spanId,
                parentSpanId);

            logger.Log(
                LogLevel.Information,
                InvokeAgentEventId,
                data,
                null,
                LogFormatter
            );
        }

        /// <summary>
        /// Logs an inference event.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="inferenceCallDetails">The details of the inference call.</param>
        /// <param name="agentDetails">The details of the agent.</param>
        /// <param name="tenantDetails">The details of the tenant.</param>
        /// <param name="conversationId">Optional conversation ID to include in the log.</param>
        /// <param name="inputMessages">Optional input messages to include in the log.</param>
        /// <param name="outputMessages">Optional output messages to include in the log.</param>
        /// <param name="startTime">Optional start time of the inference.</param>
        /// <param name="endTime">Optional end time of the inference.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        public static void LogInferenceCall(
            this ILogger logger,
            InferenceCallDetails inferenceCallDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string? conversationId = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null)
        {
            var data = ExecuteInferenceDataBuilder.Build(
                inferenceCallDetails,
                agentDetails,
                tenantDetails,
                conversationId,
                inputMessages,
                outputMessages,
                startTime,
                endTime,
                spanId,
                parentSpanId);

            logger.Log(
                LogLevel.Information,
                ExecuteInferenceEventId,
                data,
                null,
                LogFormatter
            );
        }

        /// <summary>
        /// Logs an execute_tool event.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="toolCallDetails">The details of the tool call.</param>
        /// <param name="agentDetails">The details of the agent.</param>
        /// <param name="tenantDetails">The details of the tenant.</param>
        /// <param name="conversationId">Optional conversation ID to include in the log.</param>
        /// <param name="responseContent">Optional response content to include in the log.</param>
        /// <param name="startTime">Optional start time of the tool execution.</param>
        /// <param name="endTime">Optional end time of the tool execution.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        public static void LogToolCall(
            this ILogger logger,
            ToolCallDetails toolCallDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string? conversationId = null,
            string? responseContent = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null)
        {
            var data = ExecuteToolDataBuilder.Build(
                toolCallDetails,
                agentDetails,
                tenantDetails,
                conversationId,
                responseContent,
                startTime,
                endTime,
                spanId,
                parentSpanId);

            logger.Log(
                LogLevel.Information,
                ExecuteToolEventId,
                data,
                null,
                LogFormatter
            );
        }

        private static string LogFormatter(BaseData data, Exception? ex)
        {
            return ExportFormatter.FormatLogData(data);
        }
    }
}
