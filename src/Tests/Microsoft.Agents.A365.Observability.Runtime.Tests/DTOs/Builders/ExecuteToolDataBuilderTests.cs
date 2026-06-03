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
    public class ExecuteToolDataBuilderTests
    {
        [TestMethod]
        public void Build_WithMinimalParameters_SetsBasicAttributes()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolA", "{a:1}");
            var agent = new AgentDetails("agent-1", "AgentOne", tenantId: Guid.NewGuid().ToString());
            var conversationId = "conv-min";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiToolNameKey).WhoseValue.Should().Be("toolA");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiToolArgumentsKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey).WhoseValue.Should().Be("agent-1");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
        }

        [TestMethod]
        public void Build_WithChannel_IncludesChannelAttributes()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolSource", (string?)null);
            var agent = new AgentDetails("agent-src");
            var conversationId = "conv-src-tool";
            var source = new Channel(name: "ChannelTool", link: "https://channel/tool");

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId, channel: source);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelNameKey).WhoseValue.Should().Be("ChannelTool");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelLinkKey).WhoseValue.Should().Be("https://channel/tool");
        }

        [TestMethod]
        public void Build_WithFullToolDetails_IncludesAllToolAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:7071");
            var toolDetails = new ToolCallDetails("toolB", "{b:2}", "call-123", "Test tool", "function", endpoint, "my-tool-server");
            var agent = new AgentDetails("agent-2", "AgentTwo", "Desc", agenticUserId: "auid", agenticUserEmail: "upn@example.com", agentBlueprintId: "bp-1");
            var conversationId = "conv-full";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId);

            // Assert
            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolCallIdKey).WhoseValue.Should().Be("call-123");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolDescriptionKey).WhoseValue.Should().Be("Test tool");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolTypeKey).WhoseValue.Should().Be("function");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolServerNameKey).WhoseValue.Should().Be("my-tool-server");
            attrs.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey).WhoseValue.Should().Be("example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.ServerPortKey).WhoseValue.Should().Be("7071");
            attrs.Should().ContainKey(OpenTelemetryConstants.AgentAUIDKey).WhoseValue.Should().Be("auid");
            attrs.Should().ContainKey(OpenTelemetryConstants.AgentEmailKey).WhoseValue.Should().Be("upn@example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.AgentBlueprintIdKey).WhoseValue.Should().Be("bp-1");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
        }

        [TestMethod]
        public void Build_WithNonStandardPort_IncludesPort()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolC", (string?)null, endpoint: new Uri("https://example.com:8081"));
            var agent = new AgentDetails("agent-3");
            var conversationId = "conv-port";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.ServerPortKey).WhoseValue.Should().Be("8081");
        }

        [TestMethod]
        public void Build_WithStandardPort443_ExcludesPort()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolD", (string?)null, endpoint: new Uri("https://example.com:443"));
            var agent = new AgentDetails("agent-4");
            var conversationId = "conv-443";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.ServerPortKey);
        }

        [TestMethod]
        public void Build_WithResponseContent_IncludesEventContent()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolE", (string?)null);
            var agent = new AgentDetails("agent-5");
            var conversationId = "conv-content";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId, responseContent: "result-value");

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiToolCallResultKey).WhoseValue!.ToString()!.Should().Contain("result-value");
        }

        [TestMethod]
        public void Build_WithConversationId_IncludesConversationId()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolF", (string?)null);
            var agent = new AgentDetails("agent-6");
            var conversationId = "conv-123";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be("conv-123");
        }

        [TestMethod]
        public void Build_WithNullOptionalParameters_OmitsThoseAttributes()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolG", (string?)null); // no optional fields, no endpoint
            var agent = new AgentDetails("agent-7");
            var conversationId = "conv-null";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId);

            // Assert
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiToolCallIdKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiToolDescriptionKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiToolTypeKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiToolServerNameKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiToolCallResultKey);
        }

        [TestMethod]
        public void Build_SetsTimingInformation_WhenProvided()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolH", (string?)null);
            var agent = new AgentDetails("agent-8");
            var start = DateTimeOffset.UtcNow.AddMinutes(-3);
            var end = DateTimeOffset.UtcNow;
            var conversationId = "conv-time";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId, startTime: start, endTime: end);

            // Assert
            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(3), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void Build_SetsSpanIds_WhenProvided()
        {
            // Arrange
            var toolDetails = new ToolCallDetails("toolI", (string?)null);
            var agent = new AgentDetails("agent-9");
            var spanId = "span-tool";
            var parentSpanId = "parent-tool";
            var conversationId = "conv-span";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId, spanId: spanId, parentSpanId: parentSpanId);

            // Assert
            data.SpanId.Should().Be(spanId);
            data.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithAllParameters_SetsAllExpectedAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.org:6060");
            var toolDetails = new ToolCallDetails("toolJ", "{x:1}", "call-999", "Full tool", "extension", endpoint, "full-tool-server");
            var agent = new AgentDetails("agent-10", "AgentTen", "Desc", agenticUserId: "auid10", agenticUserEmail: "upn10@example.com", agentBlueprintId: "bp-10");
            var conversationId = "conv-all";
            var start = DateTimeOffset.UtcNow.AddSeconds(-30);
            var end = DateTimeOffset.UtcNow;
            var spanId = "span-all";
            var parentSpanId = "parent-all";
            var responseContent = "tool-response";
            var callerDetails = new CallerDetails(userDetails: new UserDetails(userId: "caller-tool-123", userName: "Caller Tool Name", userEmail: "callertool@example.com", userClientIP: System.Net.IPAddress.Parse("10.0.0.50")));

            // Act
            var data = ExecuteToolDataBuilder.Build(
                toolDetails,
                agent,
                conversationId,
                responseContent: responseContent,
                startTime: start,
                endTime: end,
                spanId: spanId,
                parentSpanId: parentSpanId,
                callerDetails: callerDetails);

            // Assert
            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolCallIdKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolDescriptionKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolTypeKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolServerNameKey).WhoseValue.Should().Be("full-tool-server");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiToolCallResultKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.UserIdKey).WhoseValue.Should().Be("caller-tool-123");
            attrs.Should().ContainKey(OpenTelemetryConstants.UserNameKey).WhoseValue.Should().Be("Caller Tool Name");
            attrs.Should().ContainKey(OpenTelemetryConstants.UserEmailKey).WhoseValue.Should().Be("callertool@example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.CallerClientIpKey).WhoseValue.Should().Be("10.0.0.50");
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
            var toolDetails = new ToolCallDetails("toolK", (string?)null);
            var agent = new AgentDetails("agent-11");
            var start = DateTimeOffset.UtcNow;
            var conversationId = "conv-zero";

            // Act
            var data = ExecuteToolDataBuilder.Build(toolDetails, agent, conversationId, startTime: start);

            // Assert
            data.StartTime.Should().Be(start);
            data.EndTime.Should().BeNull();
            data.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Build_AddsExtraAttributes_WhenNotReserved()
        {
            // Arrange
            var tool = new ToolCallDetails("tool-extra", (string?)null);
            var agent = new AgentDetails("agent-extra", "ExtraToolAgent");
            var conversationId = "conv-extra-tool";
            var extras = new Dictionary<string, object?>
            {
                {"tool.custom", "abc"},
                {"tool.number", 7}
            };

            // Act
            var data = ExecuteToolDataBuilder.Build(tool, agent, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes.Should().ContainKey("tool.custom").WhoseValue.Should().Be("abc");
            data.Attributes.Should().ContainKey("tool.number").WhoseValue.Should().Be(7);
        }

        [TestMethod]
        public void Build_DoesNotOverrideReservedKeys_WithExtraAttributes()
        {
            // Arrange
            var tool = new ToolCallDetails("tool-resv", (string?)null);
            var agent = new AgentDetails("agent-resv", "ReservedToolAgent");
            var conversationId = "conv-resv-tool";
            var extras = new Dictionary<string, object?>
            {
                {OpenTelemetryConstants.GenAiToolNameKey, "fake-tool"},
                {OpenTelemetryConstants.GenAiAgentIdKey, "fake-agent"},
                {"tool.other", "ok"}
            };

            // Act
            var data = ExecuteToolDataBuilder.Build(tool, agent, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes[OpenTelemetryConstants.GenAiToolNameKey].Should().Be("tool-resv");
            data.Attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-resv");
            data.Attributes.Should().ContainKey("tool.other").WhoseValue.Should().Be("ok");
        }

        [TestMethod]
        public void Build_IgnoresNullValues_InExtraAttributes()
        {
            // Arrange
            var tool = new ToolCallDetails("tool-null", (string?)null);
            var agent = new AgentDetails("agent-null", "NullToolAgent");
            var conversationId = "conv-null-tool";
            var extras = new Dictionary<string, object?>
            {
                {"tool.null", null},
                {"tool.valid", "yes"}
            };

            // Act
            var data = ExecuteToolDataBuilder.Build(tool, agent, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes.Should().NotContainKey("tool.null");
            data.Attributes.Should().ContainKey("tool.valid").WhoseValue.Should().Be("yes");
        }

        [TestMethod]
        public void Build_SpanKind_DefaultsToNull()
        {
            // Arrange
            var tool = new ToolCallDetails("toolSK", (string?)null);
            var agent = new AgentDetails("agent-sk");
            var conversationId = "conv-sk-default";

            // Act
            var data = ExecuteToolDataBuilder.Build(tool, agent, conversationId);

            // Assert
            data.SpanKind.Should().BeNull();
        }

        [TestMethod]
        public void Build_SpanKind_PassesThroughProvidedValue()
        {
            // Arrange
            var tool = new ToolCallDetails("toolSK", (string?)null);
            var agent = new AgentDetails("agent-sk");
            var conversationId = "conv-sk-client";

            // Act
            var data = ExecuteToolDataBuilder.Build(
                tool, agent, conversationId,
                spanKind: SpanKindConstants.Client);

            // Assert
            data.SpanKind.Should().Be(SpanKindConstants.Client);
        }
    }
}
