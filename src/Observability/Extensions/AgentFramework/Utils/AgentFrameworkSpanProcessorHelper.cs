// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.AgentFramework.Utils;

using Microsoft.Agents.A365.Observability.Extensions.AgentFramework.Models;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Provides helper methods for processing and filtering Agent Framework span tags.
/// </summary>
internal static class AgentFrameworkSpanProcessorHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Processes and filters the gen_ai.input.messages and gen_ai.output.messages tags to keep only user and assistant messages.
    /// </summary>
    /// <param name="activity">The activity containing the tags to process.</param>
    public static void ProcessInputOutputMessages(Activity activity)
    {
        TryFilterMessages(activity, OpenTelemetryConstants.GenAiInputMessagesKey);
        TryFilterMessages(activity, OpenTelemetryConstants.GenAiOutputMessagesKey);
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
    /// Attempts to filter the messages in the specified tag.
    /// </summary>
    /// <param name="activity">The activity containing the tag to filter.</param>
    /// <param name="tagName">The name of the tag to filter.</param>
    private static void TryFilterMessages(Activity activity, string tagName)
    {
        var jsonString = GetTagValue(activity, tagName);
        if (jsonString != null)
        {
            TryFilterMessages(activity, jsonString, tagName);
        }
    }

    /// <summary>
    /// Attempts to parse and filter the messages JSON string, keeping only user and assistant messages
    /// and extracting text content from the parts array.
    /// </summary>
    /// <param name="activity">The activity to update with the filtered tag.</param>
    /// <param name="jsonString">The JSON string to parse and filter.</param>
    /// <param name="tagName">The name of the tag to update.</param>
    private static void TryFilterMessages(Activity activity, string jsonString, string tagName)
    {
        try
        {
            var messages = JsonSerializer.Deserialize<List<AgentFrameworkMessageContent>>(jsonString, JsonOptions);
            if (messages == null || messages.Count == 0)
            {
                return;
            }

            var filtered = messages
                .Where(m => IsUserOrAssistantRole(m.Role))
                .Select(m => ExtractTextContent(m))
                .Where(content => !string.IsNullOrEmpty(content))
                .ToList();

            var filteredString = JsonSerializer.Serialize(filtered, JsonOptions);
            activity.SetTag(tagName, filteredString);
        }
        catch (JsonException)
        {
            // Swallow exception and leave the original tag value
        }
    }

    /// <summary>
    /// Checks if the role is "user" or "assistant".
    /// </summary>
    /// <param name="role">The role to check.</param>
    /// <returns>True if the role is "user" or "assistant"; otherwise, false.</returns>
    private static bool IsUserOrAssistantRole(string? role)
    {
        return string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the text content from a message's parts array.
    /// </summary>
    /// <param name="message">The message to extract content from.</param>
    /// <returns>The concatenated text content from all text parts, or null if no text parts exist.</returns>
    private static string? ExtractTextContent(AgentFrameworkMessageContent message)
    {
        if (message.Parts == null || message.Parts.Count == 0)
        {
            return null;
        }

        var textParts = message.Parts
            .Where(p => string.Equals(p.Type, "text", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(p.Content))
            .Select(p => p.Content)
            .ToList();

        return textParts.Count > 0 ? string.Join(" ", textParts) : null;
    }
}
