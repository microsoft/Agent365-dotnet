// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing
{
    /// <summary>
    /// Conversion and serialization helpers for OTEL gen-ai message format.
    /// Provides normalization from plain <c>string[]</c> (backward compat) to the
    /// versioned wrapper format, and non-throwing SerializeMessages methods.
    /// </summary>
    internal static class MessageUtils
    {
        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        private static readonly string DiagnosticFallback =
            "{\"version\":\"" + MessageConstants.SchemaVersion + "\",\"messages\":[{\"role\":\"system\",\"parts\":[{\"type\":\"text\",\"content\":\"[serialization failed]\"}]}]}";

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false,
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
            options.Converters.Add(new MessagePartConverter());
            return options;
        }

        // -------------------------------------------------------------------
        // Normalization: string → structured wrappers
        // -------------------------------------------------------------------

        /// <summary>
        /// Normalizes a single string into a versioned <see cref="InputMessages"/> wrapper.
        /// The string is wrapped as a user role <see cref="TextPart"/>.
        /// </summary>
        public static InputMessages NormalizeInputMessages(string content)
        {
            return NormalizeInputMessages(new[] { content });
        }

        /// <summary>
        /// Normalizes a string array into a versioned <see cref="InputMessages"/> wrapper.
        /// Each string is wrapped as a user role <see cref="TextPart"/>.
        /// </summary>
        public static InputMessages NormalizeInputMessages(IEnumerable<string> messages)
        {
            var chatMessages = new List<ChatMessage>();
            foreach (var msg in messages)
            {
                if (msg == null) continue;
                chatMessages.Add(new ChatMessage(
                    MessageRole.User,
                    new IMessagePart[] { new TextPart(msg) }));
            }

            return new InputMessages(chatMessages);
        }

        /// <summary>
        /// Normalizes a single string into a versioned <see cref="OutputMessages"/> wrapper.
        /// The string is wrapped as an assistant role <see cref="TextPart"/>.
        /// </summary>
        public static OutputMessages NormalizeOutputMessages(string content)
        {
            return NormalizeOutputMessages(new[] { content });
        }

        /// <summary>
        /// Normalizes a string array into a versioned <see cref="OutputMessages"/> wrapper.
        /// Each string is wrapped as an assistant role <see cref="TextPart"/>.
        /// </summary>
        public static OutputMessages NormalizeOutputMessages(IEnumerable<string> messages)
        {
            var outputMsgs = new List<OutputMessage>();
            foreach (var msg in messages)
            {
                if (msg == null) continue;
                outputMsgs.Add(new OutputMessage(
                    MessageRole.Assistant,
                    new IMessagePart[] { new TextPart(msg) }));
            }

            return new OutputMessages(outputMsgs);
        }

        // -------------------------------------------------------------------
        // Serialization
        // -------------------------------------------------------------------

        /// <summary>
        /// Returns <c>true</c> if the string is a valid JSON object or array.
        /// Primitives (strings, numbers, booleans, null) return <c>false</c>.
        /// </summary>
        public static bool IsJson(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(value!);
                var kind = doc.RootElement.ValueKind;
                return kind == JsonValueKind.Object || kind == JsonValueKind.Array;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Serializes an object to a JSON string using the shared snake_case options.
        /// Non-throwing; falls back to a diagnostic payload on error.
        /// </summary>
        public static string Serialize(object value)
        {
            try
            {
                return JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);
            }
            catch (Exception)
            {
                return DiagnosticFallback;
            }
        }
    }
}
