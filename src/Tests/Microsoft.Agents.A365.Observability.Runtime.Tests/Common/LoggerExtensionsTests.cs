// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common
{
    [TestClass]
    public class LoggerExtensionsTests
    {
        [TestMethod]
        public void LogInvokeAgent_WithFullDetails_DoesNotThrow()
        {
            // Arrange
            var logger = NullLogger.Instance;
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent", "Test Description");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails, "session-456");
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var request = new Request(
                "test content",
                ExecutionType.HumanToAgent,
                "session-456",
                new SourceMetadata(id: "source-id", name: "source-name", role: Role.Human, description: "source-description"));
            var callerAgentDetails = new AgentDetails("caller-agent-789", "CallerAgent");
            var callerDetails = new CallerDetails("caller-123", "Caller Name", "caller@example.com");
            var conversationId = "conv-999";
            var inputMessages = new[] { "Hello" };
            var outputMessages = new[] { "Hi" };

            // Act
            Action act = () => logger.LogInvokeAgent(
                invokeAgentDetails,
                tenantDetails,
                conversationId,
                request,
                callerAgentDetails,
                callerDetails,
                inputMessages,
                outputMessages);

            // Assert
            act.Should().NotThrow();
        }

        [TestMethod]
        public void LogInvokeAgent_WithFullDetails_LogsWithCorrectArguments()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var endpoint = new Uri("https://example.com:8080");
            var tenantId = Guid.NewGuid();
            var agentDetails = new AgentDetails(
                "agent-123",
                "TestAgent",
                "Test Description",
                iconUri: null,
                agentAUID: "auid-456",
                agentUPN: "agent@example.com",
                agentBlueprintId: "blueprint-789",
                tenantId: "tenant-999");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails, "session-456");
            var tenantDetails = new TenantDetails(tenantId);
            var request = new Request(
                "test content",
                ExecutionType.HumanToAgent,
                "session-456",
                new SourceMetadata(id: "source-id", name: "source-name", role: Role.Human, description: "source-description"));
            var callerAgentDetails = new AgentDetails("caller-agent-789", "CallerAgent");
            var callerDetails = new CallerDetails("caller-123", "Caller Name", "caller@example.com");
            var conversationId = "conv-999";
            var inputMessages = new[] { "Hello", "How are you?" };
            var outputMessages = new[] { "Hi there!", "I'm fine." };

            // Act
            mockLogger.Object.LogInvokeAgent(
                invokeAgentDetails,
                tenantDetails,
                conversationId,
                request,
                callerAgentDetails,
                callerDetails,
                inputMessages,
                outputMessages);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.Is<EventId>((eventId) => eventId.Id == 1001 && eventId.Name == "InvokeAgent"),
                    It.Is<It.IsAnyType>((state, type) => VerifyInvokeAgentLogState(state,
                        agentDetails,
                        tenantDetails,
                        endpoint,
                        "session-456",
                        request,
                        callerAgentDetails,
                        callerDetails,
                        conversationId,
                        inputMessages,
                        outputMessages)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void LogExecuteTool_WithFullDetails_LogsWithCorrectArguments()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var endpoint = new Uri("https://tools.example.com:9090");
            var toolDetails = new ToolCallDetails("GetWeather", "{ \"location\": \"NYC\" }", "tool-call-123", "Gets current weather", "function", endpoint);
            var agentDetails = new AgentDetails("agent-tool-1", "ToolAgent", "Agent for tools", agentAUID: "auid-tool", agentUPN: "tool@agent.com", agentBlueprintId: "bp-tool");
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-tool";
            var responseContent = "Sunny 72F";
            var start = DateTimeOffset.UtcNow.AddSeconds(-5);
            var end = DateTimeOffset.UtcNow;
            var spanId = "tool-span";
            var parentSpanId = "tool-parent";

            // Act
            mockLogger.Object.LogToolCall(
                toolCallDetails: toolDetails,
                agentDetails: agentDetails,
                tenantDetails: tenantDetails,
                conversationId: conversationId,
                responseContent: responseContent,
                startTime: start,
                endTime: end,
                spanId: spanId,
                parentSpanId: parentSpanId);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.Is<EventId>((eventId) => eventId.Id == 1003 && eventId.Name == "ExecuteTool"),
                    It.Is<It.IsAnyType>((state, type) => VerifyExecuteToolLogState(state,
                        toolDetails,
                        agentDetails,
                        tenantDetails,
                        endpoint,
                        conversationId,
                        responseContent,
                        start,
                        end,
                        spanId,
                        parentSpanId)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void LogExecuteInference_WithFullDetails_LogsWithCorrectArguments()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var inferenceDetails = new InferenceCallDetails(
                InferenceOperationType.Chat,
                "gpt-4o-mini",
                "openai",
                inputTokens: 120,
                outputTokens: 240,
                finishReasons: new[] { "stop", "length" },
                responseId: "resp-789");
            var agentDetails = new AgentDetails("agent-inf-1", "InferAgent", "Inference agent", agentAUID: "auid-inf", agentUPN: "inf@agent.com", agentBlueprintId: "bp-inf");
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-inf";
            var inputMessages = new[] { "Hello", "Tell me attrsObj joke" };
            var outputMessages = new[] { "Hi!", "Why did the AI cross the road?" };
            var start = DateTimeOffset.UtcNow.AddSeconds(-3);
            var end = DateTimeOffset.UtcNow;
            var spanId = "inf-span";
            var parentSpanId = "inf-parent";

            // Act
            mockLogger.Object.LogInferenceCall(
                inferenceCallDetails: inferenceDetails,
                agentDetails: agentDetails,
                tenantDetails: tenantDetails,
                conversationId: conversationId,
                inputMessages: inputMessages,
                outputMessages: outputMessages,
                startTime: start,
                endTime: end,
                spanId: spanId,
                parentSpanId: parentSpanId);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.Is<EventId>((eventId) => eventId.Id == 1002 && eventId.Name == "ExecuteInference"),
                    It.Is<It.IsAnyType>((state, type) => VerifyExecuteInferenceLogState(state,
                        inferenceDetails,
                        agentDetails,
                        tenantDetails,
                        conversationId,
                        inputMessages,
                        outputMessages,
                        start,
                        end,
                        spanId,
                        parentSpanId)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void LogInvokeAgent_UsesInformationLogLevel()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-minimal";

            // Act
            mockLogger.Object.LogInvokeAgent(invokeAgentDetails, tenantDetails, conversationId);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void LogInvokeAgent_UsesCorrectEventId()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-minimal";

            // Act
            mockLogger.Object.LogInvokeAgent(invokeAgentDetails, tenantDetails, conversationId);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.Is<EventId>((eventId) => eventId.Id == 1001 && eventId.Name == "InvokeAgent"),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void LogInvokeAgent_GeneratesSpanId_WhenNotProvided()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-minimal";

            // Act
            mockLogger.Object.LogInvokeAgent(invokeAgentDetails, tenantDetails, conversationId);

            // Assert
            var invocation = mockLogger.Invocations.Single(i => i.Method.Name == "Log");
            var stateArg = invocation.Arguments[2];
            var captured = stateArg as Dictionary<string, object?>;
            captured.Should().NotBeNull();
            captured!.TryGetValue("SpanId", out var spanIdObj).Should().BeTrue();
            var spanId = spanIdObj as string;
            spanId.Should().NotBeNullOrEmpty();
            Regex.IsMatch(spanId!, "^[0-9a-fA-F]{16}$").Should().BeTrue();
        }

        private static bool VerifyInvokeAgentLogState(
            object state,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Uri endpoint,
            string sessionId,
            Request request,
            AgentDetails callerAgentDetails,
            CallerDetails callerDetails,
            string conversationId,
            string[] inputMessages,
            string[] outputMessages)
        {
            if (state is not Dictionary<string, object?> data) return false;
            if (!data.TryGetValue("Attributes", out var attrsObj)) return false;
            if (attrsObj is not IDictionary<string, object?> attrs) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentDescriptionKey, agentDetails.AgentDescription)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentAUIDKey, agentDetails.AgentAUID)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentUPNKey, agentDetails.AgentUPN)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentBlueprintIdKey, agentDetails.AgentBlueprintId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.TenantIdKey, tenantDetails.TenantId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.ServerAddressKey, endpoint.Host)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.ServerPortKey, endpoint.Port)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.SessionIdKey, sessionId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiConversationIdKey, conversationId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", inputMessages))) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages))) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiChannelNameKey, request.SourceMetadata?.Name)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiChannelLinkKey, request.SourceMetadata?.Description)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiExecutionTypeKey, request.ExecutionType?.ToString())) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiCallerAgentNameKey, callerAgentDetails.AgentName)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiCallerAgentIdKey, callerAgentDetails.AgentId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn)) return false;

            return true;
        }

        private static bool VerifyExecuteToolLogState(
            object state,
            ToolCallDetails toolDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Uri endpoint,
            string conversationId,
            string responseContent,
            DateTimeOffset start,
            DateTimeOffset end,
            string spanId,
            string parentSpanId)
        {
            if (state is not Dictionary<string, object?> data) return false;
            if (!data.TryGetValue("Attributes", out var attrsObj)) return false;
            if (attrsObj is not IDictionary<string, object?> attrs) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentDescriptionKey, agentDetails.AgentDescription)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentAUIDKey, agentDetails.AgentAUID)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentUPNKey, agentDetails.AgentUPN)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentBlueprintIdKey, agentDetails.AgentBlueprintId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.TenantIdKey, tenantDetails.TenantId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiToolNameKey, toolDetails.ToolName)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiToolArgumentsKey, toolDetails.Arguments)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiToolCallIdKey, toolDetails.ToolCallId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiToolDescriptionKey, toolDetails.Description)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiToolTypeKey, toolDetails.ToolType)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.ServerAddressKey, endpoint.Host)) return false;
            if (endpoint.Port != 443 && !TryGetAndEquals(attrs, OpenTelemetryConstants.ServerPortKey, endpoint.Port)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiConversationIdKey, conversationId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiEventContent, responseContent)) return false;
            if (!data.TryGetValue("StartTime", out var startTimeObj) || startTimeObj is not DateTimeOffset startTime || startTime != start) return false;
            if (!data.TryGetValue("EndTime", out var endTimeObj) || endTimeObj is not DateTimeOffset endTime || endTime != end) return false;
            if (!data.TryGetValue("Duration", out var durationObj) || durationObj is not TimeSpan duration || duration <= TimeSpan.Zero) return false;
            if (!data.TryGetValue("SpanId", out var spanIdObj) || spanIdObj is not string actualSpanId || actualSpanId != spanId) return false;
            if (!data.TryGetValue("ParentSpanId", out var parentSpanIdObj) || parentSpanIdObj is not string actualParentSpanId || actualParentSpanId != parentSpanId) return false;

            return true;
        }

        private static bool VerifyExecuteInferenceLogState(
            object state,
            InferenceCallDetails inferenceDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            string[] inputMessages,
            string[] outputMessages,
            DateTimeOffset start,
            DateTimeOffset end,
            string spanId,
            string parentSpanId)
        {
            if (state is not Dictionary<string, object?> data) return false;
            if (!data.TryGetValue("Attributes", out var attrsObj)) return false;
            if (attrsObj is not IDictionary<string, object?> attrs) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentDescriptionKey, agentDetails.AgentDescription)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentAUIDKey, agentDetails.AgentAUID)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentUPNKey, agentDetails.AgentUPN)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiAgentBlueprintIdKey, agentDetails.AgentBlueprintId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.TenantIdKey, tenantDetails.TenantId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiOperationNameKey, inferenceDetails.OperationName.ToString())) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiRequestModelKey, inferenceDetails.Model)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiProviderNameKey, inferenceDetails.ProviderName)) return false;
            if (inferenceDetails.InputTokens.HasValue && !TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiUsageInputTokensKey, inferenceDetails.InputTokens.Value)) return false;
            if (inferenceDetails.OutputTokens.HasValue && !TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiUsageOutputTokensKey, inferenceDetails.OutputTokens.Value)) return false;
            if (inferenceDetails.FinishReasons != null && !TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiResponseFinishReasonsKey, string.Join(",", inferenceDetails.FinishReasons))) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiResponseIdKey, inferenceDetails.ResponseId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiConversationIdKey, conversationId)) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", inputMessages))) return false;
            if (!TryGetAndEquals(attrs, OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages))) return false;
            if (!data.TryGetValue("StartTime", out var startTimeObj) || startTimeObj is not DateTimeOffset startTime || startTime != start) return false;
            if (!data.TryGetValue("EndTime", out var endTimeObj) || endTimeObj is not DateTimeOffset endTime || endTime != end) return false;
            if (!data.TryGetValue("Duration", out var durationObj) || durationObj is not TimeSpan duration || duration <= TimeSpan.Zero) return false;
            if (!data.TryGetValue("SpanId", out var spanIdObj) || spanIdObj is not string actualSpanId || actualSpanId != spanId) return false;
            if (!data.TryGetValue("ParentSpanId", out var parentSpanIdObj) || parentSpanIdObj is not string actualParentSpanId || actualParentSpanId != parentSpanId) return false;
            
            return true;
        }

        private static bool TryGetAndEquals(IDictionary<string, object?> dict, string key, object? expected)
        {
            if (!dict.TryGetValue(key, out var value)) return false;
            return Equals(value, expected);
        }
    }
}
