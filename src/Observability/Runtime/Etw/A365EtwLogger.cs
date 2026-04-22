// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Etw
{
    /// <summary>
    /// Provides ETW logging functionality for tracing events.
    /// </summary>
    public class A365EtwLogger<T> : IA365EtwLogger<T>
    {
        private ILogger logger { get; }
        private const string InvokeAgentEventName = "InvokeAgent";
        private static readonly EventId InvokeAgentEventId = new EventId(1001, InvokeAgentEventName);
        private const string ExecuteInferenceEventName = "ExecuteInference";
        private static readonly EventId ExecuteInferenceEventId = new EventId(1002, ExecuteInferenceEventName);
        private const string ExecuteToolEventName = "ExecuteTool";
        private static readonly EventId ExecuteToolEventId = new EventId(1003, ExecuteToolEventName);
        private const string OutputMessagesEventName = "OutputMessages";
        private static readonly EventId OutputMessagesEventId = new EventId(1004, OutputMessagesEventName);

        /// <summary>
        /// Initializes a new instance of the <see cref="A365EtwLogger{T}"/> class.
        /// </summary>
        /// <param name="factory">The logger factory.</param>
        public A365EtwLogger(ILoggerFactory factory)
        {
            var baseCategory = typeof(T).FullName!;
            logger = factory.CreateLogger(Constants.EtwCategoryPrefix + baseCategory);
        }

        /// <inheritdoc/>
        public void LogInferenceCall(
            InferenceCallDetails inferenceCallDetails, 
            AgentDetails agentDetails, 
            string conversationId, 
            string[]? inputMessages, 
            string[]? outputMessages, 
            DateTimeOffset? startTime, 
            DateTimeOffset? endTime, 
            string? spanId, 
            string? parentSpanId,
            Channel? channel,
            CallerDetails? callerDetails,
            string? traceId)
        {
            var data = ExecuteInferenceDataBuilder.Build(
                inferenceCallDetails,
                agentDetails,
                conversationId,
                inputMessages,
                outputMessages,
                startTime,
                endTime,
                spanId,
                parentSpanId,
                channel,
                callerDetails: callerDetails,
                traceId: traceId);

            logger.Log(
                LogLevel.Information,
                ExecuteInferenceEventId,
                data.ToDictionary(),
                null,
                LogFormatter
            );
        }

        /// <inheritdoc/>
        public void LogInvokeAgent(
            InvokeAgentScopeDetails invokeAgentScopeDetails, 
            AgentDetails agentDetails, 
            string conversationId, 
            Request? request, 
            CallerDetails? callerDetails, 
            string[]? inputMessages, 
            string[]? outputMessages, 
            DateTimeOffset? startTime, 
            DateTimeOffset? endTime, 
            string? spanId, 
            string? parentSpanId,
            string? traceId)
        {
            var data = InvokeAgentDataBuilder.Build(
                invokeAgentScopeDetails,
                agentDetails,
                conversationId,
                request,
                callerDetails,
                inputMessages,
                outputMessages,
                startTime,
                endTime,
                spanId,
                parentSpanId,
                traceId: traceId);

            logger.Log(
                LogLevel.Information,
                InvokeAgentEventId,
                data.ToDictionary(),
                null,
                LogFormatter
            );
        }

        /// <inheritdoc/>
        public void LogToolCall(
            ToolCallDetails toolCallDetails, 
            AgentDetails agentDetails, 
            string conversationId, 
            string? responseContent, 
            DateTimeOffset? startTime, 
            DateTimeOffset? endTime, 
            string? spanId, 
            string? parentSpanId,
            Channel? channel,
            CallerDetails? callerDetails,
            string? traceId)
        {
            var data = ExecuteToolDataBuilder.Build(
                toolCallDetails,
                agentDetails,
                conversationId,
                responseContent,
                startTime,
                endTime,
                spanId,
                parentSpanId,
                channel,
                callerDetails: callerDetails,
                traceId: traceId);

            logger.Log(
                LogLevel.Information,
                ExecuteToolEventId,
                data.ToDictionary(),
                null,
                LogFormatter
            );
        }

        /// <inheritdoc/>
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
            string? traceId = null)
        {
            var data = OutputDataBuilder.Build(
                agentDetails,
                response,
                conversationId,
                channel,
                callerDetails,
                startTime,
                endTime,
                spanId,
                parentSpanId,
                traceId: traceId);

            logger.Log(
                LogLevel.Information,
                OutputMessagesEventId,
                data.ToDictionary(),
                null,
                LogFormatter
            );
        }

        private static string LogFormatter(Dictionary<string, object?> data, Exception? ex)
        {
            return $"Name: {data["Name"]}, SpanId: {data["SpanId"]}, ParentSpanId: {data["ParentSpanId"]}, TraceId: {data["TraceId"]}";
        }
    }
}
