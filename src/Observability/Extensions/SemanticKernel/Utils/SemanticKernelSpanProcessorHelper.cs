// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;

using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class SemanticKernelSpanProcessorHelper
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
    /// Processes and filters the gen_ai.agent.invocation_input tag to remove system role messages.
    /// </summary>
    /// <param name="activity">The activity containing the tag to process.</param>
    public static void ProcessInvocationInputOutputTag(Activity activity)
    {
        var intputKvp = activity.TagObjects
            .OfType<KeyValuePair<string, object>>()
            .FirstOrDefault(k => k.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey);

        var outputKvp = activity.TagObjects
            .OfType<KeyValuePair<string, object>>()
            .FirstOrDefault(k => k.Key == OpenTelemetryConstants.GenAiAgentInvocationOutputKey);

        if (intputKvp.Value is string intputJsonString)
        {
            TryFilterInvocationMessage(activity, intputJsonString, OpenTelemetryConstants.GenAiAgentInvocationInputKey);
        }

        if (outputKvp.Value is string outputJsonString)
        {
            TryFilterInvocationMessage(activity, outputJsonString, OpenTelemetryConstants.GenAiAgentInvocationOutputKey);
        }
    }

    private static string QuoteUnquotedPropertyValues(string json)
    {
        // Quotes unquoted property values in the JSON string
        var quoted = UnquotedPropertyValueRegex.Replace(json, "$1\"$2\"");

        // Handle double-encoded JSON strings (e.g., "\"{...}\"")
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

            var strList = JsonSerializer.Deserialize<List<string>>(jsonString, JsonOptions);
            if (strList != null)
            {
                inputArray = strList
                    .Select<string, MessageContent?>(s =>
                    {
                        try
                        {
                            return JsonSerializer.Deserialize<MessageContent>(s, JsonOptions);
                        }
                        catch (JsonException)
                        {
                            // Try to fix unquoted property values
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
                    })
                    .Where(mc => mc != null)
                    .ToList()!;
            }

            if (inputArray != null)
            {
                var filtered = inputArray
                    .Where(e => !string.Equals(e.Role, "system", StringComparison.OrdinalIgnoreCase))
                    .Select(e =>
                    {
                        FilterUserMessageContent(e);
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
    /// Extracts the message content from messages by trimming the prefix up to and including 'Message:'.
    /// </summary>
    /// <param name="message">The MessageContent to filter.</param>
    private static void FilterUserMessageContent(MessageContent? message)
    {
        if (message?.Role == "user" && !string.IsNullOrEmpty(message.Content))
        {
            var idx = message.Content.IndexOf("Message:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                message.Content = CleanMessageContent(message.Content[(idx + "Message:".Length)..].Trim());
            }
        }
        else if (message?.Role == "Assistant" && !string.IsNullOrEmpty(message.Content))
        {
            message.Content = CleanMessageContent(message.Content);
        }
    }

    private static string CleanMessageContent(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Remove <p> and </p> tags
        var cleaned = input.Replace("<p>", string.Empty).Replace("</p>", string.Empty);

        // Remove \n and \r characters
        cleaned = cleaned.Replace("\n", string.Empty).Replace("\r", string.Empty);

        return cleaned;
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
            if (activityEvent.Name == OpenTelemetryConstants.GenAiUserMessageEventName)
            {
                if (activityEvent.Tags != null)
                {
                    foreach (var tag in activityEvent.Tags)
                    {
                        if (tag.Key == OpenTelemetryConstants.GenAiEventContent && tag.Value is string content && !string.IsNullOrEmpty(content))
                        {
                            // Try to parse the content as GenAiEventContent and filter as required
                            try
                            {
                                var userMsg = JsonSerializer.Deserialize<MessageContent>(content, JsonOptions);
                                if (userMsg != null && userMsg.Role == "user" && !string.IsNullOrEmpty(userMsg.Content))
                                {
                                    FilterUserMessageContent(userMsg);
                                    result[OpenTelemetryConstants.GenAiUserMessageEventName].Add(userMsg.Content);
                                }
                            }
                            catch (JsonException)
                            {
                                // If not JSON, fallback to original
                                result[OpenTelemetryConstants.GenAiUserMessageEventName].Add(content);
                            }
                            break;
                        }
                    }
                }
            }
            else if (activityEvent.Name == OpenTelemetryConstants.GenAiChoiceEventName)
            {
                if (activityEvent.Tags != null)
                {
                    foreach (var tag in activityEvent.Tags)
                    {
                        if (tag.Key == OpenTelemetryConstants.GenAiEventContent && tag.Value is string content && !string.IsNullOrEmpty(content))
                        {
                            FilterAiChoiceMessageContent(content, result[OpenTelemetryConstants.GenAiChoiceEventName]);
                            break;
                        }
                    }
                }
            }
        }
        return result;
    }

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
                        var messageBody = CleanMessageContent(toolCall.Function.Arguments.MessageBody);
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
            // If not JSON, fallback to original
            choiceMessages.Add(content);
        }
    }
}