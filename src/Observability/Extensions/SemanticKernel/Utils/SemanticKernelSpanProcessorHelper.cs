// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;

using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
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
        var inputJsonString = GetTagValue(activity, OpenTelemetryConstants.GenAiAgentInvocationInputKey);
        if (inputJsonString != null)
        {
            TryFilterInvocationMessage(activity, inputJsonString, OpenTelemetryConstants.GenAiAgentInvocationInputKey);
        }

        var outputJsonString = GetTagValue(activity, OpenTelemetryConstants.GenAiAgentInvocationOutputKey);
        if (outputJsonString != null)
        {
            TryFilterInvocationMessage(activity, outputJsonString, OpenTelemetryConstants.GenAiAgentInvocationOutputKey);
        }
    }

    private static string? GetTagValue(Activity activity, string key)
    {
        return activity.TagObjects
            .OfType<KeyValuePair<string, object>>()
            .FirstOrDefault(k => k.Key == key).Value as string;
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
                    .Select(TryDeserializeMessageContent)
                    .Where(mc => mc != null)
                    .ToList()!;
            }

            if (inputArray != null)
            {
                var filtered = inputArray
                    .Where(e => e.Role != "system")
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
                message.Content = message.Content[(idx + "Message:".Length)..].Trim();
            }
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
            }
            else if (activityEvent.Name == OpenTelemetryConstants.GenAiChoiceEventName)
            {
                FilterAiChoiceMessageContent(content, result[OpenTelemetryConstants.GenAiChoiceEventName]);
            }
        }
        return result;
    }

    private static string? GetEventContentTag(ActivityEvent activityEvent)
    {
        return activityEvent.Tags?
            .FirstOrDefault(tag => tag.Key == OpenTelemetryConstants.GenAiEventContent).Value as string;
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
            // If not JSON, fallback to original
            choiceMessages.Add(content);
        }
    }
}