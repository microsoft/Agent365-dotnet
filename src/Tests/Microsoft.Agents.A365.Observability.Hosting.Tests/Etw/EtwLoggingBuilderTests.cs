// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Contracts;
using Microsoft.Agents.A365.Observability.Contracts.Details;
using Microsoft.Agents.A365.Observability.Hosting.Etw;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Tracing;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Etw
{
    [TestClass]
    public class EtwLoggingBuilderTests
    {
        private class TestEventListener : EventListener
        {
            public List<EventWrittenEventArgs> Events { get; } = new List<EventWrittenEventArgs>();

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                Events.Add(eventData);
            }
        }

        private ServiceProvider BuildProvider() => new ServiceCollection().AddLoggingWithEtw().BuildServiceProvider();

        [TestMethod]
        public void Build_AddsEtwLogProcessor_AndWritesExpectedAttributes_FromInvokeAgent()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var logger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var invokeAgentDetails = new InvokeAgentDetails(new Uri("https://example.com/agent"), agentDetails, sessionId: "session-1");
            string conversationId = "conv-123";

            // Act
            logger.LogInvokeAgent(invokeAgentDetails, tenantDetails, conversationId);

            // Assert
            var evt = listener.Events.Find(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            Assert.IsNotNull(evt.Payload);
            var payloadStr = evt.Payload[0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr).RootElement;
            Assert.IsTrue(root.TryGetProperty("Name", out var nameProp));
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.InvokeAgent.ToString(), nameProp.GetString());
            Assert.IsTrue(root.TryGetProperty("SpanId", out var spanIdProp));
            Assert.IsFalse(string.IsNullOrWhiteSpace(spanIdProp.GetString()));
            Assert.IsTrue(root.TryGetProperty("Attributes", out var attrsElement));
            Assert.AreEqual(JsonValueKind.Object, attrsElement.ValueKind, "Attributes JSON element should be an object.");
            Assert.AreEqual("agent-id", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentIdKey).GetString());
            Assert.AreEqual("agent-name", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentNameKey).GetString());
            Assert.AreEqual("example.com", attrsElement.GetProperty(OpenTelemetryConstants.ServerAddressKey).GetString());
            Assert.AreEqual("session-1", attrsElement.GetProperty(OpenTelemetryConstants.SessionIdKey).GetString());
            Assert.AreEqual(conversationId, attrsElement.GetProperty(OpenTelemetryConstants.GenAiConversationIdKey).GetString());
            Assert.AreEqual("invoke_agent", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
            var tenantIdString = attrsElement.GetProperty(OpenTelemetryConstants.TenantIdKey).GetString();
            Assert.IsTrue(Guid.TryParse(tenantIdString, out var parsedTenant));
            Assert.AreEqual(tenantDetails.TenantId, parsedTenant);
        }

        [TestMethod]
        public void Build_AddsEtwLogProcessor_AndWritesExpectedAttributes_FromInferenceCall()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var logger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var inferenceDetails = new InferenceCallDetails(InferenceOperationType.Chat, "model-x", "provider-y");
            string conversationId = "conv-inf-1";

            // Act
            logger.LogInferenceCall(inferenceDetails, agentDetails, tenantDetails, conversationId, inputMessages: new[] { "hello" }, outputMessages: new[] { "world" });

            // Assert
            var evt = listener.Events.Find(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            Assert.IsNotNull(evt.Payload);
            var payloadStr = evt.Payload[0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr).RootElement;
            Assert.IsTrue(root.TryGetProperty("Name", out var nameProp));
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.ExecuteInference.ToString(), nameProp.GetString());
            Assert.IsTrue(root.TryGetProperty("SpanId", out var spanIdProp));
            Assert.IsFalse(string.IsNullOrWhiteSpace(spanIdProp.GetString()));
            Assert.IsTrue(root.TryGetProperty("Attributes", out var attrsElement));
            Assert.AreEqual(JsonValueKind.Object, attrsElement.ValueKind, "Attributes JSON element should be an object.");
            Assert.AreEqual("agent-id", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentIdKey).GetString());
            Assert.AreEqual("agent-name", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentNameKey).GetString());
            Assert.AreEqual(conversationId, attrsElement.GetProperty(OpenTelemetryConstants.GenAiConversationIdKey).GetString());
            Assert.AreEqual("chat", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
            Assert.AreEqual("model-x", attrsElement.GetProperty(OpenTelemetryConstants.GenAiRequestModelKey).GetString());
            Assert.AreEqual("provider-y", attrsElement.GetProperty(OpenTelemetryConstants.GenAiProviderNameKey).GetString());
            Assert.AreEqual("hello", attrsElement.GetProperty(OpenTelemetryConstants.GenAiInputMessagesKey).GetString());
            Assert.AreEqual("world", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOutputMessagesKey).GetString());
            var tenantIdString = attrsElement.GetProperty(OpenTelemetryConstants.TenantIdKey).GetString();
            Assert.IsTrue(Guid.TryParse(tenantIdString, out var parsedTenant));
            Assert.AreEqual(tenantDetails.TenantId, parsedTenant);
        }

        [TestMethod]
        public void Build_AddsEtwLogProcessor_AndWritesExpectedAttributes_FromToolCall()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var logger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var toolDetails = new ToolCallDetails("tool-a", arguments: @"{ ""arg"": 1 }", toolCallId: "tool-call-1", description: "desc", toolType: "function");
            string conversationId = "conv-tool-1";
            string responseContent = @"{ ""value"": ""result"" }";

            // Act
            logger.LogToolCall(toolDetails, agentDetails, tenantDetails, conversationId, responseContent: responseContent);

            // Assert
            var evt = listener.Events.Find(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            Assert.IsNotNull(evt.Payload);
            var payloadStr = evt.Payload[0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr).RootElement;
            Assert.IsTrue(root.TryGetProperty("Name", out var nameProp));
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.ExecuteTool.ToString(), nameProp.GetString());
            Assert.IsTrue(root.TryGetProperty("SpanId", out var spanIdProp));
            Assert.IsFalse(string.IsNullOrWhiteSpace(spanIdProp.GetString()));
            Assert.IsTrue(root.TryGetProperty("Attributes", out var attrsElement));
            Assert.AreEqual(JsonValueKind.Object, attrsElement.ValueKind, "Attributes JSON element should be an object.");
            Assert.AreEqual("agent-id", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentIdKey).GetString());
            Assert.AreEqual("agent-name", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentNameKey).GetString());
            Assert.AreEqual(conversationId, attrsElement.GetProperty(OpenTelemetryConstants.GenAiConversationIdKey).GetString());
            Assert.AreEqual("tool-a", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolNameKey).GetString());
            Assert.AreEqual(@"{ ""arg"": 1 }", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolArgumentsKey).GetString());
            Assert.AreEqual("tool-call-1", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolCallIdKey).GetString());
            Assert.AreEqual("desc", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolDescriptionKey).GetString());
            Assert.AreEqual("function", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolTypeKey).GetString());
            Assert.AreEqual(responseContent, attrsElement.GetProperty(OpenTelemetryConstants.GenAiEventContent).GetString());
            Assert.AreEqual("execute_tool", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
            var tenantIdString = attrsElement.GetProperty(OpenTelemetryConstants.TenantIdKey).GetString();
            Assert.IsTrue(Guid.TryParse(tenantIdString, out var parsedTenant));
            Assert.AreEqual(tenantDetails.TenantId, parsedTenant);
        }

        [TestMethod]
        public void LoggerFilter_Allows_EtwCategoryPrefix_Only()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var etwLogger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var factory = provider.GetRequiredService<ILoggerFactory>();
            var blockedLogger = factory.CreateLogger("Custom.Blocked");
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var invokeAgentDetails = new InvokeAgentDetails(new Uri("https://example.com/agent"), agentDetails, sessionId: "session-1");
            string conversationId = "conv-123";

            // Act
            etwLogger.LogInvokeAgent(invokeAgentDetails, tenantDetails, conversationId);
            blockedLogger.LogInformation("Blocked log");

            // Assert
            var payloads = listener.Events.Where(e => e.EventId == 2000).Select(e => e.Payload?[0] as string).Where(p => p != null).ToList();
            Assert.IsTrue(payloads.Any(p => p!.Contains("InvokeAgent")), "Expected at least one InvokeAgent log to be exported");
            Assert.IsFalse(payloads.Any(p => p!.Contains("Blocked")), "Blocked log without prefix should not be exported");
        }

        [TestMethod]
        public void LoggerFilter_Allows_All_Our_CustomLogs()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var etwLogger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var invokeAgentDetails = new InvokeAgentDetails(new Uri("https://example.com/agent"), agentDetails, sessionId: "session-1");
            var inferenceDetails = new InferenceCallDetails(InferenceOperationType.Chat, "model-x", "provider-y");
            var toolDetails = new ToolCallDetails("tool-a", arguments: @"{ ""arg"": 1 }", toolCallId: "tool-call-1", description: "desc", toolType: "function");
            string conversationId = "conv-123";

            // Act
            etwLogger.LogInvokeAgent(invokeAgentDetails, tenantDetails, conversationId);
            etwLogger.LogInferenceCall(inferenceDetails, agentDetails, tenantDetails, conversationId, inputMessages: new[] { "hello" }, outputMessages: new[] { "world" });
            etwLogger.LogToolCall(toolDetails, agentDetails, tenantDetails, conversationId, @"{ ""value"": ""result"" }");

            // Assert
            var payloads = listener.Events.Where(e => e.EventId == 2000).Select(e => e.Payload?[0] as string).Where(p => p != null).ToList();
            Assert.IsTrue(payloads.Any(p => p!.Contains("InvokeAgent")), "Expected at least one InvokeAgent log to be exported");
            Assert.IsTrue(payloads.Any(p => p!.Contains("ExecuteInference")), "Expected at least one ExecuteInference log to be exported");
            Assert.IsTrue(payloads.Any(p => p!.Contains("ExecuteTool")), "Expected at least one ExecuteTool log to be exported");
        }
    }
}
