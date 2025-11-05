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
                request,
                callerAgentDetails,
                callerDetails,
                conversationId,
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
                request,
                callerAgentDetails,
                callerDetails,
                conversationId,
                inputMessages,
                outputMessages);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.Is<EventId>((eventId) => eventId.Id == 1001 && eventId.Name == "InvokeAgent"),
                    It.Is<It.IsAnyType>((state, type) => VerifyLogState(state, 
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
        public void LogInvokeAgent_WithMinimalDetails_LogsOnlyRequiredAttributes()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var endpoint = new Uri("https://example.com");
            var tenantId = Guid.NewGuid();
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(tenantId);

            // Act
            mockLogger.Object.LogInvokeAgent(invokeAgentDetails, tenantDetails);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.Is<EventId>((eventId) => eventId.Id == 1001 && eventId.Name == "InvokeAgent"),
                    It.Is<It.IsAnyType>((state, type) => VerifyMinimalLogState(state, agentDetails, tenantDetails, endpoint)),
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

            // Act
            mockLogger.Object.LogInvokeAgent(invokeAgentDetails, tenantDetails);

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

            // Act
            mockLogger.Object.LogInvokeAgent(invokeAgentDetails, tenantDetails);

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

        private static bool VerifyLogState(
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
            if (state is not InvokeAgentData data)
                return false;

            var attributes = data.Attributes;

            // Agent details
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId)) return false;
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName)) return false;

            // Endpoint details
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.ServerAddressKey, endpoint.Host)) return false;
            if (endpoint.Port != 443 && !TryGetAndEquals(attributes, OpenTelemetryConstants.ServerPortKey, endpoint.Port)) return false;
            if (endpoint.Port == 443 && attributes.ContainsKey(OpenTelemetryConstants.ServerPortKey)) return false;

            // Session ID
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.SessionIdKey, sessionId)) return false;

            // Conversation ID
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.GenAiConversationIdKey, conversationId)) return false;

            // Input messages
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", inputMessages))) return false;

            // Output messages
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages))) return false;

            return true;
        }

        private static bool VerifyMinimalLogState(
            object state,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Uri endpoint)
        {
            if (state is not InvokeAgentData data)
                return false;

            var attributes = data.Attributes;

            // Required attributes
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId)) return false;
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName)) return false;
            if (!TryGetAndEquals(attributes, OpenTelemetryConstants.ServerAddressKey, endpoint.Host)) return false;
            if (!attributes.TryGetValue(OpenTelemetryConstants.TenantIdKey, out _)) return false;

            // Optional attributes should not be present
            if (attributes.TryGetValue(OpenTelemetryConstants.SessionIdKey, out _)) return false;
            if (attributes.TryGetValue(OpenTelemetryConstants.GenAiConversationIdKey, out _)) return false;
            if (attributes.TryGetValue(OpenTelemetryConstants.GenAiCallerIdKey, out _)) return false;

            return true;
        }

        private static bool TryGetAndEquals(IDictionary<string, object?> dict, string key, object? expected)
        {
            if (!dict.TryGetValue(key, out var value)) return false;

            return Equals(value, expected);
        }
    }
}
