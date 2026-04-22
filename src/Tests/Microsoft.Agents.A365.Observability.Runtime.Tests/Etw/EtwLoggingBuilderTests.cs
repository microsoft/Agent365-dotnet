// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Etw;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Tracing;
using System.Net;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Etw
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
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name", agentType: AgentType.MicrosoftCopilot, tenantId: Guid.NewGuid().ToString());
            var invokeAgentScopeDetails = new InvokeAgentScopeDetails(endpoint: new Uri("https://example.com/agent"));
            var callerDetails = new CallerDetails(userDetails: new UserDetails(userId: "caller-id-1", userName: "Caller Name", userEmail: "caller@example.com", userClientIP: IPAddress.Parse("192.168.1.100")));
            string conversationId = "conv-123";

            // Act
            logger.LogInvokeAgent(invokeAgentScopeDetails, agentDetails, conversationId, request: new Request(sessionId: "session-1"), callerDetails: callerDetails);

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
            Assert.AreEqual(conversationId, attrsElement.GetProperty(OpenTelemetryConstants.GenAiConversationIdKey).GetString());
            Assert.AreEqual("invoke_agent", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
            Assert.AreEqual(agentDetails.TenantId, attrsElement.GetProperty(OpenTelemetryConstants.TenantIdKey).GetString());
            Assert.AreEqual("caller-id-1", attrsElement.GetProperty(OpenTelemetryConstants.UserIdKey).GetString());
            Assert.AreEqual("Caller Name", attrsElement.GetProperty(OpenTelemetryConstants.UserNameKey).GetString());
            Assert.AreEqual("caller@example.com", attrsElement.GetProperty(OpenTelemetryConstants.UserEmailKey).GetString());
            Assert.AreEqual("192.168.1.100", attrsElement.GetProperty(OpenTelemetryConstants.CallerClientIpKey).GetString());
        }

        [TestMethod]
        public void Build_AddsEtwLogProcessor_AndWritesExpectedAttributes_FromInferenceCall()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var logger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name", tenantId: Guid.NewGuid().ToString());
            var inferenceDetails = new InferenceCallDetails(InferenceOperationType.Chat, "model-x", "provider-y");
            string conversationId = "conv-inf-1";
            var source = new Channel(name: "ChannelInf", link: "https://channel/inf");
            var callerDetails = new CallerDetails(userDetails: new UserDetails(userId: "inf-caller-id", userName: "Inference Caller", userEmail: "infcaller@example.com", userClientIP: IPAddress.Parse("10.0.0.50")));

            // Act
            logger.LogInferenceCall(inferenceDetails, agentDetails, conversationId, inputMessages: new[] { "hello" }, outputMessages: new[] { "world" }, channel: source, callerDetails: callerDetails);

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
            Assert.IsTrue(attrsElement.GetProperty(OpenTelemetryConstants.GenAiInputMessagesKey).GetString()!.Contains("hello"));
            Assert.IsTrue(attrsElement.GetProperty(OpenTelemetryConstants.GenAiOutputMessagesKey).GetString()!.Contains("world"));
            Assert.AreEqual("ChannelInf", attrsElement.GetProperty(OpenTelemetryConstants.ChannelNameKey).GetString());
            Assert.AreEqual("https://channel/inf", attrsElement.GetProperty(OpenTelemetryConstants.ChannelLinkKey).GetString());
            var tenantIdString = attrsElement.GetProperty(OpenTelemetryConstants.TenantIdKey).GetString();
            Assert.AreEqual(agentDetails.TenantId, tenantIdString);
            Assert.AreEqual("inf-caller-id", attrsElement.GetProperty(OpenTelemetryConstants.UserIdKey).GetString());
            Assert.AreEqual("Inference Caller", attrsElement.GetProperty(OpenTelemetryConstants.UserNameKey).GetString());
            Assert.AreEqual("infcaller@example.com", attrsElement.GetProperty(OpenTelemetryConstants.UserEmailKey).GetString());
            Assert.AreEqual("10.0.0.50", attrsElement.GetProperty(OpenTelemetryConstants.CallerClientIpKey).GetString());
        }

        [TestMethod]
        public void Build_AddsEtwLogProcessor_AndWritesExpectedAttributes_FromToolCall()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var logger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name", tenantId: Guid.NewGuid().ToString());
            var toolDetails = new ToolCallDetails("tool-a", arguments: @"{ ""arg"": 1 }", toolCallId: "tool-call-1", description: "desc", toolType: "function");
            string conversationId = "conv-tool-1";
            string responseContent = @"{ ""value"": ""result"" }";
            var source = new Channel(name: "ChannelInf", link: "https://channel/inf");
            var callerDetails = new CallerDetails(userDetails: new UserDetails(userId: "tool-caller-id", userName: "Tool Caller", userEmail: "toolcaller@example.com"));

            // Act
            logger.LogToolCall(toolDetails, agentDetails, conversationId, responseContent: responseContent, channel: source, callerDetails: callerDetails);

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
            Assert.AreEqual(responseContent, attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolCallResultKey).GetString());
            Assert.AreEqual("execute_tool", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
            Assert.AreEqual("ChannelInf", attrsElement.GetProperty(OpenTelemetryConstants.ChannelNameKey).GetString());
            Assert.AreEqual("https://channel/inf", attrsElement.GetProperty(OpenTelemetryConstants.ChannelLinkKey).GetString());
            var tenantIdString = attrsElement.GetProperty(OpenTelemetryConstants.TenantIdKey).GetString();
            Assert.AreEqual(agentDetails.TenantId, tenantIdString);
            Assert.AreEqual("tool-caller-id", attrsElement.GetProperty(OpenTelemetryConstants.UserIdKey).GetString());
            Assert.AreEqual("Tool Caller", attrsElement.GetProperty(OpenTelemetryConstants.UserNameKey).GetString());
            Assert.AreEqual("toolcaller@example.com", attrsElement.GetProperty(OpenTelemetryConstants.UserEmailKey).GetString());
        }

        [TestMethod]
        public void Build_AddsEtwLogProcessor_AndWritesExpectedAttributes_FromOutputMessages()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            var logger = provider.GetRequiredService<IA365EtwLogger<EtwLoggingBuilderTests>>();
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name", agentType: AgentType.MicrosoftCopilot, tenantId: Guid.NewGuid().ToString());
            var response = new Response(new[] { "Hello", "World" });

            // Act
            logger.LogOutput(agentDetails, response);

            // Assert
            var evt = listener.Events.Find(e => e.EventId == 2000);
            Assert.IsNotNull(evt);
            Assert.IsNotNull(evt.Payload);
            var payloadStr = evt.Payload[0] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr).RootElement;
            Assert.IsTrue(root.TryGetProperty("Name", out var nameProp));
            Assert.AreEqual(OpenTelemetryConstants.OperationNames.OutputMessages.ToString(), nameProp.GetString());
            Assert.IsTrue(root.TryGetProperty("SpanId", out var spanIdProp));
            Assert.IsFalse(string.IsNullOrWhiteSpace(spanIdProp.GetString()));
            Assert.IsTrue(root.TryGetProperty("Attributes", out var attrsElement));
            Assert.AreEqual(JsonValueKind.Object, attrsElement.ValueKind, "Attributes JSON element should be an object.");
            Assert.AreEqual("agent-id", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentIdKey).GetString());
            Assert.AreEqual("agent-name", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentNameKey).GetString());
            Assert.AreEqual("output_messages", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
            var outputValue = attrsElement.GetProperty(OpenTelemetryConstants.GenAiOutputMessagesKey).GetString()!;
            Assert.IsTrue(outputValue.Contains("Hello") && outputValue.Contains("World") && outputValue.Contains("\"version\":\"0.1.0\""));
            var tenantIdString = attrsElement.GetProperty(OpenTelemetryConstants.TenantIdKey).GetString();
            Assert.AreEqual(agentDetails.TenantId, tenantIdString);
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
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var invokeAgentScopeDetails = new InvokeAgentScopeDetails(endpoint: new Uri("https://example.com/agent"));
            string conversationId = "conv-123";

            // Act
            etwLogger.LogInvokeAgent(invokeAgentScopeDetails, agentDetails, conversationId, request: new Request(sessionId: "session-1"));
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
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var invokeAgentScopeDetails = new InvokeAgentScopeDetails(endpoint: new Uri("https://example.com/agent"));
            var inferenceDetails = new InferenceCallDetails(InferenceOperationType.Chat, "model-x", "provider-y");
            var toolDetails = new ToolCallDetails("tool-a", arguments: @"{ ""arg"": 1 }", toolCallId: "tool-call-1", description: "desc", toolType: "function");
            var response = new Response(new[] { "output" });
            string conversationId = "conv-123";

            // Act
            etwLogger.LogInvokeAgent(invokeAgentScopeDetails, agentDetails, conversationId, request: new Request(sessionId: "session-1"));
            etwLogger.LogInferenceCall(inferenceDetails, agentDetails, conversationId, inputMessages: new[] { "hello" }, outputMessages: new[] { "world" });
            etwLogger.LogToolCall(toolDetails, agentDetails, conversationId, responseContent: @"{ ""value"": ""result"" }");
            etwLogger.LogOutput(agentDetails, response);

            // Assert
            var payloads = listener.Events.Where(e => e.EventId == 2000).Select(e => e.Payload?[0] as string).Where(p => p != null).ToList();
            Assert.IsTrue(payloads.Any(p => p!.Contains("InvokeAgent")), "Expected at least one InvokeAgent log to be exported");
            Assert.IsTrue(payloads.Any(p => p!.Contains("ExecuteInference")), "Expected at least one ExecuteInference log to be exported");
            Assert.IsTrue(payloads.Any(p => p!.Contains("ExecuteTool")), "Expected at least one ExecuteTool log to be exported");
            Assert.IsTrue(payloads.Any(p => p!.Contains("OutputMessages")), "Expected at least one OutputMessages log to be exported");
        }
    }
}
