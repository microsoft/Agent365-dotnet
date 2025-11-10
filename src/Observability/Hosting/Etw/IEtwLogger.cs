using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using System;

namespace Microsoft.Agents.A365.Observability.Hosting.Etw
{
    /// <summary>
    /// Interface for ETW Logger
    /// </summary>
    public interface IEtwLogger<T>
    {
        /// <summary>
        /// Logs an invoke_agent event.
        /// </summary>
        /// <param name="invokeAgentDetails">The details of the agent invocation.</param>
        /// <param name="tenantDetails">The tenant details.</param>
        /// <param name="conversationId">The required conversation ID.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="inputMessages">Optional input messages to include in the log.</param>
        /// <param name="outputMessages">Optional output messages to include in the log.</param>
        /// <param name="startTime">Optional start time of the inference.</param>
        /// <param name="endTime">Optional end time of the inference.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        public void LogInvokeAgent(
            InvokeAgentDetails invokeAgentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            Request? request = null,
            AgentDetails? callerAgentDetails = null,
            CallerDetails? callerDetails = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null);

        /// <summary>
        /// Logs an inference event.
        /// </summary>
        /// <param name="inferenceCallDetails">The details of the inference call.</param>
        /// <param name="agentDetails">The details of the agent.</param>
        /// <param name="tenantDetails">The details of the tenant.</param>
        /// <param name="conversationId">The required conversation ID.</param>
        /// <param name="inputMessages">Optional input messages to include in the log.</param>
        /// <param name="outputMessages">Optional output messages to include in the log.</param>
        /// <param name="startTime">Optional start time of the inference.</param>
        /// <param name="endTime">Optional end time of the inference.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        public void LogInferenceCall(
            InferenceCallDetails inferenceCallDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null);

        /// <summary>
        /// Logs an execute_tool event.
        /// </summary>
        /// <param name="toolCallDetails">The details of the tool call.</param>
        /// <param name="agentDetails">The details of the agent.</param>
        /// <param name="tenantDetails">The details of the tenant.</param>
        /// <param name="conversationId">The required conversation ID.</param>
        /// <param name="responseContent">Optional response content to include in the log.</param>
        /// <param name="startTime">Optional start time of the tool execution.</param>
        /// <param name="endTime">Optional end time of the tool execution.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        public void LogToolCall(
            ToolCallDetails toolCallDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            string? responseContent = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null);
    }
}
