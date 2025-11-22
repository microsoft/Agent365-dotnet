// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;

using System.Diagnostics;
using System.Text.Json;
using System.Linq;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

internal static class SemanticKernelSpanProcessorHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Processes and filters the gen_ai.agent.invocation_input tag to remove system role messages.
    /// </summary>
    /// <param name="activity">The activity containing the tag to process.</param>
    public static void ProcessInvocationInputTag(Activity activity)
    {
        var kvp = activity.TagObjects
            .OfType<KeyValuePair<string, object>>()
            .FirstOrDefault(k => k.Key == OpenTelemetryConstants.GenAiAgentInvocationInputKey);

        if (kvp is { } && !kvp.Equals(default(KeyValuePair<string, object>)) && kvp.Value is string jsonString)
        {
            TryFilterInvocationInput(activity, jsonString);
        }
    }

    /// <summary>
    /// Attempts to parse and filter the invocation input JSON string, removing system messages and encoding the result.
    /// </summary>
    /// <param name="activity">The activity to update with the filtered tag.</param>
    /// <param name="jsonString">The JSON string to parse and filter.</param>
    private static void TryFilterInvocationInput(Activity activity, string jsonString)
    {
        try
        {
            var inputArray = JsonSerializer.Deserialize<List<GenAiInvocationInput>>(jsonString, JsonOptions);
            if (inputArray != null)
            {
                var filtered = inputArray
                    .Where(e => e.Role != "system")
                    .Select(e =>
                    {
                        FilterUserMessageContent(e);
                        return e;
                    })
                    .ToList();

                var filteredString = JsonSerializer.Serialize(filtered, JsonOptions);
                var encoded = EncodeForJsonInHtml(filteredString);
                activity.SetTag(OpenTelemetryConstants.GenAiAgentInvocationInputKey, encoded);
            }
        }
        catch (JsonException)
        {
            //Swallow exception and leave the original tag value
        }
    }

    /// <summary>
    /// Filters an input element by removing system role messages and extracting user message content.
    /// </summary>
    /// <param name="invocationInput">The GenAiInvocationInput to filter.</param>
    private static void FilterUserMessageContent(GenAiInvocationInput? invocationInput)
    {
        if (invocationInput?.Role == "user" && !string.IsNullOrEmpty(invocationInput.Content))
        {
            var idx = invocationInput.Content.IndexOf("Message:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                invocationInput.Content = invocationInput.Content[(idx + "Message:".Length)..].Trim();
            }
        }
    }

    private static string EncodeForJsonInHtml(string input)
    {
        return input
            .Replace("&", "\\u0026")
            .Replace("<", "\\u003c")
            .Replace(">", "\\u003e")
            .Replace("\"", "\\u0022")
            .Replace("'", "\\u0027")
            .Replace("/", "\\u002f");
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
                                var userMsg = JsonSerializer.Deserialize<GenAiEventContent>(content, JsonOptions);
                                if (userMsg != null && userMsg.Role == "user" && !string.IsNullOrEmpty(userMsg.Content))
                                {
                                    FilterUserMessageContent(userMsg);
                                    result[OpenTelemetryConstants.GenAiUserMessageEventName].Add(JsonSerializer.Serialize(userMsg, JsonOptions));
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
                            result[OpenTelemetryConstants.GenAiChoiceEventName].Add(content);
                            break;
                        }
                    }
                }
            }
        }
        return result;
    }

    // Overload for GenAiEventContent filtering
    private static void FilterUserMessageContent(GenAiEventContent? obj)
    {
        if (obj?.Role == "user" && !string.IsNullOrEmpty(obj.Content))
        {
            var idx = obj.Content.IndexOf("Message:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                obj.Content = obj.Content[(idx + "Message:".Length)..].Trim();
            }
        }
    }
}