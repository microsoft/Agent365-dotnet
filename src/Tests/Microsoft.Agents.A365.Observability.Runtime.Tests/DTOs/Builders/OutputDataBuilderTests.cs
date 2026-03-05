// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs.Builders
{
    [TestClass]
    public class OutputDataBuilderTests
    {
        [TestMethod]
        public void Build_WithMinimalParameters_SetsBasicAttributes()
        {
            var agent = new AgentDetails("agent-1", "AgentOne");
            var tenant = new TenantDetails(Guid.NewGuid());
            var response = new Response(new[] { "Hello" });

            var data = OutputDataBuilder.Build(agent, tenant, response);

            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOperationNameKey).WhoseValue.Should().Be("output_messages");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey).WhoseValue.Should().Be("agent-1");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey).WhoseValue.Should().Be("Hello");
            data.Name.Should().Be("OutputMessages");

            // Null optional parameters should be omitted
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.ChannelNameKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.ChannelLinkKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.CallerIdKey);
        }

        [TestMethod]
        public void Build_WithMultipleOutputMessages_JoinsMessages()
        {
            var agent = new AgentDetails("agent-2");
            var tenant = new TenantDetails(Guid.NewGuid());
            var response = new Response(new[] { "Hello", "World" });

            var data = OutputDataBuilder.Build(agent, tenant, response);

            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey).WhoseValue.Should().Be("Hello,World");
        }

        [TestMethod]
        public void Build_WithFullAgentDetails_IncludesAllAgentAttributes()
        {
            var agent = new AgentDetails(
                "agent-3",
                "AgentThree",
                "Description",
                agentAUID: "auid",
                agentUPN: "upn@example.com",
                agentBlueprintId: "bp-1",
                agentPlatformId: "platform-1",
                agentType: AgentType.MicrosoftCopilot);
            var tenant = new TenantDetails(Guid.NewGuid());
            var response = new Response(new[] { "Test" });

            var data = OutputDataBuilder.Build(agent, tenant, response);

            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey).WhoseValue.Should().Be("agent-3");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey).WhoseValue.Should().Be("AgentThree");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey).WhoseValue.Should().Be("Description");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentAUIDKey).WhoseValue.Should().Be("auid");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentUPNKey).WhoseValue.Should().Be("upn@example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentBlueprintIdKey).WhoseValue.Should().Be("bp-1");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentPlatformIdKey).WhoseValue.Should().Be("platform-1");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentTypeKey).WhoseValue.Should().Be("MicrosoftCopilot");
        }

        [TestMethod]
        public void Build_WithTimingAndSpanIds_SetsAllValues()
        {
            var agent = new AgentDetails("agent-4");
            var tenant = new TenantDetails(Guid.NewGuid());
            var response = new Response(new[] { "Test" });
            var start = DateTimeOffset.UtcNow.AddMinutes(-3);
            var end = DateTimeOffset.UtcNow;

            var data = OutputDataBuilder.Build(agent, tenant, response, startTime: start, endTime: end, spanId: "span-1", parentSpanId: "parent-1");

            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(3), TimeSpan.FromMilliseconds(100));
            data.SpanId.Should().Be("span-1");
            data.ParentSpanId.Should().Be("parent-1");
        }

        [TestMethod]
        public void Build_WithEmptyResponseMessages_OmitsOutputMessagesAttribute()
        {
            var agent = new AgentDetails("agent-5");
            var tenant = new TenantDetails(Guid.NewGuid());
            var response = new Response(Array.Empty<string>());

            var data = OutputDataBuilder.Build(agent, tenant, response);

            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
        }

        [TestMethod]
        public void Build_WithExtraAttributes_AddsNonReservedAndIgnoresReserved()
        {
            var agent = new AgentDetails("agent-6");
            var tenant = new TenantDetails(Guid.NewGuid());
            var response = new Response(new[] { "real-output" });
            var extras = new Dictionary<string, object?>
            {
                { OpenTelemetryConstants.GenAiOutputMessagesKey, "fake-output" },
                { "output.custom", "abc" },
                { "output.null", null }
            };

            var data = OutputDataBuilder.Build(agent, tenant, response, extraAttributes: extras);

            data.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey].Should().Be("real-output");
            data.Attributes.Should().ContainKey("output.custom").WhoseValue.Should().Be("abc");
            data.Attributes.Should().NotContainKey("output.null");
        }

        [TestMethod]
        public void Build_WithAllParameters_SetsAllExpectedAttributes()
        {
            // Arrange
            var agent = new AgentDetails("agent-all", "AgentAll", "Desc", agentAUID: "auid-all", agentUPN: "upn-all@example.com", agentBlueprintId: "bp-all", agentPlatformId: "platform-all");
            var tenant = new TenantDetails(Guid.NewGuid());
            var response = new Response(new[] { "Hello", "World" });
            var conversationId = "conv-output-all";
            var source = new SourceMetadata(id: "src-id", name: "ChannelOutput", role: Role.Human, description: "https://channel/output");
            var callerDetails = new CallerDetails("caller-output-123", "Output Caller Name", "calleroutput@example.com", System.Net.IPAddress.Parse("192.168.1.50"), "caller-tenant-output");
            var start = DateTimeOffset.UtcNow.AddSeconds(-5);
            var end = DateTimeOffset.UtcNow;
            var spanId = "span-all-output";
            var parentSpanId = "parent-all-output";

            // Act
            var data = OutputDataBuilder.Build(
                agent,
                tenant,
                response,
                conversationId: conversationId,
                sourceMetadata: source,
                callerDetails: callerDetails,
                startTime: start,
                endTime: end,
                spanId: spanId,
                parentSpanId: parentSpanId);

            // Assert
            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be("conv-output-all");
            attrs.Should().ContainKey(OpenTelemetryConstants.ChannelNameKey).WhoseValue.Should().Be("ChannelOutput");
            attrs.Should().ContainKey(OpenTelemetryConstants.ChannelLinkKey).WhoseValue.Should().Be("https://channel/output");
            attrs.Should().ContainKey(OpenTelemetryConstants.CallerIdKey).WhoseValue.Should().Be("caller-output-123");
            attrs.Should().ContainKey(OpenTelemetryConstants.CallerNameKey).WhoseValue.Should().Be("Output Caller Name");
            attrs.Should().ContainKey(OpenTelemetryConstants.CallerUpnKey).WhoseValue.Should().Be("calleroutput@example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.CallerClientIpKey).WhoseValue.Should().Be("192.168.1.50");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey).WhoseValue.Should().Be("Hello,World");
            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(end - start, TimeSpan.FromMilliseconds(100));
            data.SpanId.Should().Be(spanId);
            data.ParentSpanId.Should().Be(parentSpanId);
        }
    }
}
