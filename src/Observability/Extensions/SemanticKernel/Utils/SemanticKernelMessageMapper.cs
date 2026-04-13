// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Utils;

using Microsoft.Agents.A365.Observability.Runtime.Tracing;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Maps Semantic Kernel span events to A365 versioned message format.
/// SK emits gen_ai.* events with <c>gen_ai.event.content</c> JSON payloads;
/// this mapper converts them to <see cref="InputMessages"/> / <see cref="OutputMessages"/>.
/// </summary>
internal static class SemanticKernelMessageMapper
{
    /// <summary>
    /// Maps all input-related span events to a serialized A365 <see cref="InputMessages"/> JSON string.
    /// Input events: gen_ai.system.message, gen_ai.user.message, gen_ai.assistant.message, gen_ai.tool.message.
    /// </summary>
    public static string? MapInputMessages(Activity activity)
    {
        if (activity.Events == null)
            return null;

        var chatMessages = new List<ChatMessage>();
        string? lastAssistantToolCallId = null;

        foreach (var ev in activity.Events)
        {
            var content = GetEventContentTag(ev);
            if (string.IsNullOrEmpty(content))
                continue;

            switch (ev.Name)
            {
                case "gen_ai.system.message":
                case "gen_ai.tool.developer":
                    MapSystemMessage(content, chatMessages);
                    break;

                case "gen_ai.user.message":
                    MapUserMessage(content, chatMessages);
                    break;

                case "gen_ai.assistant.message":
                    lastAssistantToolCallId = MapAssistantMessage(content, chatMessages);
                    break;

                case "gen_ai.tool.message":
                    MapToolMessage(content, chatMessages, lastAssistantToolCallId);
                    lastAssistantToolCallId = null;
                    break;
            }
        }

        if (chatMessages.Count == 0)
            return null;

        return MessageUtils.Serialize(new InputMessages(chatMessages));
    }

    /// <summary>
    /// Maps gen_ai.choice span events to a serialized A365 <see cref="OutputMessages"/> JSON string.
    /// </summary>
    public static string? MapOutputMessages(Activity activity)
    {
        if (activity.Events == null)
            return null;

        var outputMessages = new List<OutputMessage>();

        foreach (var ev in activity.Events)
        {
            if (ev.Name != OpenTelemetryConstants.GenAiChoiceEventName)
                continue;

            var content = GetEventContentTag(ev);
            if (string.IsNullOrEmpty(content))
                continue;

            MapChoiceMessage(content, outputMessages);
        }

        if (outputMessages.Count == 0)
            return null;

        return MessageUtils.Serialize(new OutputMessages(outputMessages));
    }

    private static void MapSystemMessage(string json, List<ChatMessage> messages)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var textContent = GetStringProperty(root, "content");
            if (string.IsNullOrEmpty(textContent))
                return;

