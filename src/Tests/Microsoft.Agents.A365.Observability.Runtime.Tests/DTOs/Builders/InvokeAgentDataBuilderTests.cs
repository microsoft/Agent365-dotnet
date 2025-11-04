using FluentAssertions;
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
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var request = new Request(
                "test content",
                ExecutionType.HumanToAgent,
                "session-456",
                new SourceMetadata("source-id", "source-name", Role.Human, "source-description"));

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                request: request);

            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiExecutionSourceIdKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiExecutionSourceIdKey].Should().Be("source-id");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiExecutionTypeKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiExecutionTypeKey].Should().Be("HumanToAgent");
        }

        [TestMethod]
        public void Build_IncludesConversationId_WhenProvided()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-999";

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                conversationId: conversationId);

            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiConversationIdKey].Should().Be("conv-999");
        }

        [TestMethod]
        public void Build_IncludesCallerDetails_WhenProvided()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var callerDetails = new CallerDetails("caller-123", "Caller Name", "caller@example.com");

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                callerDetails: callerDetails);

            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerIdKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiCallerIdKey].Should().Be("caller-123");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerNameKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiCallerNameKey].Should().Be("Caller Name");
        }

        [TestMethod]
        public void Build_IncludesCallerAgentDetails_WhenProvided()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var callerAgentDetails = new AgentDetails("caller-agent-789", "CallerAgent");

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                callerAgentDetails: callerAgentDetails);

            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentIdKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiCallerAgentIdKey].Should().Be("caller-agent-789");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentNameKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiCallerAgentNameKey].Should().Be("CallerAgent");
        }

        [TestMethod]
        public void Build_IncludesInputMessages_WhenProvided()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var inputMessages = new[] { "Hello", "How are you?" };

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                inputMessages: inputMessages);

            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiInputMessagesKey].Should().Be("Hello,How are you?");
        }

        [TestMethod]
        public void Build_IncludesOutputMessages_WhenProvided()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var outputMessages = new[] { "Hi there!", "I'm fine." };

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                outputMessages: outputMessages);

            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey].Should().Be("Hi there!,I'm fine.");
        }

        [TestMethod]
        public void Build_IncludesBothInputAndOutputMessages_WhenProvided()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var inputMessages = new[] { "Hello" };
            var outputMessages = new[] { "Hi" };

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                inputMessages: inputMessages,
                outputMessages: outputMessages);

            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiInputMessagesKey].Should().Be("Hello");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey].Should().Be("Hi");
        }

        [TestMethod]
        public void Build_OmitsInputMessages_WhenEmptyArray()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var inputMessages = new string[] { };

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                inputMessages: inputMessages);

            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
        }

        [TestMethod]
        public void Build_OmitsMessages_WhenNull()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                inputMessages: null,
                outputMessages: null);

            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
        }

        [TestMethod]
        public void Build_SetsTimingInformation_WhenProvided()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            var endTime = DateTimeOffset.UtcNow;

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                startTime: startTime,
                endTime: endTime);

            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().Be(endTime);
            telemetry.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void Build_SetsSpanIds_WhenProvided()
        {
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var spanId = "abc123def456";
            var parentSpanId = "parent789ghi012";

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                spanId: spanId,
                parentSpanId: parentSpanId);

            telemetry.SpanId.Should().Be(spanId);
            telemetry.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithAllParameters_SetsAllExpectedAttributes()
        {
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
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);
            var endTime = DateTimeOffset.UtcNow;
            var spanId = "span123";
            var parentSpanId = "parent456";

            var telemetry = InvokeAgentDataBuilder.Build(
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

            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiExecutionSourceIdKey);
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerIdKey);
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentIdKey);
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
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var startTime = DateTimeOffset.UtcNow;

            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                startTime: startTime);

            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().BeNull();
            telemetry.Duration.Should().Be(TimeSpan.Zero);
        }
    }
}
