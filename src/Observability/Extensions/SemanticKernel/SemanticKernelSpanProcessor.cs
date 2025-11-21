// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using OpenTelemetry;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal class SemanticKernelSpanProcessor : BaseProcessor<Activity>
{
    private static readonly string TargetSourceName = SemanticKernelTelemetryConstants.SemanticKernelSource;

    const string InvocationInputTagKey = "gen_ai.agent.invocation_input";
    const string GenAiEventContentTagKey = "gen_ai.event.content";
    const string GenAiUserMessageEventName = "gen_ai.user.message";
    const string GenAiChoiceEventName = "gen_ai.choice";
    const string GenAiInputMessagesTagKey = "gen_ai.input.messages";
    const string GenAiOutputMessagesTagKey = "gen_ai.output.messages";
    private static readonly Regex NameValueRegex =
        new Regex(
            @"(""name"":\s*)([^""\s][^,}\s]*)",
            RegexOptions.Compiled);
    private static readonly Regex UserMessageContentRegex =
        new Regex(
            @"Message:\s.*",
            RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override void OnStart(Activity activity)
    {
    }

    public override void OnEnd(Activity activity)
    {
        if (activity.Source.Name.StartsWith(TargetSourceName))
        {
            var tags = activity.Tags.ToDictionary(kv => kv.Key, kv => kv.Value);
            if (tags.TryGetValue(OpenTelemetryConstants.GenAiOperationNameKey, out var operationName))
            {
                switch (operationName)
                {
                    case SemanticKernelTelemetryConstants.InvokeAgentOperation:
                        ProcessInvocationInputTag(activity);
                        break;

                    case SemanticKernelTelemetryConstants.ExecuteToolOperation:
                        // Span emitted by SK SDK follows Microsoft Agents A365 schema, so no modification needed.
                        // FunctionInvocationFilter already adds other relevant tags.
                        // Placeholder for any plumbing if needed in the future.
                        break;

                    case SemanticKernelTelemetryConstants.ChatCompletionsOperation:
                        activity.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, InferenceOperationType.Chat.ToString());
                        activity.DisplayName = activity.DisplayName.ToString().Replace(SemanticKernelTelemetryConstants.ChatCompletionsOperation, InferenceOperationType.Chat.ToString());

                        var userAndChoiceMessages = GetGenAiUserAndChoiceMessageContent(activity);
                        if (userAndChoiceMessages[GenAiUserMessageEventName] is List<string> userMessages && userMessages.Count > 0)
                        {
                            activity.SetTag(GenAiInputMessagesTagKey, string.Join(", ", userMessages));
                        }
                        if (userAndChoiceMessages[GenAiChoiceEventName] is List<string> choiceMessages && choiceMessages.Count > 0)
                        {
                            activity.SetTag(GenAiOutputMessagesTagKey, string.Join(", ", choiceMessages));
                        }
                        // Other tags set by SK SDK follow Microsoft Agent A365 schema.
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Processes and filters the gen_ai.agent.invocation_input tag to remove system role messages.
    /// </summary>
    /// <param name="activity">The activity containing the tag to process.</param>
    private static void ProcessInvocationInputTag(Activity activity)
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
            var inputArray = JsonNode.Parse(jsonString) as JsonArray;
            if (inputArray != null)
            {
                var filtered = new JsonArray(
                    inputArray
                        .Select(e => FilterInvocationInputElement(e))
                        .Where(obj => obj != null)
                        .ToArray()
                );

                var filteredString = filtered.ToJsonString(JsonOptions);
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
    /// <param name="e">The JSON token to filter.</param>
    /// <returns>The filtered JsonObject, or null if the element should be excluded (e.g., system role).</returns>
    private static JsonObject? FilterInvocationInputElement(JsonNode? e)
    {
        JsonObject? obj = null;
        if (e is JsonObject jo)
        {
            obj = jo;
        }
        else if (e is JsonValue jv && jv.TryGetValue<string>(out var str))
        {
            obj = TryParseJsonObjectFromString(str);
        }
        else
        {
            return null;
        }

        if (obj?["role"]?.GetValue<string>() == "system")
            return null;

        FilterUserMessageContent(obj);

        return obj;
    }

    private static JsonObject? TryParseJsonObjectFromString(string jstring)
    {
        try
        {
            var node = JsonNode.Parse(jstring);
            return node as JsonObject;
        }
        catch (JsonException)
        {
            try
            {
                // Attempt to fix unquoted property names (not supported by System.Text.Json, but try to fix)
                var fixedString = NameValueRegex.Replace(
                    jstring,
                    "$1\"$2\""
                );
                var node = JsonNode.Parse(fixedString);
                return node as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    static void FilterUserMessageContent(JsonObject? obj)
    {
        if (obj?["role"]?.GetValue<string>() == "user" && obj["content"] is JsonValue contentVal)
        {
            var contentStr = contentVal.GetValue<string>();
            var match = UserMessageContentRegex.Match(contentStr);
            if (match.Success)
            {
                obj["content"] = match.Value;
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
    private static Dictionary<string, List<string>> GetGenAiUserAndChoiceMessageContent(Activity activity)
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
                            // Try to parse the content as JSON and filter as required
                            try
                            {
                                var node = JsonNode.Parse(content);
                                if (node is JsonObject jObj && jObj["role"]?.GetValue<string>() == "user" && jObj["content"] is JsonValue)
                                {
                                    FilterUserMessageContent(jObj);
                                    // Only keep the filtered object
                                    result[GenAiUserMessageEventName].Add(jObj.ToJsonString(JsonOptions));
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
}