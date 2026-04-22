// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Extension.Tests
{
    [TestClass]
    public class SemanticKernelSpanProcessorHelperTests
    {
        [TestMethod]
        public void ProcessInvocationInputOutputTag_MapsMessagesToStructuredInputFormat()
        {
            var activity = new Activity("test");
            var messages = new List<string>
        {
            JsonSerializer.Serialize(new MessageContent { Role = "system", Content = "System message" }),
            JsonSerializer.Serialize(new MessageContent { Role = "user", Content = "Message:User message" })
        };
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, JsonSerializer.Serialize(messages));

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity);

            var invocationTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value;
            Assert.IsNull(invocationTag, "Invocation input key should be removed");

            var result = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("\"version\":\"0.1.0\""), "Should contain version");
            Assert.IsTrue(result.Contains("\"role\":\"system\""), "System messages should be preserved");
            Assert.IsTrue(result.Contains("\"role\":\"user\""), "Should contain user role");
            Assert.IsTrue(result.Contains("System message"), "System message content should be preserved");
            Assert.IsTrue(result.Contains("User message"), "User message content should be preserved");
            Assert.IsTrue(result.Contains("\"type\":\"text\""), "Should contain text part type");
        }

        [TestMethod]
        public void ProcessInvocationInputOutputTag_HandlesJsonArrayOfObjects()
        {
            var activity = new Activity("test");

            // JSON array of objects directly (not wrapped in strings)
            var jsonArrayOfObjects = @"[{""role"":""system"",""name"":""Agent365Agent"",""content"":""You are a friendly assistant."",""tool_calls"":[]},{""role"":""user"",""name"":null,""content"":""hi"",""tool_calls"":[]}]";

            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, jsonArrayOfObjects);

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity);

            var invocationTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value;
            Assert.IsNull(invocationTag, "Invocation input key should be removed");

            var result = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("\"version\":\"0.1.0\""), "Should contain version");
            Assert.IsTrue(result.Contains("\"role\":\"system\""), "System message should be preserved");
            Assert.IsTrue(result.Contains("\"role\":\"user\""), "Should contain user role");
            Assert.IsTrue(result.Contains("hi"), "User message should be preserved");
        }

        [TestMethod]
        public void ProcessInvocationInputOutputTag_SuppressInvocationInput_RemovesInputTagsAndPreservesOutput()
        {
            var activity = new Activity("test");
            var agentMessages = new List<string>
            {
                JsonSerializer.Serialize(new MessageContent { Role = "system", Content = "System message" }),
                JsonSerializer.Serialize(new MessageContent { Role = "user", Content = "Message:Sensitive user message" })
            };
            var inputMessages = JsonSerializer.Serialize(new[] { "Sensitive input message 1", "Sensitive input message 2" });
            var outputMessages = new List<string>
            {
                JsonSerializer.Serialize(new MessageContent { Role = "assistant", Content = "Output message" })
            };
            
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, JsonSerializer.Serialize(agentMessages));
            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationOutputKey, JsonSerializer.Serialize(outputMessages));

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity, suppressInvocationInput: true);

            var removedAgentInput = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value;
            var removedInputMessages = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value;
            var removedInvocationOutput = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationOutputKey).Value;
            var output = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiOutputMessagesKey).Value as string;
            
            Assert.IsNull(removedAgentInput, "Invocation input key should be removed");
            Assert.IsNull(removedInputMessages, "Input messages key should be removed");
            Assert.IsNull(removedInvocationOutput, "Invocation output key should be removed");
            Assert.IsNotNull(output, "Output should be on gen_ai.output.messages");
            Assert.IsTrue(output.Contains("\"version\":\"0.1.0\""), "Output should be in structured format");
            Assert.IsTrue(output.Contains("\"role\":\"assistant\""), "Should contain assistant role");
            Assert.IsTrue(output.Contains("Output message"), "Output content should be preserved");
        }

        [TestMethod]
        public void ProcessInvocationInputOutputTag_SuppressInvocationInput_HandlesMissingTags()
        {
            var activity = new Activity("test");
            // GenAiInputMessagesKey and GenAiAgentInvocationInputKey intentionally not set

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity, suppressInvocationInput: true);

            var agentInput = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value;
            var inputMessages = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value;

            Assert.IsNull(agentInput);
            Assert.IsNull(inputMessages);
        }

        [TestMethod]
        public void ProcessInvocationInputOutputTag_SuppressInvocationInput_RemovesEmptyStringInputTags()
        {
            var activity = new Activity("test");
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, "");
            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, "");

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity, suppressInvocationInput: true);

            var agentInput = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value;
            var inputMessages = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value;

            Assert.IsNull(agentInput);
            Assert.IsNull(inputMessages);
        }

        [TestMethod]
        public void TryDeserializeMessageContent_HandlesUnquotedPropertyValues()
        {
            var unquotedJson = "{\u0022role\u0022: \u0022Assistant\u0022, \u0022content\u0022: \u0022\\u003Cp\\u003EHello Jian Han,\\u003C/p\\u003E\\n\\u003Cp\\u003EHow may I assist you today?\\u003C/p\\u003E\u0022, \u0022name\u0022: ShippingAgent1efe6ed@a365preview005.onmicrosoft.com}";
            var result = typeof(SemanticKernelSpanProcessorHelper)
                .GetMethod("TryDeserializeMessageContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { unquotedJson }) as MessageContent;

            Assert.IsNotNull(result);
            Assert.AreEqual("Assistant", result.Role);
            Assert.AreEqual("<p>Hello Jian Han,</p>\n<p>How may I assist you today?</p>", result.Content);
        }

        [TestMethod]
        public void ProcessInvocationInputOutputTag_ExtractsNestedContentFromAssistantMessages()
        {
            var activity = new Activity("test");

            // JSON array with an assistant message containing nested content structure
            var nestedContentJson = @"{""contentType"": ""'Text'"", ""content"": ""Hello! Before I can assist you, you must accept the terms and conditions.""}";
            var jsonArrayOfObjects = $@"[{{""role"":""Assistant"",""name"":""Agent365Agent"",""content"":{JsonSerializer.Serialize(nestedContentJson)}}}]";

            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationOutputKey, jsonArrayOfObjects);

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity);

            var invocationTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationOutputKey).Value;
            Assert.IsNull(invocationTag, "Invocation output key should be removed");

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiOutputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("Hello! Before I can assist you, you must accept the terms and conditions."), "Nested content should be extracted");
            Assert.IsFalse(filtered.Contains("contentType"), "contentType property should be removed");
        }

        [TestMethod]
        public void ProcessInvocationInputOutputTag_PreservesNonNestedContent()
        {
            var activity = new Activity("test");

            // JSON array with a user message that has simple string content (not nested)
            var jsonArrayOfObjects = @"[{""role"":""user"",""name"":null,""content"":""Message:Simple text message""}]";

            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, jsonArrayOfObjects);

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity);

            var invocationTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value;
            Assert.IsNull(invocationTag, "Invocation input key should be removed");

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("Simple text message"), "Simple content should be preserved after Message: trimming");
        }

        [TestMethod]
        public void ProcessInvocationInputOutputTag_HandlesNestedContentWithUserRole()
        {
            var activity = new Activity("test");

            // JSON array with a user message containing nested content structure AND Message: prefix
            var nestedContentJson = @"{""contentType"": ""'Text'"", ""content"": ""Message:User query with nested structure""}";
            var jsonArrayOfObjects = $@"[{{""role"":""user"",""name"":null,""content"":{JsonSerializer.Serialize(nestedContentJson)}}}]";

            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, jsonArrayOfObjects);

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity);

            var invocationTag = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value;
            Assert.IsNull(invocationTag, "Invocation input key should be removed");

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("User query with nested structure"), "Nested content should be extracted and Message: prefix trimmed");
            Assert.IsFalse(filtered.Contains("contentType"), "contentType property should be removed");
            Assert.IsFalse(filtered.Contains("Message:"), "Message: prefix should be trimmed");
        }
    }
}
