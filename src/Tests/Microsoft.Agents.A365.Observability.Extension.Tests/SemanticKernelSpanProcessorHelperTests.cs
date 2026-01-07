// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Extension.Tests
{
    [TestClass]
    public class SemanticKernelSpanProcessorHelperTests
    {
        [TestMethod]
        public void ProcessInvocationInputOutputTag_RemovesSystemRoleMessages()
        {
            var activity = new Activity("test");
            var messages = new List<string>
        {
            JsonSerializer.Serialize(new MessageContent { Role = "system", Content = "System message" }),
            JsonSerializer.Serialize(new MessageContent { Role = "user", Content = "Message:User message" })
        };
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, JsonSerializer.Serialize(messages));

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsFalse(filtered.Contains("System message"));
            Assert.IsTrue(filtered.Contains("User message"));
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
            var output = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationOutputKey).Value as string;
            
            Assert.IsNull(removedAgentInput);
            Assert.IsNull(removedInputMessages);
            Assert.IsNotNull(output);
            Assert.IsTrue(output.Contains("Output message"));
        }

        [TestMethod]
        public void ProcessInvocationInputOutputTag_SuppressInvocationInput_HandlesEmptyAndMissingTags()
        {
            var activity = new Activity("test");
            activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, "");
            // GenAiInputMessagesKey intentionally not set

            SemanticKernelSpanProcessorHelper.ProcessInvocationInputOutputTag(activity, suppressInvocationInput: true);

            var agentInput = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey).Value as string;
            var inputMessages = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value;

            Assert.AreEqual("", agentInput);
            Assert.IsNull(inputMessages);
        }

        [TestMethod]
        public void GetGenAiUserAndChoiceMessageContent_ExtractsUserAndChoiceMessages()
        {
            var activity = new Activity("test");
            var userMsg = new MessageContent { Role = "user", Content = "Message:Hello user" };
            var choiceMsg = new AiChoice
            {
                Message = new AiChoiceMessage
                {
                    Role = "Assistant",
                    ToolCalls = new List<AiChoiceToolCall>
                {
                    new AiChoiceToolCall
                    {
                        Function = new AiChoiceFunction
                        {
                            Arguments = new AiChoiceArguments { MessageBody = "Choice message body" }
                        }
                    }
                }
                }
            };

            activity.AddEvent(new ActivityEvent(OpenTelemetryConstants.GenAiUserMessageEventName, tags: new ActivityTagsCollection
        {
            { OpenTelemetryConstants.GenAiEventContent, JsonSerializer.Serialize(userMsg) }
        }));
            activity.AddEvent(new ActivityEvent(OpenTelemetryConstants.GenAiChoiceEventName, tags: new ActivityTagsCollection
        {
            { OpenTelemetryConstants.GenAiEventContent, JsonSerializer.Serialize(choiceMsg) }
        }));

            var result = SemanticKernelSpanProcessorHelper.GetGenAiUserAndChoiceMessageContent(activity);

            Assert.AreEqual(1, result[OpenTelemetryConstants.GenAiUserMessageEventName].Count);
            Assert.AreEqual("Hello user", result[OpenTelemetryConstants.GenAiUserMessageEventName][0]);
            Assert.AreEqual(1, result[OpenTelemetryConstants.GenAiChoiceEventName].Count);
            Assert.AreEqual("Choice message body", result[OpenTelemetryConstants.GenAiChoiceEventName][0]);
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
        public void FilterAiChoiceMessageContent_FallbacksToOriginalOnInvalidJson()
        {
            var choiceMessages = new List<string>();
            var invalidJson = "not a json";
            typeof(SemanticKernelSpanProcessorHelper)
                .GetMethod("FilterAiChoiceMessageContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { invalidJson, choiceMessages });

            Assert.AreEqual(1, choiceMessages.Count);
            Assert.AreEqual("not a json", choiceMessages[0]);
        }
    }
}
