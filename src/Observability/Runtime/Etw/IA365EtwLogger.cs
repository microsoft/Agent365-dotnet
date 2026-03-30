// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Etw
{
    /// <summary>
    /// Interface for ETW Logger
    /// </summary>
    public interface IA365EtwLogger<T>
    {
        /// <summary>
        /// Logs an invoke_agent event.
        /// </summary>
        /// <param name="invokeAgentScopeDetails">The scope-level details of the agent invocation.</param>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="conversationId">The required conversation ID.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerDetails">The details of the caller.</param>
        /// <param name="inputMessages">Optional input messages to include in the log.</param>
        /// <param name="outputMessages">Optional output messages to include in the log.</param>
        /// <param name="startTime">Optional start time of the inference.</param>
        /// <param name="endTime">Optional end time of the inference.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        public void LogInvokeAgent(
            InvokeAgentScopeDetails invokeAgentScopeDetails,
            AgentDetails agentDetails,
            string conversationId,
            Request? request = null,
            CallerDetails? callerDetails = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            string? traceId = null);

        /// <summary>
        /// Logs an inference event.
        /// </summary>
        /// <param name="inferenceCallDetails">The details of the inference call.</param>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="conversationId">The required conversation ID.</param>
        /// <param name="inputMessages">Optional input messages to include in the log.</param>
        /// <param name="outputMessages">Optional output messages to include in the log.</param>
        /// <param name="startTime">Optional start time of the inference.</param>
        /// <param name="endTime">Optional end time of the inference.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        /// <param name="channel">Optional channel information for the inference call.</param>
        /// <param name="callerDetails">Optional details of the caller.</param>
        public void LogInferenceCall(
            InferenceCallDetails inferenceCallDetails,
            AgentDetails agentDetails,
            string conversationId,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            Channel? channel = null,
            CallerDetails? callerDetails = null,
            string? traceId = null);

        /// <summary>
        /// Logs an execute_tool event.
        /// </summary>
        /// <param name="toolCallDetails">The details of the tool call.</param>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="conversationId">The required conversation ID.</param>
        /// <param name="responseContent">Optional response content to include in the log.</param>
        /// <param name="startTime">Optional start time of the tool execution.</param>
        /// <param name="endTime">Optional end time of the tool execution.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        /// <param name="channel">Optional channel information for the tool call.</param>
        /// <param name="callerDetails">Optional details of the caller.</param>
        public void LogToolCall(
            ToolCallDetails toolCallDetails,
            AgentDetails agentDetails,
            string conversationId,
            string? responseContent = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            Channel? channel = null,
            CallerDetails? callerDetails = null,
            string? traceId = null);

        /// <summary>
        /// Logs an output_messages event.
        /// </summary>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="response">The response containing output messages.</param>
        /// <param name="conversationId">Optional conversation ID for the output.</param>
        /// <param name="channel">Optional channel information for the output.</param>
        /// <param name="callerDetails">Optional details of the caller.</param>
        /// <param name="startTime">Optional start time of the output operation.</param>
        /// <param name="endTime">Optional end time of the output operation.</param>
        /// <param name="spanId">Optional span ID for tracing.</param>
        /// <param name="parentSpanId">Optional parent span ID for tracing.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        public void LogOutput(
            AgentDetails agentDetails,
            Response response,
            string? conversationId = null,
            Channel? channel = null,
            CallerDetails? callerDetails = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            string? traceId = null);
    }
}
