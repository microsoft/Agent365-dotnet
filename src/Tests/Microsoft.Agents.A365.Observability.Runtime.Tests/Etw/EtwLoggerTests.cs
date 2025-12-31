// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------

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
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name", agentPlatformId: "platform-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint: new Uri("https://example.com/agent"), details: agentDetails, sessionId: "session-1");
            string conversationId = "conv-123";

            // Act
            etwLogger.LogInvokeAgent(invokeAgentDetails, tenantDetails, conversationId);

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
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var inferenceDetails = new InferenceCallDetails(InferenceOperationType.Chat, "model-x", "provider-y");
            string conversationId = "conv-inf-1";

            // Act
            etwLogger.LogInferenceCall(inferenceDetails, agentDetails, tenantDetails, conversationId, inputMessages: new[] { "hello" }, outputMessages: new[] { "world" });

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
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var toolDetails = new ToolCallDetails("tool-a", arguments: @"{ ""arg"": 1 }", toolCallId: "tool-call-1", description: "desc", toolType: "function");
            string conversationId = "conv-tool-1";
            string responseContent = @"{ ""value"": ""result"" }";

            // Act
            etwLogger.LogToolCall(toolDetails, agentDetails, tenantDetails, conversationId, responseContent: responseContent);

            // Assert
            var evt = listener.Events.FirstOrDefault(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            var payloadStr = evt!.Payload![0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.ExecuteTool.ToString(), root.GetProperty("Name").GetString());
        }
    }
}
