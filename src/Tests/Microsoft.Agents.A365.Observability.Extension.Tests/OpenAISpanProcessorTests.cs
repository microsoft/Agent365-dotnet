// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Extensions.OpenAI;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Extension.Tests
{
    [TestClass]
    public class OpenAISpanProcessorTests
    {
        private const string InvokeAgentOperationName = "invoke_agent";
        private const string OpenAISourceName = "OpenAI.Test";

        [TestMethod]
        public void OpenAISpanProcessor_SendPromptInInvokeAgentScopes_DefaultsToTrue()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions();

            // Assert
            Assert.IsTrue(options.SendPromptInInvokeAgentScopes, "SendPromptInInvokeAgentScopes should default to true for backward compatibility");
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithDefaultOptions_PreservesPromptInInvokeAgentScope()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = true };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity(InvokeAgentOperationName)
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "Test prompt content");
            activity.Start();

            // Act
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            Assert.IsNotNull(promptTag.Value, "Prompt should be preserved when SendPromptInInvokeAgentScopes is true");
            Assert.AreEqual("Test prompt content", promptTag.Value);
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithSuppressOption_RemovesPromptFromInvokeAgentScope()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = false };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity(InvokeAgentOperationName)
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "Sensitive prompt content");
            activity.Start();

            // Act
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            Assert.IsNull(promptTag.Value, "Prompt should be removed when SendPromptInInvokeAgentScopes is false");
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithSuppressOption_RemovesPromptFromInvokeAgentScopeWithAgentName()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = false };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity("invoke_agent MyAgent")
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "Sensitive prompt content");
            activity.Start();

            // Act
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            Assert.IsNull(promptTag.Value, "Prompt should be removed from invoke_agent scope with agent name");
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithSuppressOption_PreservesOtherTags()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = false };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity(InvokeAgentOperationName)
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "Sensitive prompt")
                .SetTag(OpenTelemetryConstants.GenAiOutputMessagesKey, "Response content")
                .SetTag(OpenTelemetryConstants.GenAiAgentIdKey, "agent-123")
                .SetTag(OpenTelemetryConstants.GenAiConversationIdKey, "conv-456");
            activity.Start();

            // Act
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            var outputTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiOutputMessagesKey);
            var agentIdTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentIdKey);
            var conversationIdTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiConversationIdKey);

            Assert.IsNull(promptTag.Value, "Prompt should be removed");
            Assert.AreEqual("Response content", outputTag.Value, "Output messages should be preserved");
            Assert.AreEqual("agent-123", agentIdTag.Value, "Agent ID should be preserved");
            Assert.AreEqual("conv-456", conversationIdTag.Value, "Conversation ID should be preserved");
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithSuppressOption_DoesNotAffectNonInvokeAgentScopes()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = false };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity("execute_inference")
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "Inference prompt content");
            activity.Start();

            // Act
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            Assert.IsNotNull(promptTag.Value, "Prompt should be preserved for non-InvokeAgent scopes");
            Assert.AreEqual("Inference prompt content", promptTag.Value);
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithSuppressOption_DoesNotAffectExecuteToolScopes()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = false };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity("execute_tool")
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "Tool input content");
            activity.Start();

            // Act
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            Assert.IsNotNull(promptTag.Value, "Prompt should be preserved for execute_tool scopes");
            Assert.AreEqual("Tool input content", promptTag.Value);
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithNullOptions_UsesDefaultBehavior()
        {
            // Arrange
            var processor = new OpenAISpanProcessor(null!);
            
            using var activity = new Activity(InvokeAgentOperationName)
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "Test prompt");
            activity.Start();

            // Act
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            Assert.IsNotNull(promptTag.Value, "Prompt should be preserved with null options (defaults to true)");
            Assert.AreEqual("Test prompt", promptTag.Value);
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithSuppressOption_HandlesActivityWithoutPromptTag()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = false };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity(InvokeAgentOperationName)
                .SetTag(OpenTelemetryConstants.GenAiAgentIdKey, "agent-123");
            activity.Start();

            // Act - should not throw
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            Assert.IsNull(promptTag.Value, "Prompt tag should remain null when it was never set");
        }

        [TestMethod]
        public void OpenAISpanProcessor_WithSuppressOption_HandlesEmptyPromptTag()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = false };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity(InvokeAgentOperationName)
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, string.Empty);
            activity.Start();

            // Act
            processor.OnEnd(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            // After setting to null, the tag might still exist but with null value
            Assert.IsTrue(promptTag.Value == null || string.IsNullOrEmpty(promptTag.Value as string), 
                "Empty prompt should be removed or set to null");
        }

        [TestMethod]
        public void OpenAISpanProcessor_OnStart_DoesNotModifyActivity()
        {
            // Arrange
            var options = new OpenAISpanProcessorOptions { SendPromptInInvokeAgentScopes = false };
            var processor = new OpenAISpanProcessor(options);
            
            using var activity = new Activity(InvokeAgentOperationName)
                .SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "Test prompt");
            activity.Start();

            // Act
            processor.OnStart(activity);

            // Assert
            var promptTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey);
            Assert.IsNotNull(promptTag.Value, "OnStart should not modify the activity");
            Assert.AreEqual("Test prompt", promptTag.Value);
        }
    }
}
