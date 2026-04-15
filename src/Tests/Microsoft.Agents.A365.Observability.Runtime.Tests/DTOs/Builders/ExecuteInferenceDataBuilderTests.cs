// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs.Builders
{
    [TestClass]
    public class ExecuteInferenceDataBuilderTests
    {
        [TestMethod]
        public void Build_WithMinimalParameters_SetsBasicAttributes()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-1", "AgentOne");
            var conversationId = "conv-min";

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOperationNameKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiRequestModelKey).WhoseValue.Should().Be("gpt-4o");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiProviderNameKey).WhoseValue.Should().Be("openai");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
        }

        [TestMethod]
        public void Build_WithChannel_IncludesChannelAttributes()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-src");
            var conversationId = "conv-src-inf";
            var source = new Channel(name: "ChannelInf", link: "https://channel/inf");

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId, channel: source);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelNameKey).WhoseValue.Should().Be("ChannelInf");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.ChannelLinkKey).WhoseValue.Should().Be("https://channel/inf");
        }

        [TestMethod]
        public void Build_WithTokensAndFinishReasons_IncludesUsageAndReasons()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai", 10, 20, new[]{"stop"}, "resp-1");
            var agent = new AgentDetails("agent-2");
            var conversationId = "conv-tokens";

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiUsageInputTokensKey).WhoseValue.Should().Be("10");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiUsageOutputTokensKey).WhoseValue.Should().Be("20");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiResponseFinishReasonsKey).WhoseValue.Should().Be("stop");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
        }

        [TestMethod]
        public void Build_WithMessages_IncludesInputAndOutput()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-3");
            var input = new[]{"Hello"};
            var output = new[]{"Hi"};
            var conversationId = "conv-msg";

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId, inputMessages: input, outputMessages: output);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey).WhoseValue!.ToString()!.Should().Contain("Hello").And.Contain("\"version\":\"0.1.0\"");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey).WhoseValue!.ToString()!.Should().Contain("Hi").And.Contain("\"version\":\"0.1.0\"");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be(conversationId);
        }

        [TestMethod]
        public void Build_WithConversationId_IncludesConversationId()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-4");
            var conversationId = "conv-456";

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId);

            // Assert
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be("conv-456");
        }

        [TestMethod]
        public void Build_WithNullOptionalParameters_OmitsThoseAttributes()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-5");
            var conversationId = "conv-null";

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId);

            // Assert
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiUsageInputTokensKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiUsageOutputTokensKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiResponseFinishReasonsKey);
        }

        [TestMethod]
        public void Build_SetsTimingInformation_WhenProvided()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-6");
            var start = DateTimeOffset.UtcNow.AddMinutes(-2);
            var end = DateTimeOffset.UtcNow;
            var conversationId = "conv-time";

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId, startTime: start, endTime: end);

            // Assert
            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void Build_SetsSpanIds_WhenProvided()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-7");
            var spanId = "span-inf";
            var parentSpanId = "parent-inf";
            var conversationId = "conv-span";

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId, spanId: spanId, parentSpanId: parentSpanId);

            // Assert
            data.SpanId.Should().Be(spanId);
            data.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithAllParameters_SetsAllExpectedAttributes()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai", 33, 44, new[]{"length","stop"}, "resp-all");
            var agent = new AgentDetails("agent-8", "AgentEight", "Desc", agenticUserId: "auid8", agenticUserEmail: "upn8@example.com", agentBlueprintId: "bp-8");
            var conversationId = "conv-all";
            var input = new[]{"Hello"};
            var output = new[]{"World"};
            var start = DateTimeOffset.UtcNow.AddSeconds(-10);
            var end = DateTimeOffset.UtcNow;
            var spanId = "span-all-inf";
            var parentSpanId = "parent-all-inf";
            var thoughtProcess = "First, I analyzed the request. Then, I formulated a response.";
            var callerDetails = new CallerDetails(userDetails: new UserDetails(userId: "caller-inf-123", userName: "Caller Inf Name", userEmail: "callerinf@example.com", userClientIP: System.Net.IPAddress.Parse("192.168.1.100")));

            // Act
            var data = ExecuteInferenceDataBuilder.Build(
                details,
                agent,
                conversationId,
                inputMessages: input,
                outputMessages: output,
                startTime: start,
                endTime: end,
                spanId: spanId,
                parentSpanId: parentSpanId,
                thoughtProcess: thoughtProcess,
                callerDetails: callerDetails);

            // Assert
            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiUsageInputTokensKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiUsageOutputTokensKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiResponseFinishReasonsKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiAgentThoughtProcessKey).WhoseValue.Should().Be(thoughtProcess);
            attrs.Should().ContainKey(OpenTelemetryConstants.UserIdKey).WhoseValue.Should().Be("caller-inf-123");
            attrs.Should().ContainKey(OpenTelemetryConstants.UserNameKey).WhoseValue.Should().Be("Caller Inf Name");
            attrs.Should().ContainKey(OpenTelemetryConstants.UserEmailKey).WhoseValue.Should().Be("callerinf@example.com");
            attrs.Should().ContainKey(OpenTelemetryConstants.CallerClientIpKey).WhoseValue.Should().Be("192.168.1.100");
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
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-9");
            var start = DateTimeOffset.UtcNow;
            var conversationId = "conv-zero";

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId, startTime: start);

            // Assert
            data.StartTime.Should().Be(start);
            data.EndTime.Should().BeNull();
            data.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Build_AddsExtraAttributes_WhenNotReserved()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "model-extra", "provider-extra");
            var agent = new AgentDetails("agent-extra", "ExtraInfAgent");
            var conversationId = "conv-extra-inf";
            var extras = new Dictionary<string, object?>
            {
                {"inf.attr1", "v1"},
                {"inf.attr2", 99}
            };

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes.Should().ContainKey("inf.attr1").WhoseValue.Should().Be("v1");
            data.Attributes.Should().ContainKey("inf.attr2").WhoseValue.Should().Be(99);
        }

        [TestMethod]
        public void Build_DoesNotOverrideReservedKeys_WithExtraAttributes()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "model-resv", "provider-resv");
            var agent = new AgentDetails("agent-resv", "ReservedInfAgent");
            var conversationId = "conv-resv-inf";
            var extras = new Dictionary<string, object?>
            {
                {OpenTelemetryConstants.GenAiRequestModelKey, "fake-model"},
                {OpenTelemetryConstants.GenAiProviderNameKey, "fake-provider"},
                {"inf.other", "ok"}
            };

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes[OpenTelemetryConstants.GenAiRequestModelKey].Should().Be("model-resv");
            data.Attributes[OpenTelemetryConstants.GenAiProviderNameKey].Should().Be("provider-resv");
            data.Attributes.Should().ContainKey("inf.other").WhoseValue.Should().Be("ok");
        }

        [TestMethod]
        public void Build_IgnoresNullValues_InExtraAttributes()
        {
            // Arrange
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "model-null", "provider-null");
            var agent = new AgentDetails("agent-null", "NullInfAgent");
            var conversationId = "conv-null-inf";
            var extras = new Dictionary<string, object?>
            {
                {"inf.null", null},
                {"inf.valid", "yes"}
            };

            // Act
            var data = ExecuteInferenceDataBuilder.Build(details, agent, conversationId, extraAttributes: extras);

            // Assert
            data.Attributes.Should().NotContainKey("inf.null");
            data.Attributes.Should().ContainKey("inf.valid").WhoseValue.Should().Be("yes");
        }
    }
}