            messages.Add(new ChatMessage(
                MessageRole.System,
                new IMessagePart[] { new TextPart(textContent) },
                GetStringProperty(root, "name")));
        }
        catch (JsonException) { }
    }

    private static void MapUserMessage(string json, List<ChatMessage> messages)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var textContent = GetStringProperty(root, "content");
            if (string.IsNullOrEmpty(textContent))
                return;

            messages.Add(new ChatMessage(
                MessageRole.User,
                new IMessagePart[] { new TextPart(textContent) },
                GetStringProperty(root, "name")));
        }
        catch (JsonException) { }
    }

    /// <summary>
    /// Maps an assistant message. Returns the first tool call ID if present (for correlating tool responses).
    /// </summary>
    private static string? MapAssistantMessage(string json, List<ChatMessage> messages)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var parts = new List<IMessagePart>();
            string? firstToolCallId = null;

            var textContent = GetStringProperty(root, "content");
            if (!string.IsNullOrEmpty(textContent))
            {
                parts.Add(new TextPart(textContent));
            }

            if (root.TryGetProperty("tool_calls", out var toolCallsElement)
                && toolCallsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in toolCallsElement.EnumerateArray())
                {
                    var toolCallId = GetStringProperty(tc, "id");
                    if (firstToolCallId == null)
                        firstToolCallId = toolCallId;

                    string? functionName = null;
                    object? arguments = null;

                    if (tc.TryGetProperty("function", out var funcElement))
                    {
                        functionName = GetStringProperty(funcElement, "name");
                        if (funcElement.TryGetProperty("arguments", out var argsElement))
                        {
                            arguments = argsElement.ValueKind == JsonValueKind.String
                                ? argsElement.GetString()
                                : argsElement.ToString();
                        }
                    }

                    if (!string.IsNullOrEmpty(functionName))
                    {
                        parts.Add(new ToolCallRequestPart(functionName, toolCallId, arguments));
                    }
                }
            }

            if (parts.Count > 0)
            {
                messages.Add(new ChatMessage(
                    MessageRole.Assistant,
                    parts,
                    GetStringProperty(root, "name")));
            }

            return firstToolCallId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void MapToolMessage(string json, List<ChatMessage> messages, string? toolCallId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var textContent = GetStringProperty(root, "content");
            if (string.IsNullOrEmpty(textContent))
                return;

            messages.Add(new ChatMessage(
                MessageRole.Tool,
                new IMessagePart[] { new ToolCallResponsePart(toolCallId, textContent) }));
        }
        catch (JsonException) { }
    }

    private static void MapChoiceMessage(string json, List<OutputMessage> messages)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("message", out var msgElement))
                return;

            var parts = new List<IMessagePart>();

            var textContent = GetStringProperty(msgElement, "content");
            if (!string.IsNullOrEmpty(textContent))
            {
                parts.Add(new TextPart(textContent));
            }

            if (msgElement.TryGetProperty("tool_calls", out var toolCallsElement)
                && toolCallsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in toolCallsElement.EnumerateArray())
                {
                    var toolCallId = GetStringProperty(tc, "id");
                    string? functionName = null;
                    object? arguments = null;

                    if (tc.TryGetProperty("function", out var funcElement))
                    {
                        functionName = GetStringProperty(funcElement, "name");
                        if (funcElement.TryGetProperty("arguments", out var argsElement))
                        {
                            arguments = argsElement.ValueKind == JsonValueKind.String
                                ? argsElement.GetString()
                                : argsElement.ToString();
                        }
                    }

                    if (!string.IsNullOrEmpty(functionName))
                    {
                        parts.Add(new ToolCallRequestPart(functionName, toolCallId, arguments));
                    }
                }
            }

            if (parts.Count == 0)
                return;

            var role = MapRole(GetStringProperty(msgElement, "role"));
            var finishReason = MapFinishReason(GetStringProperty(root, "finish_reason"));

            messages.Add(new OutputMessage(role, parts, finishReason: finishReason));
        }
        catch (JsonException) { }
    }

    private static MessageRole MapRole(string? role)
    {
        if (string.IsNullOrEmpty(role))
            return MessageRole.Assistant;

        if (role.Equals("system", System.StringComparison.OrdinalIgnoreCase)) return MessageRole.System;
        if (role.Equals("user", System.StringComparison.OrdinalIgnoreCase)) return MessageRole.User;
        if (role.Equals("assistant", System.StringComparison.OrdinalIgnoreCase)) return MessageRole.Assistant;
        if (role.Equals("tool", System.StringComparison.OrdinalIgnoreCase)) return MessageRole.Tool;

        return MessageRole.Assistant;
    }

    private static string? MapFinishReason(string? skFinishReason)
    {
        if (string.IsNullOrEmpty(skFinishReason))
            return null;

        if (skFinishReason.Equals("Stop", System.StringComparison.OrdinalIgnoreCase)) return "stop";
        if (skFinishReason.Equals("Length", System.StringComparison.OrdinalIgnoreCase)) return "length";
        if (skFinishReason.Equals("ContentFilter", System.StringComparison.OrdinalIgnoreCase)) return "content_filter";
        if (skFinishReason.Equals("ToolCalls", System.StringComparison.OrdinalIgnoreCase)) return "tool_calls";

        return skFinishReason.ToLowerInvariant();
    }

    private static string? GetEventContentTag(ActivityEvent activityEvent)
    {
        return activityEvent.Tags?
            .FirstOrDefault(tag => tag.Key == SemanticKernelTelemetryConstants.EventContentTag).Value as string;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }
}
