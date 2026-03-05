// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;

using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;
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
            return;
        }

        TryFilterInvocationMessage(activity, OpenTelemetryConstants.GenAiAgentInvocationInputKey);
        TryFilterInvocationMessage(activity, OpenTelemetryConstants.GenAiInputMessagesKey);
        TryFilterInvocationMessage(activity, OpenTelemetryConstants.GenAiAgentInvocationOutputKey);
        TryFilterInvocationMessage(activity, OpenTelemetryConstants.GenAiOutputMessagesKey);
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

    private static void TryFilterInvocationMessage(Activity activity, string tagName)
    {
        var jsonString = GetTagValue(activity, tagName);
        if (jsonString != null)
        {
            TryFilterInvocationMessage(activity, jsonString, tagName);
        }
    }

    /// <summary>
    /// Attempts to parse and filter the invocation input JSON string, removing system messages and encoding the result.
    /// </summary>
    /// <param name="activity">The activity to update with the filtered tag.</param>
    /// <param name="jsonString">The JSON string to parse and filter.</param>
    /// <param name="tagName">The name of the tag to update.</param>
    private static void TryFilterInvocationMessage(Activity activity, string jsonString, string tagName)
    {
        try
        {
            List<MessageContent>? inputArray = null;

            // First, try to deserialize as a list of MessageContent objects directly
            try
            {
                inputArray = JsonSerializer.Deserialize<List<MessageContent>>(jsonString, JsonOptions);
            }
            catch (JsonException)
            {
                // If that fails, try to deserialize as a list of strings and then parse each string
                var strList = JsonSerializer.Deserialize<List<string>>(jsonString, JsonOptions);
                if (strList != null)
                {
                    inputArray = strList
                        .Select(TryDeserializeMessageContent)
                        .Where(mc => mc != null)
                        .ToList()!;
                }
            }

            if (inputArray != null)
            {
                var filtered = inputArray
                    .Where(e => !string.Equals(e.Role, "system", StringComparison.OrdinalIgnoreCase))
                    .Select(e =>
                    {
                        FilterMessageContent(e);
                        return e.Content;
                    })
                    .ToList();

                var filteredString = JsonSerializer.Serialize(filtered, JsonOptions);
                activity.SetTag(tagName, filteredString);
            }
        }
        catch (JsonException)
        {
            // Swallow exception and leave the original tag value
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

    /// <summary>
    /// Extracts user messages and choice messages from activity events.
    /// </summary>
    /// <param name="activity">The activity containing the events to process.</param>
    /// <returns>A dictionary containing lists of user messages and choice messages.</returns>
    public static Dictionary<string, List<string>> GetGenAiUserAndChoiceMessageContent(Activity activity)
    {
        var result = new Dictionary<string, List<string>>
        {
            { OpenTelemetryConstants.GenAiUserMessageEventName, new List<string>() },
            { OpenTelemetryConstants.GenAiChoiceEventName, new List<string>() }
        };

        if (activity.Events == null)
            return result;

        foreach (var activityEvent in activity.Events)
        {
            var content = GetEventContentTag(activityEvent);
            if (string.IsNullOrEmpty(content))
                continue;

            if (activityEvent.Name == OpenTelemetryConstants.GenAiUserMessageEventName)
            {
                try
                {
                    var userMsg = JsonSerializer.Deserialize<MessageContent>(content, JsonOptions);
                    if (userMsg != null && userMsg.Role == "user" && !string.IsNullOrEmpty(userMsg.Content))
                    {
                        FilterMessageContent(userMsg);
                        result[OpenTelemetryConstants.GenAiUserMessageEventName].Add(userMsg.Content);
                    }
                }
                catch (JsonException)
                {
                    result[OpenTelemetryConstants.GenAiUserMessageEventName].Add(content);
                }
            }
            else if (activityEvent.Name == OpenTelemetryConstants.GenAiChoiceEventName)
            {
                FilterAiChoiceMessageContent(content, result[OpenTelemetryConstants.GenAiChoiceEventName]);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets the content tag value from an activity event.
    /// </summary>
    /// <param name="activityEvent">The activity event to extract the tag from.</param>
    /// <returns>The content tag value as a string, or null if not found.</returns>
    private static string? GetEventContentTag(ActivityEvent activityEvent)
    {
        return activityEvent.Tags?
            .FirstOrDefault(tag => tag.Key == OpenTelemetryConstants.GenAiToolCallResultKey).Value as string;
    }

    /// <summary>
    /// Filters AI choice message content and adds it to the provided list.
    /// </summary>
    /// <param name="content">The content to filter.</param>
    /// <param name="choiceMessages">The list to add filtered messages to.</param>
    private static void FilterAiChoiceMessageContent(string content, List<string> choiceMessages)
    {
        try
        {
            var aiChoice = JsonSerializer.Deserialize<AiChoice>(content, JsonOptions);
            if (aiChoice?.Message != null &&
                aiChoice.Message.Role?.Equals("Assistant", StringComparison.OrdinalIgnoreCase) == true &&
                aiChoice.Message.ToolCalls != null)
            {
                foreach (var toolCall in aiChoice.Message.ToolCalls)
                {
                    if (toolCall.Function?.Arguments?.MessageBody != null)
                    {
                        var messageBody = toolCall.Function.Arguments.MessageBody;
                        if (!string.IsNullOrEmpty(messageBody))
                        {
                            choiceMessages.Add(messageBody);
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            choiceMessages.Add(content);
        }
    }
}