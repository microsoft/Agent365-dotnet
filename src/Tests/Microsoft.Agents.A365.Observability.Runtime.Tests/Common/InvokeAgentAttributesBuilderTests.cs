using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common
{
    [TestClass]
    public class InvokeAgentAttributesBuilderTests
    {
        [TestMethod]
        public void BuildAttributes_WithMinimalDetails_ReturnsExpectedAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var attributes = InvokeAgentAttributesBuilder.BuildAttributes(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-123");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            attributes[OpenTelemetryConstants.GenAiAgentNameKey].Should().Be("TestAgent");
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            attributes[OpenTelemetryConstants.ServerAddressKey].Should().Be("example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);
        }

        [TestMethod]
        public void BuildAttributes_WithFullDetails_ReturnsAllAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:8080");
            var agentDetails = new AgentDetails(
                "agent-123", 
                "TestAgent", 
                "Test Description",
                null,
                "auid-456",
                "agent@example.com",
                "blueprint-789",
                "tenant-999");
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

            // Act
            var attributes = InvokeAgentAttributesBuilder.BuildAttributes(
                invokeAgentDetails,
                tenantDetails,
                request,
                callerAgentDetails,
                callerDetails,
                conversationId);

            // Assert
            // Agent details
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-123");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            attributes[OpenTelemetryConstants.GenAiAgentNameKey].Should().Be("TestAgent");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey);
            attributes[OpenTelemetryConstants.GenAiAgentDescriptionKey].Should().Be("Test Description");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentAUIDKey);
            attributes[OpenTelemetryConstants.GenAiAgentAUIDKey].Should().Be("auid-456");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentUPNKey);
            attributes[OpenTelemetryConstants.GenAiAgentUPNKey].Should().Be("agent@example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentBlueprintIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentBlueprintIdKey].Should().Be("blueprint-789");

            // Endpoint details
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            attributes[OpenTelemetryConstants.ServerAddressKey].Should().Be("example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerPortKey);
            attributes[OpenTelemetryConstants.ServerPortKey].Should().Be(8080);

            // Session ID
            attributes.Should().ContainKey(OpenTelemetryConstants.SessionIdKey);
            attributes[OpenTelemetryConstants.SessionIdKey].Should().Be("session-456");

            // Request details
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiExecutionSourceIdKey);
            attributes[OpenTelemetryConstants.GenAiExecutionSourceIdKey].Should().Be("source-id");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiExecutionTypeKey);
            attributes[OpenTelemetryConstants.GenAiExecutionTypeKey].Should().Be("HumanToAgent");

            // Conversation ID
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            attributes[OpenTelemetryConstants.GenAiConversationIdKey].Should().Be("conv-999");

            // Caller details
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerIdKey);
            attributes[OpenTelemetryConstants.GenAiCallerIdKey].Should().Be("caller-123");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerNameKey);
            attributes[OpenTelemetryConstants.GenAiCallerNameKey].Should().Be("Caller Name");

            // Caller agent details
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentIdKey);
            attributes[OpenTelemetryConstants.GenAiCallerAgentIdKey].Should().Be("caller-agent-789");
        }

        [TestMethod]
        public void BuildAttributes_WithNonStandardPort_IncludesPortInAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:8080");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var attributes = InvokeAgentAttributesBuilder.BuildAttributes(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerPortKey);
            attributes[OpenTelemetryConstants.ServerPortKey].Should().Be(8080);
        }

        [TestMethod]
        public void BuildAttributes_WithStandardPort443_ExcludesPortFromAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:443");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var attributes = InvokeAgentAttributesBuilder.BuildAttributes(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            attributes.Should().NotContainKey(OpenTelemetryConstants.ServerPortKey);
        }

        [TestMethod]
        public void BuildAttributes_WithNullOptionalParameters_OmitsThoseAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var attributes = InvokeAgentAttributesBuilder.BuildAttributes(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            attributes.Should().NotContainKey(OpenTelemetryConstants.SessionIdKey);
            attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiCallerIdKey);
            attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiCallerAgentIdKey);
        }

        [TestMethod]
        public void BuildAttributes_MatchesOpenTelemetryScopeAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:8080");
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
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var attributes = InvokeAgentAttributesBuilder.BuildAttributes(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            // Agent details (from OpenTelemetryScope constructor)
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-123");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            attributes[OpenTelemetryConstants.GenAiAgentNameKey].Should().Be("TestAgent");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey);
            attributes[OpenTelemetryConstants.GenAiAgentDescriptionKey].Should().Be("Test Description");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentAUIDKey);
            attributes[OpenTelemetryConstants.GenAiAgentAUIDKey].Should().Be("auid-456");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentUPNKey);
            attributes[OpenTelemetryConstants.GenAiAgentUPNKey].Should().Be("agent@example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentBlueprintIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentBlueprintIdKey].Should().Be("blueprint-789");

            // Tenant details (from OpenTelemetryScope constructor)
            attributes.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);

            // InvokeAgentScope-specific attributes
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            attributes[OpenTelemetryConstants.ServerAddressKey].Should().Be("example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerPortKey);
            attributes[OpenTelemetryConstants.ServerPortKey].Should().Be(8080);
            attributes.Should().ContainKey(OpenTelemetryConstants.SessionIdKey);
            attributes[OpenTelemetryConstants.SessionIdKey].Should().Be("session-456");
        }
    }
}
