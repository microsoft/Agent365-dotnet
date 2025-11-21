// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------
namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using OpenTelemetry;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal class SemanticKernelSpanProcessor : BaseProcessor<Activity>
{
    private static readonly string TargetSourceName = SemanticKernelTelemetryConstants.SemanticKernelSource;

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
                        if (userAndChoiceMessages["gen_ai.user.message"] is List<string> userMessages && userMessages.Count > 0)
                        {
                            activity.SetTag("gen_ai.input.messages", string.Join(", ", userMessages));
                        }
                        if (userAndChoiceMessages["gen_ai.choice"] is List<string> choiceMessages && choiceMessages.Count > 0)
                        {
                            activity.SetTag("gen_ai.output.messages", string.Join(", ", choiceMessages));
                        }
                        // Other tags set by SK SDK follow Microsoft Agent A365 schema.
                        break;
                }
            }
        }
    }

    private static void ProcessInvocationInputTag(Activity activity)
    {
        if (activity is { TagObjects: not null })
        {
            foreach (var tagObj in activity.TagObjects)
            {
                if (tagObj is KeyValuePair<string, object> kvp &&
                    kvp.Key == "gen_ai.agent.invocation_input")
                {
                    if (kvp.Value is string jsonString)
                    {
                        TryFilterInvocationInput(activity, jsonString);
                    }
                    break;
                }
            }
        }
    }

    private static void TryFilterInvocationInput(Activity activity, string jsonString)
    {
        try
        {
            var inputArray = JsonConvert.DeserializeObject<JArray>(jsonString);
            if (inputArray != null)
            {
                var filtered = new JArray(
                    inputArray
                        .Select(e => FilterInvocationInputElement(e))
                        .Where(obj => obj != null)
                );

                var filteredString = filtered.ToString(Formatting.None);
                var encoded = EncodeForJsonInHtml(filteredString);
                activity.SetTag("gen_ai.agent.invocation_input", encoded);
            }
        }
        catch (JsonException)
        {
            // Handle invalid JSON if necessary
        }
    }

    private static JObject? FilterInvocationInputElement(JToken e)
    {
        JObject? obj = null;
        if (e.Type == JTokenType.Object)
        {
            obj = (JObject)e;
        }
        else if (e.Type == JTokenType.String)
        {
            obj = TryParseJObjectFromString(e.Value<string>() ?? "");
        }
        else
        {
            return null;
        }

        if (obj?["role"]?.ToString() == "system")
            return null;

        if (obj?["role"]?.ToString() == "user" && obj["content"] is JValue contentVal)
        {
            var contentStr = contentVal.ToString();
            var match = System.Text.RegularExpressions.Regex.Match(contentStr, @"Message:\s.*");
            if (match.Success)
            {
                obj["content"] = match.Value;
            }
        }

        return obj;
    }

    private static JObject? TryParseJObjectFromString(string jstring)
    {
        try
        {
            return JsonConvert.DeserializeObject<JObject>(jstring);
        }
        catch
        {
            try
            {
                var fixedString = System.Text.RegularExpressions.Regex.Replace(
                    jstring,
                    @"(""name"":\s*)([^""\s][^,}\s]*)",
                    "$1\"$2\""
                );
                return JsonConvert.DeserializeObject<JObject>(fixedString);
            }
            catch
            {
                return null;
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

    private static Dictionary<string, List<string>> GetGenAiUserAndChoiceMessageContent(Activity activity)
    {
        var result = new Dictionary<string, List<string>>
        {
            { "gen_ai.user.message", new List<string>() },
            { "gen_ai.choice", new List<string>() }
        };

        if (activity == null || activity.Events == null)
            return result;

        foreach (var activityEvent in activity.Events)
        {
            if (activityEvent.Name == "gen_ai.user.message")
            {
                if (activityEvent.Tags != null)
                {
                    foreach (var tag in activityEvent.Tags)
                    {
                        if (tag.Key == "gen_ai.event.content" && tag.Value is string content && !string.IsNullOrEmpty(content))
                        {
                            // Try to parse the content as JSON and filter as required
                            try
                            {
                                var jObj = JsonConvert.DeserializeObject<JObject>(content);
                                if (jObj != null && jObj["role"]?.ToString() == "user" && jObj["content"] is JValue contentVal)
                                {
                                    var contentStr = contentVal.ToString();
                                    var match = System.Text.RegularExpressions.Regex.Match(contentStr, @"Message:\s.*");
                                    if (match.Success)
                                    {
                                        jObj["content"] = match.Value;
                                    }
                                    // Only keep the filtered object
                                    result["gen_ai.user.message"].Add(jObj.ToString(Formatting.None));
                                }
                            }
                            catch
                            {
                                // If not JSON, fallback to original
                                result["gen_ai.user.message"].Add(content);
                            }
                            break;
                        }
                    }
                }
            }
            else if (activityEvent.Name == "gen_ai.choice")
            {
                if (activityEvent.Tags != null)
                {
                    foreach (var tag in activityEvent.Tags)
                    {
                        if (tag.Key == "gen_ai.event.content" && tag.Value is string content && !string.IsNullOrEmpty(content))
                        {
                            result["gen_ai.choice"].Add(content);
                            break;
                        }
                    }
                }
            }
        }
        return result;
    }
}