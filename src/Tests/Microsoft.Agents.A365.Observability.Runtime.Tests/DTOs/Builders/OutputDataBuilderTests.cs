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
            var agent = new AgentDetails("agent-1", "AgentOne", tenantId: Guid.NewGuid().ToString());
            var response = new Response(new[] { "Hello" });

            var data = OutputDataBuilder.Build(agent, response);

            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOperationNameKey).WhoseValue.Should().Be("output_messages");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey).WhoseValue.Should().Be("agent-1");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey).WhoseValue!.ToString()!.Should().Contain("Hello").And.Contain("\"version\":\"0.1.0\"");
            data.Name.Should().Be("OutputMessages");

            // Null optional parameters should be omitted
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.ChannelNameKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.ChannelLinkKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.UserIdKey);
        }

        [TestMethod]
        public void Build_WithMultipleOutputMessages_JoinsMessages()
        {
            var agent = new AgentDetails("agent-2");
            var response = new Response(new[] { "Hello", "World" });

            var data = OutputDataBuilder.Build(agent, response);

            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey).WhoseValue!.ToString()!.Should().Contain("Hello").And.Contain("World").And.Contain("\"version\":\"0.1.0\"");
        }

        [TestMethod]
        public void Build_WithFullAgentDetails_IncludesAllAgentAttributes()
        {
            var agent = new AgentDetails(
                "agent-3",
                "AgentThree",
                "Description",
                agenticUserId: "auid",
                agenticUserEmail: "upn@example.com",
                agentBlueprintId: "bp-1",
                agentPlatformId: "platform-1",
                agentType: AgentType.MicrosoftCopilot);
            var response = new Response(new[] { "Test" });

            var data = OutputDataBuilder.Build(agent, response);

            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey).WhoseValue.Should().Be("agent-3");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey).WhoseValue.Should().Be("AgentThree");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey).WhoseValue.Should().Be("Description");
            attrs.Should().ContainKey(OpenTelemetryConstants.AgentAUIDKey).WhoseValue.Should().Be("auid");
            attrs.Should().ContainKey(OpenTelemetryConstants.AgentEmailKey).WhoseValue.Should().Be("upn@example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.AgentBlueprintIdKey).WhoseValue.Should().Be("bp-1");
            attrs.Should().ContainKey(OpenTelemetryConstants.AgentPlatformIdKey).WhoseValue.Should().Be("platform-1");

        }

        [TestMethod]
        public void Build_WithTimingAndSpanIds_SetsAllValues()
        {
            var agent = new AgentDetails("agent-4");
            var response = new Response(new[] { "Test" });
            var start = DateTimeOffset.UtcNow.AddMinutes(-3);
            var end = DateTimeOffset.UtcNow;

            var data = OutputDataBuilder.Build(agent, response, startTime: start, endTime: end, spanId: "span-1", parentSpanId: "parent-1");

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
            var response = new Response(Array.Empty<string>());

            var data = OutputDataBuilder.Build(agent, response);

            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
        }

        [TestMethod]
        public void Build_WithExtraAttributes_AddsNonReservedAndIgnoresReserved()
        {
            var agent = new AgentDetails("agent-6");
            var response = new Response(new[] { "real-output" });
            var extras = new Dictionary<string, object?>
            {
                { OpenTelemetryConstants.GenAiOutputMessagesKey, "fake-output" },
                { "output.custom", "abc" },
                { "output.null", null }
            };

            var data = OutputDataBuilder.Build(agent, response, extraAttributes: extras);

            data.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey]!.ToString()!.Should().Contain("real-output").And.Contain("\"version\":\"0.1.0\"");
            data.Attributes.Should().ContainKey("output.custom").WhoseValue.Should().Be("abc");
            data.Attributes.Should().NotContainKey("output.null");
        }

        [TestMethod]
        public void Build_WithAllParameters_SetsAllExpectedAttributes()
        {
            // Arrange
            var agent = new AgentDetails("agent-all", "AgentAll", "Desc", agenticUserId: "auid-all", agenticUserEmail: "upn-all@example.com", agentBlueprintId: "bp-all", agentPlatformId: "platform-all");
            var response = new Response(new[] { "Hello", "World" });
            var conversationId = "conv-output-all";
            var source = new Channel(name: "ChannelOutput", link: "https://channel/output");
            var callerDetails = new CallerDetails(
                userDetails: new UserDetails(
                    userId: "caller-output-123",
                    userName: "Output Caller Name",
                    userEmail: "calleroutput@example.com",
                    userClientIP: System.Net.IPAddress.Parse("192.168.1.50")));
            var start = DateTimeOffset.UtcNow.AddSeconds(-5);
            var end = DateTimeOffset.UtcNow;
            var spanId = "span-all-output";
            var parentSpanId = "parent-all-output";

            // Act
            var data = OutputDataBuilder.Build(
                agent,
                response,
                conversationId: conversationId,
                channel: source,
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
            attrs.Should().ContainKey(OpenTelemetryConstants.UserIdKey).WhoseValue.Should().Be("caller-output-123");
            attrs.Should().ContainKey(OpenTelemetryConstants.UserNameKey).WhoseValue.Should().Be("Output Caller Name");
            attrs.Should().ContainKey(OpenTelemetryConstants.UserEmailKey).WhoseValue.Should().Be("calleroutput@example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.CallerClientIpKey).WhoseValue.Should().Be("192.168.1.50");
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey).WhoseValue!.ToString()!.Should().Contain("Hello").And.Contain("World").And.Contain("\"version\":\"0.1.0\"");
            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(end - start, TimeSpan.FromMilliseconds(100));
            data.SpanId.Should().Be(spanId);
            data.ParentSpanId.Should().Be(parentSpanId);
        }
    }
}
