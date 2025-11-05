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
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-1", "AgentOne");
            var tenant = new TenantDetails(Guid.NewGuid());
            var data = ExecuteInferenceDataBuilder.Build(details, agent, tenant);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOperationNameKey);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiRequestModelKey).WhoseValue.Should().Be("gpt-4o");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiProviderNameKey).WhoseValue.Should().Be("openai");
        }

        [TestMethod]
        public void Build_WithTokensAndFinishReasons_IncludesUsageAndReasons()
        {
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai", 10, 20, new[]{"stop"}, "resp-1");
            var agent = new AgentDetails("agent-2");
            var tenant = new TenantDetails(Guid.NewGuid());
            var data = ExecuteInferenceDataBuilder.Build(details, agent, tenant);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiUsageInputTokensKey).WhoseValue.Should().Be(10);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiUsageOutputTokensKey).WhoseValue.Should().Be(20);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiResponseFinishReasonsKey).WhoseValue.Should().Be("stop");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiResponseIdKey).WhoseValue.Should().Be("resp-1");
        }

        [TestMethod]
        public void Build_WithMessages_IncludesInputAndOutput()
        {
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-3");
            var tenant = new TenantDetails(Guid.NewGuid());
            var input = new[]{"Hello"};
            var output = new[]{"Hi"};
            var data = ExecuteInferenceDataBuilder.Build(details, agent, tenant, inputMessages: input, outputMessages: output);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey).WhoseValue.Should().Be("Hello");
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey).WhoseValue.Should().Be("Hi");
        }

        [TestMethod]
        public void Build_WithConversationId_IncludesConversationId()
        {
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-4");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-456";
            var data = ExecuteInferenceDataBuilder.Build(details, agent, tenant, conversationId: conversationId);
            data.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey).WhoseValue.Should().Be("conv-456");
        }

        [TestMethod]
        public void Build_WithNullOptionalParameters_OmitsThoseAttributes()
        {
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-5");
            var tenant = new TenantDetails(Guid.NewGuid());
            var data = ExecuteInferenceDataBuilder.Build(details, agent, tenant);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiUsageInputTokensKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiUsageOutputTokensKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiResponseFinishReasonsKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiResponseIdKey);
            data.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
        }

        [TestMethod]
        public void Build_SetsTimingInformation_WhenProvided()
        {
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-6");
            var tenant = new TenantDetails(Guid.NewGuid());
            var start = DateTimeOffset.UtcNow.AddMinutes(-2);
            var end = DateTimeOffset.UtcNow;
            var data = ExecuteInferenceDataBuilder.Build(details, agent, tenant, startTime: start, endTime: end);
            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void Build_SetsSpanIds_WhenProvided()
        {
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-7");
            var tenant = new TenantDetails(Guid.NewGuid());
            var spanId = "span-inf";
            var parentSpanId = "parent-inf";
            var data = ExecuteInferenceDataBuilder.Build(details, agent, tenant, spanId: spanId, parentSpanId: parentSpanId);
            data.SpanId.Should().Be(spanId);
            data.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithAllParameters_SetsAllExpectedAttributes()
        {
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai", 33, 44, new[]{"length","stop"}, "resp-all");
            var agent = new AgentDetails("agent-8", "AgentEight", "Desc", agentAUID: "auid8", agentUPN: "upn8@example.com", agentBlueprintId: "bp-8");
            var tenant = new TenantDetails(Guid.NewGuid());
            var conversationId = "conv-all";
            var input = new[]{"Hello"};
            var output = new[]{"World"};
            var start = DateTimeOffset.UtcNow.AddSeconds(-10);
            var end = DateTimeOffset.UtcNow;
            var spanId = "span-all-inf";
            var parentSpanId = "parent-all-inf";
            var data = ExecuteInferenceDataBuilder.Build(
                details,
                agent,
                tenant,
                conversationId: conversationId,
                inputMessages: input,
                outputMessages: output,
                startTime: start,
                endTime: end,
                spanId: spanId,
                parentSpanId: parentSpanId);
            var attrs = data.Attributes;
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiUsageInputTokensKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiUsageOutputTokensKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiResponseFinishReasonsKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiResponseIdKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            attrs.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            data.StartTime.Should().Be(start);
            data.EndTime.Should().Be(end);
            data.Duration.Should().BeCloseTo(end - start, TimeSpan.FromMilliseconds(100));
            data.SpanId.Should().Be(spanId);
            data.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithOnlyStartTime_DurationZero()
        {
            var details = new InferenceCallDetails(InferenceOperationType.Chat, "gpt-4o", "openai");
            var agent = new AgentDetails("agent-9");
            var tenant = new TenantDetails(Guid.NewGuid());
            var start = DateTimeOffset.UtcNow;
            var data = ExecuteInferenceDataBuilder.Build(details, agent, tenant, startTime: start);
            data.StartTime.Should().Be(start);
            data.EndTime.Should().BeNull();
            data.Duration.Should().Be(TimeSpan.Zero);
        }
    }
}
