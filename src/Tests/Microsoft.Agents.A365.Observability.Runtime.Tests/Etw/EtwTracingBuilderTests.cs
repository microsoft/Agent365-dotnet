// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Etw;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Etw
{
    [TestClass]
    public class EtwTracingBuilderTests
    {
        private class TestEventListener : EventListener
        {
            public List<EventWrittenEventArgs> Events { get; } = new List<EventWrittenEventArgs>();

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                Events.Add(eventData);
            }
        }

        private ServiceProvider BuildProvider() => new ServiceCollection().AddTracingWithEtw().BuildServiceProvider();

        [TestMethod]
        public void Build_AddsEtwScopeEventProcessor_AndWritesExpectedAttributes_ForInvokeAgentSpan()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            using var tracerProvider = provider.GetRequiredService<OpenTelemetry.Trace.TracerProvider>();
            using var source = new ActivitySource(OpenTelemetryConstants.SourceName);

            var activityName = OpenTelemetryConstants.OperationNames.InvokeAgent.ToString();
            using var activity = source.StartActivity(activityName, ActivityKind.Server);
            Assert.IsNotNull(activity);

            activity?.SetTag(OpenTelemetryConstants.GenAiAgentIdKey, "agent-id");
            activity?.SetTag(OpenTelemetryConstants.GenAiAgentNameKey, "agent-name");
            activity?.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, "invoke_agent");
            activity?.SetTag(OpenTelemetryConstants.GenAiConversationIdKey, "conv-123");
            activity?.SetTag(OpenTelemetryConstants.ServerAddressKey, "example.com");

            // Act
            activity?.Stop();

            // Assert
            var evt = listener.Events.Find(e => e.EventId == 1000);
            Assert.IsNotNull(evt);
            Assert.IsNotNull(evt.Payload);
            Assert.AreEqual(activityName, evt.Payload![0] as string);

            var payloadStr = evt.Payload![4] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.IsTrue(root.TryGetProperty("resourceSpan", out var resourceSpan));
            var span = resourceSpan.GetProperty("scopeSpan").GetProperty("span");
            Assert.AreEqual(activityName, span.GetProperty("name").GetString());
            Assert.IsTrue(span.TryGetProperty("attributes", out var attrsElement));
            Assert.AreEqual("agent-id", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentIdKey).GetString());
            Assert.AreEqual("agent-name", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentNameKey).GetString());
            Assert.AreEqual("invoke_agent", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
            Assert.AreEqual("conv-123", attrsElement.GetProperty(OpenTelemetryConstants.GenAiConversationIdKey).GetString());
            Assert.AreEqual("example.com", attrsElement.GetProperty(OpenTelemetryConstants.ServerAddressKey).GetString());
        }

        [TestMethod]
        public void Build_AddsEtwScopeEventProcessor_AndWritesExpectedAttributes_ForInferenceSpan()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            using var tracerProvider = provider.GetRequiredService<OpenTelemetry.Trace.TracerProvider>();
            using var source = new ActivitySource(OpenTelemetryConstants.SourceName);

            var activityName = OpenTelemetryConstants.OperationNames.ExecuteInference.ToString();
            using var activity = source.StartActivity(activityName, ActivityKind.Server);
            Assert.IsNotNull(activity);

            activity?.SetTag(OpenTelemetryConstants.GenAiAgentIdKey, "agent-id");
            activity?.SetTag(OpenTelemetryConstants.GenAiAgentNameKey, "agent-name");
            activity?.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, "chat");
            activity?.SetTag(OpenTelemetryConstants.GenAiConversationIdKey, "conv-inf-1");
            activity?.SetTag(OpenTelemetryConstants.GenAiRequestModelKey, "model-x");
            activity?.SetTag(OpenTelemetryConstants.GenAiProviderNameKey, "provider-y");
            activity?.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "hello");
            activity?.SetTag(OpenTelemetryConstants.GenAiOutputMessagesKey, "world");

            // Act
            activity?.Stop();

            // Assert
            var evt = listener.Events.Find(e => e.EventId == 1000);
            Assert.IsNotNull(evt);
            Assert.IsNotNull(evt.Payload);
            Assert.AreEqual(activityName, evt.Payload![0] as string);

            var payloadStr = evt.Payload![4] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.IsTrue(root.TryGetProperty("resourceSpan", out var resourceSpan));
            var span = resourceSpan.GetProperty("scopeSpan").GetProperty("span");
            Assert.AreEqual(activityName, span.GetProperty("name").GetString());
            var attrsElement = span.GetProperty("attributes");
            Assert.AreEqual("agent-id", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentIdKey).GetString());
            Assert.AreEqual("agent-name", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentNameKey).GetString());
            Assert.AreEqual("conv-inf-1", attrsElement.GetProperty(OpenTelemetryConstants.GenAiConversationIdKey).GetString());
            Assert.AreEqual("chat", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
            Assert.AreEqual("model-x", attrsElement.GetProperty(OpenTelemetryConstants.GenAiRequestModelKey).GetString());
            Assert.AreEqual("provider-y", attrsElement.GetProperty(OpenTelemetryConstants.GenAiProviderNameKey).GetString());
            Assert.AreEqual("hello", attrsElement.GetProperty(OpenTelemetryConstants.GenAiInputMessagesKey).GetString());
            Assert.AreEqual("world", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOutputMessagesKey).GetString());
        }

        [TestMethod]
        public void Build_AddsEtwScopeEventProcessor_AndWritesExpectedAttributes_ForToolCallSpan()
        {
            // Arrange
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);
            using var provider = BuildProvider();
            using var tracerProvider = provider.GetRequiredService<OpenTelemetry.Trace.TracerProvider>();
            using var source = new ActivitySource(OpenTelemetryConstants.SourceName);

            var activityName = OpenTelemetryConstants.OperationNames.ExecuteTool.ToString();
            using var activity = source.StartActivity(activityName, ActivityKind.Server);
            Assert.IsNotNull(activity);

            activity?.SetTag(OpenTelemetryConstants.GenAiAgentIdKey, "agent-id");
            activity?.SetTag(OpenTelemetryConstants.GenAiAgentNameKey, "agent-name");
            activity?.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, "execute_tool");
            activity?.SetTag(OpenTelemetryConstants.GenAiConversationIdKey, "conv-tool-1");
            activity?.SetTag(OpenTelemetryConstants.GenAiToolNameKey, "tool-a");
            activity?.SetTag(OpenTelemetryConstants.GenAiToolArgumentsKey, "{ \"arg\": 1 }");
            activity?.SetTag(OpenTelemetryConstants.GenAiToolCallIdKey, "tool-call-1");
            activity?.SetTag(OpenTelemetryConstants.GenAiToolDescriptionKey, "desc");
            activity?.SetTag(OpenTelemetryConstants.GenAiToolTypeKey, "function");
            activity?.SetTag(OpenTelemetryConstants.GenAiToolCallResultKey, "{ \"value\": \"result\" }");

            // Act
            activity?.Stop();

            // Assert
            var evt = listener.Events.Find(e => e.EventId == 1000);
            Assert.IsNotNull(evt);
            Assert.IsNotNull(evt.Payload);
            Assert.AreEqual(activityName, evt.Payload![0] as string);

            var payloadStr = evt.Payload![4] as string;
            Assert.IsNotNull(payloadStr);
            var root = JsonDocument.Parse(payloadStr!).RootElement;
            Assert.IsTrue(root.TryGetProperty("resourceSpan", out var resourceSpan));
            var span = resourceSpan.GetProperty("scopeSpan").GetProperty("span");
            Assert.AreEqual(activityName, span.GetProperty("name").GetString());
            var attrsElement = span.GetProperty("attributes");
            Assert.AreEqual("agent-id", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentIdKey).GetString());
            Assert.AreEqual("agent-name", attrsElement.GetProperty(OpenTelemetryConstants.GenAiAgentNameKey).GetString());
            Assert.AreEqual("conv-tool-1", attrsElement.GetProperty(OpenTelemetryConstants.GenAiConversationIdKey).GetString());
            Assert.AreEqual("tool-a", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolNameKey).GetString());
            Assert.AreEqual("{ \"arg\": 1 }", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolArgumentsKey).GetString());
            Assert.AreEqual("tool-call-1", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolCallIdKey).GetString());
            Assert.AreEqual("desc", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolDescriptionKey).GetString());
            Assert.AreEqual("function", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolTypeKey).GetString());
            Assert.AreEqual("{ \"value\": \"result\" }", attrsElement.GetProperty(OpenTelemetryConstants.GenAiToolCallResultKey).GetString());
            Assert.AreEqual("execute_tool", attrsElement.GetProperty(OpenTelemetryConstants.GenAiOperationNameKey).GetString());
        }
    }
}
