// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;

internal static class SemanticKernelSpanProcessorHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string InvocationInputTagKey = "gen_ai.agent.invocation_input";
    private const string GenAiEventContentTagKey = "gen_ai.event.content";
    private const string GenAiUserMessageEventName = "gen_ai.user.message";
    private const string GenAiChoiceEventName = "gen_ai.choice";

    /// <summary>
    /// Processes and filters the gen_ai.agent.invocation_input tag to remove system role messages.
    /// </summary>
    /// <param name="activity">The activity containing the tag to process.</param>
    public static void ProcessInvocationInputTag(Activity activity)
    {
        var kvp = activity.TagObjects
            .OfType<KeyValuePair<string, object>>()
            .FirstOrDefault(k => k.Key == InvocationInputTagKey);

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
                activity.SetTag(InvocationInputTagKey, encoded);
            }
        }
        catch (JsonException)
        {
            //swallow exception and leave the original tag value
        }
    }

    /// <summary>
    /// Filters an invocation input element by removing system role messages and extracting user message content.
    /// </summary>
    /// <param name="invocationInput">The GenAiInvocationInput to filter.</param>
    /// <returns>The filtered GenAiInvocationInput, or null if the element should be excluded (e.g., system role).</returns>
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
            { GenAiUserMessageEventName, new List<string>() },
            { GenAiChoiceEventName, new List<string>() }
        };

        if (activity.Events == null)
            return result;

        foreach (var activityEvent in activity.Events)
        {
            if (activityEvent.Name == GenAiUserMessageEventName)
            {
                if (activityEvent.Tags != null)
                {
                    foreach (var tag in activityEvent.Tags)
                    {
                        if (tag.Key == GenAiEventContentTagKey && tag.Value is string content && !string.IsNullOrEmpty(content))
                        {
                            // Try to parse the content as GenAiEventContent and filter as required
                            try
                            {
                                var userMsg = JsonSerializer.Deserialize<GenAiEventContent>(content, JsonOptions);
                                if (userMsg != null && userMsg.Role == "user" && !string.IsNullOrEmpty(userMsg.Content))
                                {
                                    FilterUserMessageContent(userMsg);
                                    result[GenAiUserMessageEventName].Add(JsonSerializer.Serialize(userMsg, JsonOptions));
                                }
                            }
                            catch (JsonException)
                            {
                                // If not JSON, fallback to original
                                result[GenAiUserMessageEventName].Add(content);
                            }
                            break;
                        }
                    }
                }
            }
            else if (activityEvent.Name == GenAiChoiceEventName)
            {
                if (activityEvent.Tags != null)
                {
                    foreach (var tag in activityEvent.Tags ?? Enumerable.Empty<KeyValuePair<string, object?>>())
                    {
                        if (tag.Key == GenAiEventContentTagKey && tag.Value is string content && !string.IsNullOrEmpty(content))
                        {
                            result[GenAiChoiceEventName].Add(content);
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