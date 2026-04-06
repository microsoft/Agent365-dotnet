// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
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

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = new SnakeCaseLowerNamingPolicy(),
                WriteIndented = false,
            };
            options.Converters.Add(new JsonStringEnumConverter(new SnakeCaseLowerNamingPolicy()));
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
        /// Serializes an object to a JSON string using the shared snake_case options.
        /// Non-throwing; returns <see cref="object.ToString"/> on error.
        /// </summary>
        public static string SerializeToJson(object value)
        {
            try
            {
                return JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);
            }
            catch (Exception)
            {
                return value?.ToString() ?? string.Empty;
            }
        }

        /// <summary>
        /// Serializes a versioned <see cref="InputMessages"/> wrapper to JSON.
        /// Non-throwing; falls back to a diagnostic payload on error.
        /// </summary>
        public static string SerializeMessages(InputMessages wrapper)
        {
            return SerializeMessagesCore(wrapper);
        }

        /// <summary>
        /// Serializes a versioned <see cref="OutputMessages"/> wrapper to JSON.
        /// Non-throwing; falls back to a diagnostic payload on error.
        /// </summary>
        public static string SerializeMessages(OutputMessages wrapper)
        {
            return SerializeMessagesCore(wrapper);
        }

        private static string SerializeMessagesCore(object wrapper)
        {
            try
            {
                return JsonSerializer.Serialize(wrapper, wrapper.GetType(), SerializerOptions);
            }
            catch (Exception)
            {
                return "{\"version\":\"" + MessageConstants.SchemaVersion + "\",\"messages\":[{\"role\":\"system\",\"parts\":[{\"type\":\"text\",\"content\":\"[serialization failed]\"}]}]}";
            }
        }

        /// <summary>
        /// A JSON naming policy that converts PascalCase names to snake_case_lower.
        /// </summary>
        internal sealed class SnakeCaseLowerNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return name;
                }

                var sb = new StringBuilder();
                for (int i = 0; i < name.Length; i++)
                {
                    char c = name[i];
                    if (char.IsUpper(c))
                    {
                        if (i > 0)
                        {
                            sb.Append('_');
                        }

                        sb.Append(char.ToLowerInvariant(c));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }
        }
    }
}
