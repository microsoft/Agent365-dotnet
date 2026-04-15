// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.AgentFramework.Utils;

using Microsoft.Agents.A365.Observability.Runtime.Tracing;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Maps Agent Framework span tag messages to A365 versioned message format.
/// Agent Framework sets <c>gen_ai.input.messages</c> / <c>gen_ai.output.messages</c> as span tags
/// containing JSON arrays of <c>{role, parts[{type, content}], finish_reason?}</c>.
/// This mapper converts them to <see cref="InputMessages"/> / <see cref="OutputMessages"/>.
/// </summary>
internal static class AgentFrameworkMessageMapper
{

    /// <summary>
    /// Maps the <c>gen_ai.input.messages</c> tag value to a serialized A365 <see cref="InputMessages"/> JSON string.
    /// </summary>
    public static string? MapInputMessages(Activity activity)
    {
        var json = GetTagValue(activity, OpenTelemetryConstants.GenAiInputMessagesKey);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            var chatMessages = new List<ChatMessage>();

            foreach (var msgElement in doc.RootElement.EnumerateArray())
            {
                var role = GetStringProperty(msgElement, "role");
                var mappedRole = MapRole(role, MessageRole.User);
                var parts = MapParts(msgElement);
                var name = GetStringProperty(msgElement, "name");

                if (parts.Count > 0)
                {
                    chatMessages.Add(new ChatMessage(mappedRole, parts, name));
                }
            }

            if (chatMessages.Count == 0)
                return null;

            return MessageUtils.Serialize(new InputMessages(chatMessages));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps the <c>gen_ai.output.messages</c> tag value to a serialized A365 <see cref="OutputMessages"/> JSON string.
    /// </summary>
    public static string? MapOutputMessages(Activity activity)
    {
        var json = GetTagValue(activity, OpenTelemetryConstants.GenAiOutputMessagesKey);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            var outputMessages = new List<OutputMessage>();

            foreach (var msgElement in doc.RootElement.EnumerateArray())
            {
                var role = MapRole(GetStringProperty(msgElement, "role"), MessageRole.Assistant);
                var parts = MapParts(msgElement);
                var finishReason = GetStringProperty(msgElement, "finish_reason");

                if (parts.Count > 0)
                {
                    outputMessages.Add(new OutputMessage(role, parts, finishReason: finishReason));
                }
            }

            if (outputMessages.Count == 0)
                return null;

            return MessageUtils.Serialize(new OutputMessages(outputMessages));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<IMessagePart> MapParts(JsonElement msgElement)
    {
        var parts = new List<IMessagePart>();

        if (!msgElement.TryGetProperty("parts", out var partsElement) ||
            partsElement.ValueKind != JsonValueKind.Array)
        {
            return parts;
        }

        foreach (var partElement in partsElement.EnumerateArray())
        {
            var partType = GetStringProperty(partElement, "type");
            if (string.IsNullOrEmpty(partType))
                continue;

            var part = MapSinglePart(partType, partElement);
            if (part != null)
            {
                parts.Add(part);
            }
        }

        return parts;
    }

    private static IMessagePart? MapSinglePart(string partType, JsonElement partElement)
    {
        switch (partType.ToLowerInvariant())
        {
            case "text":
                var textContent = GetStringProperty(partElement, "content");
                return !string.IsNullOrEmpty(textContent) ? new TextPart(textContent) : null;

            case "reasoning":
                var reasoningContent = GetStringProperty(partElement, "content");
                return !string.IsNullOrEmpty(reasoningContent) ? new ReasoningPart(reasoningContent) : null;

            case "tool_call":
                return MapToolCallPart(partElement);

            case "tool_call_response":
                return MapToolCallResponsePart(partElement);

            case "blob":
                return MapBlobPart(partElement);

            case "file":
                return MapFilePart(partElement);

            case "uri":
                return MapUriPart(partElement);

            case "server_tool_call":
                return MapServerToolCallPart(partElement);

            case "server_tool_call_response":
                return MapServerToolCallResponsePart(partElement);

            default:
                return MapGenericPart(partType, partElement);
        }
    }

    private static IMessagePart? MapToolCallPart(JsonElement el)
    {
        var name = GetStringProperty(el, "name");
        if (string.IsNullOrEmpty(name))
            return null;

        var id = GetStringProperty(el, "id");
        object? arguments = null;
        if (el.TryGetProperty("arguments", out var argsEl))
        {
            arguments = argsEl.ValueKind == JsonValueKind.String
                ? argsEl.GetString()
                : argsEl.ToString();
        }

        return new ToolCallRequestPart(name, id, arguments);
    }

    private static IMessagePart? MapToolCallResponsePart(JsonElement el)
    {
        var id = GetStringProperty(el, "id");
        object? response = null;
        if (el.TryGetProperty("response", out var respEl))
        {
            response = respEl.ValueKind == JsonValueKind.String
                ? respEl.GetString()
                : respEl.ToString();
        }

        return new ToolCallResponsePart(id, response);
    }

    private static IMessagePart? MapBlobPart(JsonElement el)
    {
        var modality = GetStringProperty(el, "modality");
        var content = GetStringProperty(el, "content");
        if (string.IsNullOrEmpty(modality) || string.IsNullOrEmpty(content))
            return null;

        var mimeType = GetStringProperty(el, "mime_type");
        return new BlobPart(modality, content, mimeType);
    }

    private static IMessagePart? MapFilePart(JsonElement el)
    {
        var modality = GetStringProperty(el, "modality");
        var fileId = GetStringProperty(el, "file_id");
        if (string.IsNullOrEmpty(modality) || string.IsNullOrEmpty(fileId))
            return null;

        var mimeType = GetStringProperty(el, "mime_type");
        return new FilePart(modality, fileId, mimeType);
    }

    private static IMessagePart? MapUriPart(JsonElement el)
    {
        var modality = GetStringProperty(el, "modality");
        var uri = GetStringProperty(el, "uri");
        if (string.IsNullOrEmpty(modality) || string.IsNullOrEmpty(uri))
            return null;

        var mimeType = GetStringProperty(el, "mime_type");
        return new UriPart(modality, uri, mimeType);
    }

    private static IMessagePart? MapServerToolCallPart(JsonElement el)
    {
        var name = GetStringProperty(el, "name");
        if (string.IsNullOrEmpty(name))
            return null;

        var id = GetStringProperty(el, "id");
        var payload = new Dictionary<string, object>();
        if (el.TryGetProperty("server_tool_call", out var stcEl))
        {
            payload["server_tool_call"] = stcEl.ToString();
        }

        return new ServerToolCallPart(name, payload, id);
    }

    private static IMessagePart? MapServerToolCallResponsePart(JsonElement el)
    {
        var id = GetStringProperty(el, "id");
        var payload = new Dictionary<string, object>();
        if (el.TryGetProperty("server_tool_call_response", out var strEl))
        {
            payload["server_tool_call_response"] = strEl.ToString();
        }

        return new ServerToolCallResponsePart(payload, id);
    }

    private static IMessagePart? MapGenericPart(string type, JsonElement el)
    {
        var data = new Dictionary<string, object>();
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Name != "type")
            {
                data[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()!
                    : prop.Value.ToString();
            }
        }

        return new GenericPart(type, data);
    }

    private static MessageRole MapRole(string? role, MessageRole defaultRole)
    {
        if (string.IsNullOrEmpty(role))
            return defaultRole;

        if (role.Equals("system", StringComparison.OrdinalIgnoreCase)) return MessageRole.System;
        if (role.Equals("user", StringComparison.OrdinalIgnoreCase)) return MessageRole.User;
        if (role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) return MessageRole.Assistant;
        if (role.Equals("tool", StringComparison.OrdinalIgnoreCase)) return MessageRole.Tool;

        return defaultRole;
    }

    private static string? GetTagValue(Activity activity, string key)
    {
        return activity.GetTagItem(key) as string;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }
}
