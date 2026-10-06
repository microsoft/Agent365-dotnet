// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.Etw;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Tracing;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Etw
{
    [TestClass]
    public class EtwLoggerTests
    {
        private class TestEventListener : EventListener
        {
            public List<EventWrittenEventArgs> Events { get; } = new List<EventWrittenEventArgs>();
            protected override void OnEventWritten(EventWrittenEventArgs eventData) => Events.Add(eventData);
        }

        private ServiceProvider BuildProvider() => new ServiceCollection().AddLoggingWithEtw().BuildServiceProvider();

        [TestMethod]
        public void Logs_InvokeAgent_Event()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var etwLogger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name", agentPlatformId: "platform-123");
            var invokeAgentScopeDetails = new InvokeAgentScopeDetails(endpoint: new Uri("https://example.com/agent"));
            string conversationId = "conv-123";

            // Act
            etwLogger.LogInvokeAgent(invokeAgentScopeDetails, agentDetails, conversationId, request: new Request(sessionId: "session-1"));

            // Assert
            var evt = listener.Events.FirstOrDefault(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            var payloadStr = evt!.Payload![0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.InvokeAgent.ToString(), root.GetProperty("Name").GetString());
        }

        [TestMethod]
        public void Logs_InferenceCall_Event()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var etwLogger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var inferenceDetails = new InferenceCallDetails(InferenceOperationType.Chat, "model-x", "provider-y");
            string conversationId = "conv-inf-1";

            // Act
            etwLogger.LogInferenceCall(inferenceDetails, agentDetails, conversationId, inputMessages: new[] { "hello" }, outputMessages: new[] { "world" });

            // Assert
            var evt = listener.Events.FirstOrDefault(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            var payloadStr = evt!.Payload![0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.ExecuteInference.ToString(), root.GetProperty("Name").GetString());
        }

        [TestMethod]
        public void Logs_ToolCall_Event()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var etwLogger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var toolDetails = new ToolCallDetails("tool-a", arguments: @"{ ""arg"": 1 }", toolCallId: "tool-call-1", description: "desc", toolType: "function");
            string conversationId = "conv-tool-1";
            string responseContent = @"{ ""value"": ""result"" }";

            // Act
            etwLogger.LogToolCall(toolDetails, agentDetails, conversationId, responseContent: responseContent);

            // Assert
            var evt = listener.Events.FirstOrDefault(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            var payloadStr = evt!.Payload![0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.ExecuteTool.ToString(), root.GetProperty("Name").GetString());
        }

        [TestMethod]
        public void Logs_Output_Event()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var etwLogger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var response = new Response(new[] { "Hello", "World" });
            var conversationId = "conv-output-etw";
            var sourceMetadata = new Channel(name: "EtwChannel", link: "https://channel/etw");
            var callerDetails = new CallerDetails(userDetails: new UserDetails(userId: "caller-etw-123", userName: "Etw Caller", userEmail: "etw-caller@example.com"));

            // Act
            etwLogger.LogOutput(agentDetails, response, conversationId: conversationId, channel: sourceMetadata, callerDetails: callerDetails);

            // Assert
            var evt = listener.Events.FirstOrDefault(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            var payloadStr = evt!.Payload![0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.OutputMessages.ToString(), root.GetProperty("Name").GetString());
        }

        [TestMethod]
        public void Logs_ApplyGuardrail_Event_WithFindings()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var etwLogger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var guardrailDetails = new GuardrailDetails(
                targetType: GuardrailTargetType.LlmInput,
                decisionType: GuardrailDecisionType.Deny);
            var findings = new[]
            {
                new GuardrailFinding("prompt_injection", GuardrailRiskSeverity.High, policyDecisionType: "deny")
            };

            // Act
            etwLogger.LogApplyGuardrail(guardrailDetails, agentDetails, "conv-guardrail-1", "parent-1", findings: findings);

            // Assert
            var evt = listener.Events.FirstOrDefault(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            var payloadStr = evt!.Payload![0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.ApplyGuardrail.ToString(), root.GetProperty("Name").GetString());

            var events = root.GetProperty("Events");
            Assert.AreEqual(1, events.GetArrayLength());
            var finding = events[0];
            Assert.AreEqual(OpenTelemetryConstants.GenAiSecurityFindingEventName, finding.GetProperty("name").GetString());
            var attrs = finding.GetProperty("attributes");
            Assert.AreEqual("prompt_injection", attrs.GetProperty(OpenTelemetryConstants.GenAiSecurityRiskCategoryKey).GetString());
            Assert.AreEqual("high", attrs.GetProperty(OpenTelemetryConstants.GenAiSecurityRiskSeverityKey).GetString());
        }

        [TestMethod]
        public void Logs_ApplyGuardrail_Event_NoFindings_OmitsEvents()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var etwLogger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var guardrailDetails = new GuardrailDetails(
                targetType: GuardrailTargetType.LlmInput,
                decisionType: GuardrailDecisionType.Allow);

            // Act
            etwLogger.LogApplyGuardrail(guardrailDetails, agentDetails, "conv-guardrail-2", "parent-1");

            // Assert
            var evt = listener.Events.FirstOrDefault(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            var payloadStr = evt!.Payload![0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.IsFalse(root.TryGetProperty("Events", out _));
        }
    }
}
