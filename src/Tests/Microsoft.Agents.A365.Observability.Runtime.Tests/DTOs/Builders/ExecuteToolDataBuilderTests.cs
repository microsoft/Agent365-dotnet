using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs.Builders
{
    [TestClass]
    public class ExecuteToolDataBuilderTests
    {
        [TestMethod]
        public void Build_WithMinimalParameters_SetsBasicAttributes()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolA", "{a:1}");
            var agent = new AgentDetails("agent-1", "AgentOne");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-min";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiToolNameKey).WhoseValue.Should().Be("toolA");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiToolArgumentsKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey).WhoseValue.Should().Be("agent-1");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
        }

        [TestMethod]
        public void Build_WithSourceMetadata_IncludesChannelAttributes()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolSource", null);
            var agent = new AgentDetails("agent-src");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-src-tool";
            var source = new SourceMetadata(id: "src-id", name: "ChannelTool", role: Role.Agent, description: "https://channel/tool");

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId, sourceMetadata: source);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiChannelNameKey).WhoseValue.Should().Be("ChannelTool");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiChannelLinkKey).WhoseValue.Should().Be("https://channel/tool");
        }

        [TestMethod]
        public void Build_WithFullToolDetails_IncludesAllToolAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:7071");
            var toolDetails = new ToolCallDetails("toolB", "{b:2}", "call-123", "Test tool", "function", endpoint);
            var agent = new AgentDetails("agent-2", "AgentTwo", "Desc", agentAUID: "auid", agentUPN: "upn@example.com", agentBlueprintId: "bp-1");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-full";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId);

            // Assert
            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolCallIdKey).WhoseValue.Should().Be("call-123");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolDescriptionKey).WhoseValue.Should().Be("Test tool");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolTypeKey).WhoseValue.Should().Be("function");
            attrs.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey).WhoseValue.Should().Be("example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.ServerPortKey).WhoseValue.Should().Be(7071);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentAUIDKey).WhoseValue.Should().Be("auid");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentUPNKey).WhoseValue.Should().Be("upn@example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentBlueprintIdKey).WhoseValue.Should().Be("bp-1");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
        }

        [TestMethod]
        public void Build_WithNonStandardPort_IncludesPort()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolC", null, endpoint: new Uri("https://example.com:8081"));
            var agent = new AgentDetails("agent-3");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-port";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.ServerPortKey).WhoseValue.Should().Be(8081);
        }

        [TestMethod]
        public void Build_WithStandardPort443_ExcludesPort()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolD", null, endpoint: new Uri("https://example.com:443"));
            var agent = new AgentDetails("agent-4");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-443";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.ServerPortKey);
        }

        [TestMethod]
        public void Build_WithResponseContent_IncludesEventContent()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolE", null);
            var agent = new AgentDetails("agent-5");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-content";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId, responseContent: "result-value");

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiEventContent).WhoseValue.Should().Be("result-value");
        }

        [TestMethod]
        public void Build_WithConversationId_IncludesConversationId()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolF", null);
            var agent = new AgentDetails("agent-6");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-123";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be("conv-123");
        }

        [TestMethod]
        public void Build_WithNullOptionalParameters_OmitsThoseAttributes()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolG", null); // no optional fields, no endpoint
            var agent = new AgentDetails("agent-7");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-null";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId);

            // Assert
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiToolCallIdKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiToolDescriptionKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiToolTypeKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiEventContent);
        }

        [TestMethod]
        public void Build_SetsTimingInformation_WhenProvided()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolH", null);
            var agent = new AgentDetails("agent-8");
            var tenant = new TenantDetails(Guid.NewGuid());
            var start = DateTimeOffset.UtcNow.AddMinutes(-3);
            var end = DateTimeOffset.UtcNow;
            var conversationId = "conv-time";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId, startTime: start, endTime: end);

            // Assert
            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(3), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void Build_SetsSpanIds_WhenProvided()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolI", null);
            var agent = new AgentDetails("agent-9");
            var tenant = new TenantDetails(Guid.NewGuid());
            var spanId = "span-tool";
            var parentSpanId = "parent-tool";
            var conversationId = "conv-span";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId, spanId: spanId, parentSpanId: parentSpanId);

            // Assert
            data.SpanId.Should().Be(spanId);
            data.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithAllParameters_SetsAllExpectedAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.org:6060");
            var toolDetails = new ToolCallDetails("toolJ", "{x:1}", "call-999", "Full tool", "extension", endpoint);
            var agent = new AgentDetails("agent-10", "AgentTen", "Desc", agentAUID: "auid10", agentUPN: "upn10@example.com", agentBlueprintId: "bp-10");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-all";
            var start = DateTimeOffset.UtcNow.AddSeconds(-30);
            var end = DateTimeOffset.UtcNow;
            var spanId = "span-all";
            var parentSpanId = "parent-all";
            var responseContent = "tool-response";

            // Act
            var data = ExecuteToolDataBuilder.Build(
                toolDetails,
                agent,
                tenant,
                conversationId,
                responseContent: responseContent,
                startTime: start,
                endTime: end,
                spanId: spanId,
                parentSpanId: parentSpanId);

            // Assert
            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolCallIdKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolDescriptionKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolTypeKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiEventContent);
            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(end - start, TimeSpan.FromMilliseconds(100));
            data.SpanId.Should().Be(spanId);
            data.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithOnlyStartTime_DurationZero()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolK", null);
            var agent = new AgentDetails("agent-11");
            var tenant = new TenantDetails(Guid.NewGuid());
            var start = DateTimeOffset.UtcNow;
            var conversationId = "conv-zero";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, tenant, conversationId, startTime: start);

            // Assert
            data.StartTime.Should().Be(start);
            data.EndTime.Should().BeNull();
            data.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Build_AddsExtraAttributes_WhenNotReserved()
        {
            // Arrange
            var tool = new ToolCallDetails("tool-extra", null);
            var agent = new AgentDetails("agent-extra", "ExtraToolAgent");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-extra-tool";
            var extras = new Dictionary<string, object?>
            {
                {"tool.custom", "abc"},
                {"tool.number", 7}
            };

            // Act
            var data = ExecuteToolDataBuilder.Build(tool, agent, tenant, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes.Should().ContainKey("tool.custom").WhoseValue.Should().Be("abc");
            data.Attributes.Should().ContainKey("tool.number").WhoseValue.Should().Be(7);
        }

        [TestMethod]
        public void Build_DoesNotOverrideReservedKeys_WithExtraAttributes()
        {
            // Arrange
            var tool = new ToolCallDetails("tool-resv", null);
            var agent = new AgentDetails("agent-resv", "ReservedToolAgent");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-resv-tool";
            var extras = new Dictionary<string, object?>
            {
                {OpenTelemetryConstants.GenAiToolNameKey, "fake-tool"},
                {OpenTelemetryConstants.GenAiAgentIdKey, "fake-agent"},
                {"tool.other", "ok"}
            };

            // Act
            var data = ExecuteToolDataBuilder.Build(tool, agent, tenant, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes[OpenTelemetryConstants.GenAiToolNameKey].Should().Be("tool-resv");
            data.Attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-resv");
            data.Attributes.Should().ContainKey("tool.other").WhoseValue.Should().Be("ok");
        }

        [TestMethod]
        public void Build_IgnoresNullValues_InExtraAttributes()
        {
            // Arrange
            var tool = new ToolCallDetails("tool-null", null);
            var agent = new AgentDetails("agent-null", "NullToolAgent");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-null-tool";
            var extras = new Dictionary<string, object?>
            {
                {"tool.null", null},
                {"tool.valid", "yes"}
            };

            // Act
            var data = ExecuteToolDataBuilder.Build(tool, agent, tenant, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes.Should().NotContainKey("tool.null");
            data.Attributes.Should().ContainKey("tool.valid").WhoseValue.Should().Be("yes");
        }
    }
}
