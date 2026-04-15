// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs.Builders
{
    [TestClass]
    public class InvokeAgentDataBuilderTests
    {
        [TestMethod]
        public void Build_IncludesRequestDetails_WhenRequestProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var request = new Request(
                "test content",
                sessionId: "session-456",
                channel: new Channel(name: "source-name", link: "source-description"));
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                request: request);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelNameKey);
            telemetry.Attributes[OpenTelemetryConstants.ChannelNameKey].Should().Be("source-name");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelLinkKey);
            telemetry.Attributes[OpenTelemetryConstants.ChannelLinkKey].Should().Be("source-description");
        }

        [TestMethod]
        public void Build_IncludesConversationId_WhenProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var conversationId = "conv-999";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiConversationIdKey].Should().Be("conv-999");
        }

        [TestMethod]
        public void Build_IncludesCallerDetails_WhenProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var callerDetails = new CallerDetails(userDetails: new UserDetails(userId: "caller-123", userName: "Caller Name", userEmail: "caller@example.com"));
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                callerDetails: callerDetails);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.UserIdKey);
            telemetry.Attributes[OpenTelemetryConstants.UserIdKey].Should().Be("caller-123");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.UserNameKey);
            telemetry.Attributes[OpenTelemetryConstants.UserNameKey].Should().Be("Caller Name");
        }

        [TestMethod]
        public void Build_IncludesCallerAgentDetails_WhenProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var callerAgentDetails = new AgentDetails("caller-agent-789", "CallerAgent");
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                callerDetails: new CallerDetails(callerAgentDetails: callerAgentDetails));

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.CallerAgentIdKey);
            telemetry.Attributes[OpenTelemetryConstants.CallerAgentIdKey].Should().Be("caller-agent-789");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.CallerAgentNameKey);
            telemetry.Attributes[OpenTelemetryConstants.CallerAgentNameKey].Should().Be("CallerAgent");
        }

        [TestMethod]
        public void Build_IncludesInputMessages_WhenProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var inputMessages = new[] { "Hello", "How are you?" };
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                inputMessages: inputMessages);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiInputMessagesKey]!.ToString()!.Should().Contain("Hello").And.Contain("How are you?");
        }

        [TestMethod]
        public void Build_IncludesOutputMessages_WhenProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var outputMessages = new[] { "Hi there!", "I'm fine." };
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                outputMessages: outputMessages);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey]!.ToString()!.Should().Contain("Hi there!").And.Contain("fine.").And.Contain("\"version\":\"0.1.0\"");
        }

        [TestMethod]
        public void Build_IncludesBothInputAndOutputMessages_WhenProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var inputMessages = new[] { "Hello" };
            var outputMessages = new[] { "Hi" };
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                inputMessages: inputMessages,
                outputMessages: outputMessages);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiInputMessagesKey]!.ToString()!.Should().Contain("Hello").And.Contain("\"version\":\"0.1.0\"");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey]!.ToString()!.Should().Contain("Hi").And.Contain("\"version\":\"0.1.0\"");
        }

        [TestMethod]
        public void Build_OmitsInputMessages_WhenEmptyArray()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var inputMessages = new string[] { };
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                inputMessages: inputMessages);

            // Assert
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
        }

        [TestMethod]
        public void Build_OmitsMessages_WhenNull()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                inputMessages: null,
                outputMessages: null);

            // Assert
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
        }

        [TestMethod]
        public void Build_SetsTimingInformation_WhenProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            var endTime = DateTimeOffset.UtcNow;
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                startTime: startTime,
                endTime: endTime);

            // Assert
            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().Be(endTime);
            telemetry.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void Build_SetsSpanIds_WhenProvided()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var spanId = "abc123def456";
            var parentSpanId = "parent789ghi012";
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                spanId: spanId,
                parentSpanId: parentSpanId);

            // Assert
            telemetry.SpanId.Should().Be(spanId);
            telemetry.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithAllParameters_SetsAllExpectedAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:8080");
            var agentDetails = new AgentDetails(
                "agent-123",
                "TestAgent",
                "Test Description",
                agenticUserId: "auid-456",
                agenticUserEmail: "agent@example.com",
                agentBlueprintId: "blueprint-789",
                tenantId: "tenant-999");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var request = new Request(
                "test content",
                sessionId: "session-456",
                channel: new Channel(name: "source-name", link: "source-description"));
            var callerAgentDetails = new AgentDetails("caller-agent-789", "CallerAgent");
            var fullCallerDetails = new CallerDetails(
                userDetails: new UserDetails(userId: "caller-123", userName: "Caller Name", userEmail: "caller@example.com"),
                callerAgentDetails: callerAgentDetails);
            var conversationId = "conv-999";
            var inputMessages = new[] { "Hello" };
            var outputMessages = new[] { "Hi" };
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);
            var endTime = DateTimeOffset.UtcNow;
            var spanId = "span123";
            var parentSpanId = "parent456";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                request: request,
                callerDetails: fullCallerDetails,
                inputMessages: inputMessages,
                outputMessages: outputMessages,
                startTime: startTime,
                endTime: endTime,
                spanId: spanId,
                parentSpanId: parentSpanId);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelNameKey);
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.UserIdKey);
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.CallerAgentIdKey);
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().Be(endTime);
            telemetry.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(100));
            telemetry.SpanId.Should().Be(spanId);
            telemetry.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithOnlyStartTime_DurationZero()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var startTime = DateTimeOffset.UtcNow;
            var conversationId = "conv-123";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                startTime: startTime);

            // Assert
            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().BeNull();
            telemetry.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Build_AddsExtraAttributes_WhenNotReserved()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-extra", "ExtraAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var conversationId = "conv-extra";
            var extras = new Dictionary<string, object?>
            {
                {"custom.key1", "value1"},
                {"custom.key2", 42},
            };

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                extraAttributes: extras);

            // Assert
            telemetry.Attributes.Should().ContainKey("custom.key1").WhoseValue.Should().Be("value1");
            telemetry.Attributes.Should().ContainKey("custom.key2").WhoseValue.Should().Be(42);
        }

        [TestMethod]
        public void Build_DoesNotOverrideReservedKeys_WithExtraAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-resv", "ReservedAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var conversationId = "conv-resv";
            var extras = new Dictionary<string, object?>
            {
                {OpenTelemetryConstants.GenAiAgentIdKey, "fake-id"}, // reserved
                {OpenTelemetryConstants.GenAiAgentNameKey, "fake-name"}, // reserved
                {"another.key", "present"}
            };

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                extraAttributes: extras);

            // Assert - reserved keys keep original values
            telemetry.Attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-resv");
            telemetry.Attributes[OpenTelemetryConstants.GenAiAgentNameKey].Should().Be("ReservedAgent");
            telemetry.Attributes.Should().ContainKey("another.key").WhoseValue.Should().Be("present");
        }

        [TestMethod]
        public void Build_IgnoresNullValues_InExtraAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-null", "NullAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var conversationId = "conv-null-extra";
            var extras = new Dictionary<string, object?>
            {
                {"custom.null", null},
                {"custom.good", "ok"}
            };

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                extraAttributes: extras);

            // Assert
            telemetry.Attributes.Should().NotContainKey("custom.null");
            telemetry.Attributes.Should().ContainKey("custom.good").WhoseValue.Should().Be("ok");
        }

        [TestMethod]
        public void Build_WithAgentPlatformId_SetsExpectedAttributes()
        {
            // Arrange
            var tenantId = Guid.NewGuid().ToString();
            var agentDetails = new AgentDetails(
                agentPlatformId: "agent-123",
                agentName: "TestAgent",
                agentDescription: "Test Description",
                tenantId: tenantId);
            var scopeDetails = new InvokeAgentScopeDetails();
            var request = new Request(
                "test content",
                sessionId: "session-456",
                channel: new Channel(name: "source-name", link: "source-description"));
            var callerDetails = new CallerDetails(userDetails: new UserDetails(userId: "caller-123", userName: "Caller Name", userEmail: "caller@example.com"));
            var conversationId = "conv-999";
            var inputMessages = new[] { "Hello" };
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                request: request,
                callerDetails: callerDetails,
                inputMessages: inputMessages,
                startTime: startTime);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.AgentPlatformIdKey);
            telemetry.Attributes[OpenTelemetryConstants.AgentPlatformIdKey].Should().Be("agent-123");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiAgentNameKey].Should().Be("TestAgent");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiAgentDescriptionKey].Should().Be("Test Description");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiConversationIdKey].Should().Be("conv-999");
            telemetry.StartTime.Should().Be(startTime);
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelNameKey);
            telemetry.Attributes[OpenTelemetryConstants.ChannelNameKey].Should().Be("source-name");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelLinkKey);
            telemetry.Attributes[OpenTelemetryConstants.ChannelLinkKey].Should().Be("source-description");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.UserIdKey);
            telemetry.Attributes[OpenTelemetryConstants.UserIdKey].Should().Be("caller-123");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.UserNameKey);
            telemetry.Attributes[OpenTelemetryConstants.UserNameKey].Should().Be("Caller Name");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.UserEmailKey);
            telemetry.Attributes[OpenTelemetryConstants.UserEmailKey].Should().Be("caller@example.com");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);
            telemetry.Attributes[OpenTelemetryConstants.TenantIdKey].Should().Be(tenantId);
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiInputMessagesKey]!.ToString()!.Should().Contain("Hello").And.Contain("\"version\":\"0.1.0\"");
        }

        [TestMethod]
        public void Build_SpanKind_DefaultsToNull()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var conversationId = "conv-sk-default";

            // Act
            var data = InvokeAgentDataBuilder.Build(scopeDetails, agentDetails, conversationId);

            // Assert
            data.SpanKind.Should().BeNull();
        }

        [TestMethod]
        public void Build_SpanKind_PassesThroughProvidedValue()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var scopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var conversationId = "conv-sk-server";

            // Act
            var data = InvokeAgentDataBuilder.Build(
                scopeDetails,
                agentDetails,
                conversationId,
                spanKind: SpanKindConstants.Server);

            // Assert
            data.SpanKind.Should().Be(SpanKindConstants.Server);
        }
    }
}
