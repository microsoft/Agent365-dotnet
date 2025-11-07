using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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
                new SourceMetadata("source-id", "source-name", Role.Human, "source-description"));
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
                new SourceMetadata("source-id", "source-name", Role.Human, "source-description"));
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
            var inputMessages = new[] { "Hello", "Tell me a joke" };
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
            if (state is not InvokeAgentData data) return false;
            var a = data.Attributes;
            return TryGetAndEquals(a, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId)
                && TryGetAndEquals(a, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName)
                && TryGetAndEquals(a, OpenTelemetryConstants.ServerAddressKey, endpoint.Host)
                && (endpoint.Port == 443 || TryGetAndEquals(a, OpenTelemetryConstants.ServerPortKey, endpoint.Port))
                && TryGetAndEquals(a, OpenTelemetryConstants.SessionIdKey, sessionId)
                && TryGetAndEquals(a, OpenTelemetryConstants.GenAiConversationIdKey, conversationId)
                && TryGetAndEquals(a, OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", inputMessages))
                && TryGetAndEquals(a, OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages));
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
            if (state is not ExecuteToolData data) return false;
            var a = data.Attributes;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiToolNameKey, toolDetails.ToolName)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiToolArgumentsKey, toolDetails.Arguments)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiToolCallIdKey, toolDetails.ToolCallId)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiToolDescriptionKey, toolDetails.Description)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiToolTypeKey, toolDetails.ToolType)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.ServerAddressKey, endpoint.Host)) return false;
            if (endpoint.Port != 443 && !TryGetAndEquals(a, OpenTelemetryConstants.ServerPortKey, endpoint.Port)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiConversationIdKey, conversationId)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiEventContent, responseContent)) return false;
            // Timing and span validation
            if (data.StartTime != start || data.EndTime != end) return false;
            if (data.Duration <= TimeSpan.Zero) return false;
            if (data.SpanId != spanId || data.ParentSpanId != parentSpanId) return false;
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
            if (state is not ExecuteInferenceData data) return false;
            var a = data.Attributes;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiOperationNameKey, inferenceDetails.OperationName.ToString())) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiRequestModelKey, inferenceDetails.Model)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiProviderNameKey, inferenceDetails.ProviderName)) return false;
            if (inferenceDetails.InputTokens.HasValue && !TryGetAndEquals(a, OpenTelemetryConstants.GenAiUsageInputTokensKey, inferenceDetails.InputTokens.Value)) return false;
            if (inferenceDetails.OutputTokens.HasValue && !TryGetAndEquals(a, OpenTelemetryConstants.GenAiUsageOutputTokensKey, inferenceDetails.OutputTokens.Value)) return false;
            if (inferenceDetails.FinishReasons != null && !TryGetAndEquals(a, OpenTelemetryConstants.GenAiResponseFinishReasonsKey, string.Join(",", inferenceDetails.FinishReasons))) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiResponseIdKey, inferenceDetails.ResponseId)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiConversationIdKey, conversationId)) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", inputMessages))) return false;
            if (!TryGetAndEquals(a, OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages))) return false;
            if (data.StartTime != start || data.EndTime != end) return false;
            if (data.Duration <= TimeSpan.Zero) return false;
            if (data.SpanId != spanId || data.ParentSpanId != parentSpanId) return false;
            return true;
        }

        private static bool TryGetAndEquals(IDictionary<string, object?> dict, string key, object? expected)
        {
            if (!dict.TryGetValue(key, out var value)) return false;
            return Equals(value, expected);
        }
    }
}
