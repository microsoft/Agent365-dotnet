// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;

using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;
using Microsoft.Agents.A365.Observability.Runtime.Tracing;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Provides helper methods for processing and filtering Semantic Kernel span tags and events.
/// </summary>
public static class SemanticKernelSpanProcessorHelper
{
    private static readonly Regex UnquotedPropertyValueRegex =
        new Regex(
            @"(""[a-zA-Z0-9_]+"":\s*)([^""\s][^,}\s]*)",
            RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Processes and filters the gen_ai.agent.invocation_input and gen_ai.agent.invocation_output tags to remove system role messages.
    /// </summary>
    /// <param name="activity">The activity containing the tags to process.</param>
    /// <param name="suppressInvocationInput">Whether to suppress the invocation input messages.</param>
    public static void ProcessInvocationInputOutputTag(Activity activity, bool suppressInvocationInput = false)
    {
        if (suppressInvocationInput)
        {
            RemoveTagIfExists(activity, OpenTelemetryConstants.GenAiInputMessagesKey);
            RemoveTagIfExists(activity, OpenTelemetryConstants.GenAiAgentInvocationInputKey);
        }
        else
        {
            MapToStructuredFormat(activity, OpenTelemetryConstants.GenAiAgentInvocationInputKey, OpenTelemetryConstants.GenAiInputMessagesKey, isOutput: false);
            MapToStructuredFormat(activity, OpenTelemetryConstants.GenAiInputMessagesKey, OpenTelemetryConstants.GenAiInputMessagesKey, isOutput: false);
        }

        MapToStructuredFormat(activity, OpenTelemetryConstants.GenAiAgentInvocationOutputKey, OpenTelemetryConstants.GenAiOutputMessagesKey, isOutput: true);
        MapToStructuredFormat(activity, OpenTelemetryConstants.GenAiOutputMessagesKey, OpenTelemetryConstants.GenAiOutputMessagesKey, isOutput: true);
    }

    /// <summary>
    /// Gets the value of a tag from the activity by key.
    /// </summary>
    /// <param name="activity">The activity containing the tag.</param>
    /// <param name="key">The key of the tag to retrieve.</param>
    /// <returns>The tag value as a string, or null if not found.</returns>
    private static string? GetTagValue(Activity activity, string key)
    {
        return activity.TagObjects
            .OfType<KeyValuePair<string, object>>()
            .FirstOrDefault(k => k.Key == key).Value as string;
    }

    /// <summary>
    /// Removes a tag from the activity if it exists.
    /// </summary>
    /// <param name="activity">The activity containing the tag to remove.</param>
    /// <param name="key">The key of the tag to remove.</param>
    private static void RemoveTagIfExists(Activity activity, string key)
    {
        if (GetTagValue(activity, key) != null)
        {
            activity.SetTag(key, null);
        }
    }

    /// <summary>
    /// Quotes unquoted property values in the JSON string.
    /// </summary>
    /// <param name="json">The JSON string to process.</param>
    /// <returns>The JSON string with quoted property values.</returns>
    private static string QuoteUnquotedPropertyValues(string json)
    {
        var quoted = UnquotedPropertyValueRegex.Replace(json, "$1\"$2\"");

        if (quoted.Length > 2 &&
            quoted.StartsWith("\"", StringComparison.Ordinal) &&
            quoted.EndsWith("\"", StringComparison.Ordinal))
        {
            try
            {
                var unescaped = JsonSerializer.Deserialize<string>(quoted);
                if (!string.IsNullOrEmpty(unescaped))
                {
                    quoted = UnquotedPropertyValueRegex.Replace(unescaped, "$1\"$2\"");
                }
            }
            catch (JsonException)
            {
                // If not a valid double-encoded string, continue with quoted
            }
        }

        return quoted;
    }

    /// <summary>
        /// Maps invocation message tags to the A365 structured message format.
        /// Reads from <paramref name="sourceTagName"/>, writes structured result to <paramref name="targetTagName"/>,
        /// and removes the source tag when they differ.
        /// </summary>
        private static void MapToStructuredFormat(Activity activity, string sourceTagName, string targetTagName, bool isOutput)
        {
            var jsonString = GetTagValue(activity, sourceTagName);
            if (string.IsNullOrEmpty(jsonString))
                return;

            try
            {
                List<MessageContent>? messageArray = null;

                try
                {
                    messageArray = JsonSerializer.Deserialize<List<MessageContent>>(jsonString, JsonOptions);
                }
                catch (JsonException)
                {
                    var strList = JsonSerializer.Deserialize<List<string>>(jsonString, JsonOptions);
                    if (strList != null)
                    {
                        messageArray = new List<MessageContent>();
                        foreach (var s in strList)
                        {
                            if (s == null) continue;
                            var mc = TryDeserializeMessageContent(s);
                            messageArray.Add(mc ?? new MessageContent
                            {
                                Role = isOutput ? "assistant" : "user",
                                Content = s
                            });
                        }
                    }
                }

                if (messageArray == null || messageArray.Count == 0)
                    return;

                string? serialized = null;

                if (isOutput)
                {
                    var outputMessages = new List<OutputMessage>();
                    foreach (var msg in messageArray)
                    {
                        FilterMessageContent(msg);
                        if (string.IsNullOrEmpty(msg.Content)) continue;
                        var role = SemanticKernelMessageMapper.MapRole(msg.Role, MessageRole.Assistant);
                        outputMessages.Add(new OutputMessage(role, new IMessagePart[] { new TextPart(msg.Content) }));
                    }

                    if (outputMessages.Count > 0)
                    {
                        serialized = MessageUtils.Serialize(new OutputMessages(outputMessages));
                    }
                }
                else
                {
                    var chatMessages = new List<ChatMessage>();
                    foreach (var msg in messageArray)
                    {
                        FilterMessageContent(msg);
                        if (string.IsNullOrEmpty(msg.Content)) continue;
                        var role = SemanticKernelMessageMapper.MapRole(msg.Role, MessageRole.User);
                        chatMessages.Add(new ChatMessage(role, new IMessagePart[] { new TextPart(msg.Content) }, msg.Name));
                    }

                    if (chatMessages.Count > 0)
                    {
                        serialized = MessageUtils.Serialize(new InputMessages(chatMessages));
                    }
                }

                if (serialized != null)
                {
                    activity.SetTag(targetTagName, serialized);
                }

                if (sourceTagName != targetTagName)
                {
                    activity.SetTag(sourceTagName, null);
                }
            }
            catch (JsonException)
            {
                // Leave original tag value
            }
        }

    /// <summary>
    /// Attempts to deserialize a string into a MessageContent object.
    /// </summary>
    /// <param name="s">The string to deserialize.</param>
    /// <returns>The deserialized MessageContent object, or null if deserialization fails.</returns>
    private static MessageContent? TryDeserializeMessageContent(string s)
    {
        try
        {
            return JsonSerializer.Deserialize<MessageContent>(s, JsonOptions);
        }
        catch (JsonException)
        {
            var fixedString = QuoteUnquotedPropertyValues(s);
            try
            {
                return JsonSerializer.Deserialize<MessageContent>(fixedString, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Filters and extracts the message content from messages.
    /// For user messages, trims the prefix up to and including 'Message:'.
    /// For all messages, if Content is a JSON object with a nested 'content' property, extracts the inner content.
    /// </summary>
    /// <param name="message">The MessageContent to filter.</param>
    private static void FilterMessageContent(MessageContent? message)
    {
        if (message == null || string.IsNullOrEmpty(message.Content))
        {
            return;
        }

        // Try to extract nested content if Content is a JSON object with a 'content' property
        TryExtractNestedContent(message);

        // For user messages, trim the "Message:" prefix
        if (string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            var idx = message.Content.IndexOf("Message:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                message.Content = message.Content[(idx + "Message:".Length)..].Trim();
            }
        }
    }

    /// <summary>
    /// Attempts to extract nested content from a message's Content property.
    /// If Content is a JSON object with a 'content' property, replaces Content with the inner content value.
    /// </summary>
    /// <param name="message">The MessageContent to process.</param>
    private static void TryExtractNestedContent(MessageContent message)
    {
        if (string.IsNullOrEmpty(message.Content))
        {
            return;
        }

        var content = message.Content.Trim();
        if (!content.StartsWith("{", StringComparison.Ordinal) || !content.EndsWith("}", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var nestedContent = JsonSerializer.Deserialize<NestedContent>(content, JsonOptions);
            if (nestedContent?.Content != null)
            {
                message.Content = nestedContent.Content;
            }
        }
        catch (JsonException)
        {
            // Content is not a valid nested content JSON, keep the original value
        }
    }

}