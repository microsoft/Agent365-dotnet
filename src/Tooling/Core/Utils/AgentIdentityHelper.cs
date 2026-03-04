// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.Builder;
using System.Text.Json;

namespace Microsoft.Agents.A365.Tooling.Utils;

/// <summary>
/// Utility methods for building agent identity context and injecting it into MCP requests.
/// </summary>
public static class AgentIdentityHelper
{
    /// <summary>
    /// Builds an <see cref="AgentIdentityContext"/> from the given turn context and resolved identity.
    /// </summary>
    /// <param name="turnContext">The current turn context.</param>
    /// <param name="agentInstanceId">The resolved agent instance ID (from <c>ResolveAgentIdentity</c>).</param>
    /// <returns>A populated <see cref="AgentIdentityContext"/>.</returns>
    public static AgentIdentityContext BuildFromTurnContext(ITurnContext turnContext, string? agentInstanceId)
    {
        var activity = turnContext?.Activity;

        var context = new AgentIdentityContext
        {
            AgentInstanceId = agentInstanceId,
        };

        if (activity != null)
        {
            context.TenantId = activity.Conversation?.TenantId;
            context.UserAadObjectId = activity.From?.AadObjectId;
            context.UserName = activity.From?.Name;
            context.ConversationId = activity.Conversation?.Id;
            context.ChannelId = activity.ChannelId?.Channel;

            // Try to get the blueprint app ID from the Recipient properties
            if (activity.Recipient?.Properties != null &&
                activity.Recipient.Properties.TryGetValue("agenticAppId", out var appIdElement))
            {
                context.AgentAppId = appIdElement.ToString();
            }
        }

        return context;
    }

    /// <summary>
    /// Injects agent identity into the <c>_meta</c> field of a serialized JSON-RPC <c>tools/call</c> message.
    /// If <c>_meta</c> already exists in the message's <c>params</c>, the identity fields are merged into it.
    /// Non-<c>tools/call</c> messages are returned unchanged.
    /// </summary>
    /// <param name="messageJson">The serialized JSON-RPC message.</param>
    /// <param name="identityContext">The agent identity to inject.</param>
    /// <returns>The modified JSON string with agent identity in <c>_meta</c>.</returns>
    public static string InjectIdentityIntoMcpMessage(string messageJson, AgentIdentityContext identityContext)
    {
        if (identityContext == null || string.IsNullOrEmpty(messageJson))
        {
            return messageJson;
        }

        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;

            // Only inject into tools/call requests
            if (!root.TryGetProperty("method", out var methodProp) ||
                methodProp.GetString() != "tools/call")
            {
                return messageJson;
            }

            // Must have a params object
            if (!root.TryGetProperty("params", out var paramsProp) ||
                paramsProp.ValueKind != JsonValueKind.Object)
            {
                return messageJson;
            }

            // Rebuild the message with _meta.agentIdentity injected
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name == "params")
                    {
                        writer.WritePropertyName("params");
                        WriteParamsWithIdentity(writer, prop.Value, identityContext);
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            // If injection fails for any reason, return the original message unchanged
            return messageJson;
        }
    }

    /// <summary>
    /// Writes the params object with agent identity added to the _meta field.
    /// </summary>
    private static void WriteParamsWithIdentity(Utf8JsonWriter writer, JsonElement paramsElement, AgentIdentityContext identityContext)
    {
        writer.WriteStartObject();

        bool metaWritten = false;

        foreach (var prop in paramsElement.EnumerateObject())
        {
            if (prop.Name == "_meta")
            {
                // Merge identity into existing _meta
                writer.WritePropertyName("_meta");
                WriteMergedMeta(writer, prop.Value, identityContext);
                metaWritten = true;
            }
            else
            {
                prop.WriteTo(writer);
            }
        }

        // If _meta didn't exist, create it
        if (!metaWritten)
        {
            writer.WritePropertyName("_meta");
            writer.WriteStartObject();
            WriteIdentityProperties(writer, identityContext);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the _meta object with existing properties plus agent identity.
    /// </summary>
    private static void WriteMergedMeta(Utf8JsonWriter writer, JsonElement existingMeta, AgentIdentityContext identityContext)
    {
        writer.WriteStartObject();

        // Write all existing _meta properties
        foreach (var prop in existingMeta.EnumerateObject())
        {
            if (prop.Name != "agentIdentity")
            {
                prop.WriteTo(writer);
            }
        }

        // Add/overwrite agentIdentity
        WriteIdentityProperties(writer, identityContext);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the agent identity properties into the current object.
    /// The identity is nested under an "agentIdentity" key to avoid conflicts with other _meta fields.
    /// </summary>
    private static void WriteIdentityProperties(Utf8JsonWriter writer, AgentIdentityContext identityContext)
    {
        writer.WritePropertyName("agentIdentity");
        writer.WriteRawValue(JsonSerializer.Serialize(identityContext, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
    }
}
