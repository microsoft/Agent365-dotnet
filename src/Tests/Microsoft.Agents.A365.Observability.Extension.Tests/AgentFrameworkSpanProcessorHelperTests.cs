// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.A365.Observability.Extensions.AgentFramework.Utils;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Extension.Tests
{
    [TestClass]
    public class AgentFrameworkSpanProcessorHelperTests
    {
        [TestMethod]
        public void ProcessInputOutputMessages_FiltersOutSystemMessages()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""system"", ""parts"": [{""type"": ""text"", ""content"": ""You are a helpful assistant.""}]},
                {""role"": ""user"", ""parts"": [{""type"": ""text"", ""content"": ""Hello""}]},
                {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""Hi there!""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsFalse(filtered.Contains("You are a helpful assistant"), "System message should be filtered out");
            Assert.IsTrue(filtered.Contains("Hello"), "User message should be preserved");
            Assert.IsTrue(filtered.Contains("Hi there!"), "Assistant message should be preserved");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_ExtractsTextContentFromParts()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user"", ""parts"": [{""type"": ""text"", ""content"": ""What is the weather?""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("What is the weather?"), "Text content should be extracted from parts");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_HandlesMultipleTextParts()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user"", ""parts"": [
                    {""type"": ""text"", ""content"": ""First part""},
                    {""type"": ""text"", ""content"": ""Second part""}
                ]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("First part"), "First text part should be included");
            Assert.IsTrue(filtered.Contains("Second part"), "Second text part should be included");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_FiltersNonTextParts()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user"", ""parts"": [
                    {""type"": ""image"", ""content"": ""base64imagedata""},
                    {""type"": ""text"", ""content"": ""Describe this image""}
                ]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsFalse(filtered.Contains("base64imagedata"), "Non-text parts should be filtered out");
            Assert.IsTrue(filtered.Contains("Describe this image"), "Text parts should be preserved");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_HandlesOutputMessagesWithFinishReason()
        {
            var activity = new Activity("test");
            var outputMessages = @"[
                {
                    ""role"": ""assistant"",
                    ""parts"": [{""type"": ""text"", ""content"": ""Here is your answer.""}],
                    ""finish_reason"": ""stop""
                }
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiOutputMessagesKey, outputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiOutputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("Here is your answer"), "Output message content should be preserved");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_HandlesEmptyPartsArray()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user"", ""parts"": []},
                {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""Valid message""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("Valid message"), "Valid message should be preserved");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_HandlesNullPartsArray()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user""},
                {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""Valid message""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("Valid message"), "Valid message should be preserved");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_PreservesOriginalOnInvalidJson()
        {
            var activity = new Activity("test");
            var invalidJson = "not a valid json";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, invalidJson);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var result = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.AreEqual(invalidJson, result, "Original value should be preserved on invalid JSON");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_HandlesMissingTags()
        {
            var activity = new Activity("test");
            // No tags set

            // Should not throw
            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var inputResult = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value;
            var outputResult = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiOutputMessagesKey).Value;

            Assert.IsNull(inputResult);
            Assert.IsNull(outputResult);
        }

        [TestMethod]
        public void ProcessInputOutputMessages_HandlesEmptyMessagesArray()
        {
            var activity = new Activity("test");
            var emptyArray = "[]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, emptyArray);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var result = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.AreEqual(emptyArray, result, "Empty array should remain unchanged");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_IsCaseInsensitiveForRoles()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""USER"", ""parts"": [{""type"": ""text"", ""content"": ""User message""}]},
                {""role"": ""Assistant"", ""parts"": [{""type"": ""text"", ""content"": ""Assistant message""}]},
                {""role"": ""SYSTEM"", ""parts"": [{""type"": ""text"", ""content"": ""System message""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("User message"), "User message should be preserved (case-insensitive)");
            Assert.IsTrue(filtered.Contains("Assistant message"), "Assistant message should be preserved (case-insensitive)");
            Assert.IsFalse(filtered.Contains("System message"), "System message should be filtered out (case-insensitive)");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_IsCaseInsensitiveForPartType()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user"", ""parts"": [{""type"": ""TEXT"", ""content"": ""Uppercase type""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("Uppercase type"), "Text content should be extracted regardless of case");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_FiltersToolRole()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user"", ""parts"": [{""type"": ""text"", ""content"": ""User message""}]},
                {""role"": ""tool"", ""parts"": [{""type"": ""text"", ""content"": ""Tool response""}]},
                {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""Assistant message""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);
            Assert.IsTrue(filtered.Contains("User message"), "User message should be preserved");
            Assert.IsTrue(filtered.Contains("Assistant message"), "Assistant message should be preserved");
            Assert.IsFalse(filtered.Contains("Tool response"), "Tool messages should be filtered out");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_ProcessesBothInputAndOutputTags()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user"", ""parts"": [{""type"": ""text"", ""content"": ""User input""}]}
            ]";
            var outputMessages = @"[
                {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""Assistant output""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);
            activity.SetTag(OpenTelemetryConstants.GenAiOutputMessagesKey, outputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filteredInput = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            var filteredOutput = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiOutputMessagesKey).Value as string;

            Assert.IsNotNull(filteredInput);
            Assert.IsNotNull(filteredOutput);
            Assert.IsTrue(filteredInput.Contains("User input"), "Input messages should be processed");
            Assert.IsTrue(filteredOutput.Contains("Assistant output"), "Output messages should be processed");
        }

        [TestMethod]
        public void ProcessInputOutputMessages_HandlesRealWorldExample()
        {
            var activity = new Activity("test");
            // Real-world example from the user's sample data
            var inputMessages = @"[
                {
                    ""role"": ""user"",
                    ""parts"": [
                        {
                            ""type"": ""text"",
                            ""content"": ""hi""
                        }
                    ]
                },
                {
                    ""role"": ""assistant"",
                    ""parts"": [
                        {
                            ""type"": ""text"",
                            ""content"": ""Hello! How can I assist you today?""
                        }
                    ]
                },
                {
                    ""role"": ""user"",
                    ""parts"": [
                        {
                            ""type"": ""text"",
                            ""content"": ""what can you do""
                        }
                    ]
                }
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);

            // Verify the output is a JSON array of strings
            var result = JsonSerializer.Deserialize<List<string>>(filtered);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("hi", result[0]);
            Assert.AreEqual("Hello! How can I assist you today?", result[1]);
            Assert.AreEqual("what can you do", result[2]);
        }

        [TestMethod]
        public void ProcessInputOutputMessages_SkipsEmptyContentParts()
        {
            var activity = new Activity("test");
            var inputMessages = @"[
                {""role"": ""user"", ""parts"": [{""type"": ""text"", ""content"": """"}]},
                {""role"": ""assistant"", ""parts"": [{""type"": ""text"", ""content"": ""Valid content""}]}
            ]";

            activity.SetTag(OpenTelemetryConstants.GenAiInputMessagesKey, inputMessages);

            AgentFrameworkSpanProcessorHelper.ProcessInputOutputMessages(activity);

            var filtered = activity.Tags.FirstOrDefault(t => t.Key == OpenTelemetryConstants.GenAiInputMessagesKey).Value as string;
            Assert.IsNotNull(filtered);

            var result = JsonSerializer.Deserialize<List<string>>(filtered);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count, "Empty content messages should be skipped");
            Assert.AreEqual("Valid content", result[0]);
        }
    }
}
