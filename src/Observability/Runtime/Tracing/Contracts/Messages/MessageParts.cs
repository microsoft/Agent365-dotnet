// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages
{
    /// <summary>
    /// Marker interface for all message part types per OTEL gen-ai semantic conventions.
    /// </summary>
    public interface IMessagePart
    {
        /// <summary>
        /// Gets the discriminator string for this part type (e.g. "text", "tool_call").
        /// </summary>
        string Type { get; }
    }

    /// <summary>
    /// Plain text content part.
    /// </summary>
    public sealed class TextPart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextPart"/> class.
        /// </summary>
        /// <param name="content">The text content.</param>
        public TextPart(string content)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        /// <summary>Gets the text content.</summary>
        public string Content { get; }

        /// <inheritdoc/>
        public string Type => "text";
    }

    /// <summary>
    /// A tool call requested by the model.
    /// </summary>
    public sealed class ToolCallRequestPart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallRequestPart"/> class.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="id">Optional tool call identifier.</param>
        /// <param name="arguments">Optional arguments for the tool call.</param>
        public ToolCallRequestPart(string name, string? id = null, object? arguments = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Id = id;
            Arguments = arguments;
        }

        /// <summary>Gets the tool name.</summary>
        public string Name { get; }

        /// <summary>Gets the optional tool call identifier.</summary>
        public string? Id { get; }

        /// <summary>Gets the optional arguments for the tool call.</summary>
        public object? Arguments { get; }

        /// <inheritdoc/>
        public string Type => "tool_call";
    }

    /// <summary>
    /// Result of a tool call.
    /// </summary>
    public sealed class ToolCallResponsePart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallResponsePart"/> class.
        /// </summary>
        /// <param name="id">Optional tool call identifier.</param>
        /// <param name="response">Optional tool response payload.</param>
        public ToolCallResponsePart(string? id = null, object? response = null)
        {
            Id = id;
            Response = response;
        }

        /// <summary>Gets the optional tool call identifier.</summary>
        public string? Id { get; }

        /// <summary>Gets the optional tool response payload.</summary>
        public object? Response { get; }

        /// <inheritdoc/>
        public string Type => "tool_call_response";
    }

    /// <summary>
    /// Model reasoning / chain-of-thought content.
    /// </summary>
    public sealed class ReasoningPart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReasoningPart"/> class.
        /// </summary>
        /// <param name="content">The reasoning content.</param>
        public ReasoningPart(string content)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        /// <summary>Gets the reasoning content.</summary>
        public string Content { get; }

        /// <inheritdoc/>
        public string Type => "reasoning";
    }

    /// <summary>
    /// Inline binary data (base64-encoded).
    /// </summary>
    public sealed class BlobPart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlobPart"/> class.
        /// </summary>
        /// <param name="modality">The media modality.</param>
        /// <param name="content">The base64-encoded binary content.</param>
        /// <param name="mimeType">Optional MIME type.</param>
        public BlobPart(string modality, string content, string? mimeType = null)
        {
            Modality = modality ?? throw new ArgumentNullException(nameof(modality));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            MimeType = mimeType;
        }

        /// <summary>Gets the media modality.</summary>
        public string Modality { get; }

        /// <summary>Gets the base64-encoded binary content.</summary>
        public string Content { get; }

        /// <summary>Gets the optional MIME type.</summary>
        public string? MimeType { get; }

        /// <inheritdoc/>
        public string Type => "blob";
    }

    /// <summary>
    /// Reference to a pre-uploaded file.
    /// </summary>
    public sealed class FilePart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FilePart"/> class.
        /// </summary>
        /// <param name="modality">The media modality.</param>
        /// <param name="fileId">The file identifier.</param>
        /// <param name="mimeType">Optional MIME type.</param>
        public FilePart(string modality, string fileId, string? mimeType = null)
        {
            Modality = modality ?? throw new ArgumentNullException(nameof(modality));
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
            MimeType = mimeType;
        }

        /// <summary>Gets the media modality.</summary>
        public string Modality { get; }

        /// <summary>Gets the file identifier.</summary>
        public string FileId { get; }

        /// <summary>Gets the optional MIME type.</summary>
        public string? MimeType { get; }

        /// <inheritdoc/>
        public string Type => "file";
    }

    /// <summary>
    /// External URI reference.
    /// </summary>
    public sealed class UriPart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UriPart"/> class.
        /// </summary>
        /// <param name="modality">The media modality.</param>
        /// <param name="uri">The external URI.</param>
        /// <param name="mimeType">Optional MIME type.</param>
        public UriPart(string modality, string uri, string? mimeType = null)
        {
            Modality = modality ?? throw new ArgumentNullException(nameof(modality));
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            MimeType = mimeType;
        }

        /// <summary>Gets the media modality.</summary>
        public string Modality { get; }

        /// <summary>Gets the external URI.</summary>
        public string Uri { get; }

        /// <summary>Gets the optional MIME type.</summary>
        public string? MimeType { get; }

        /// <inheritdoc/>
        public string Type => "uri";
    }

    /// <summary>
    /// Server-side tool invocation.
    /// </summary>
    public sealed class ServerToolCallPart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerToolCallPart"/> class.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="serverToolCall">The tool call payload.</param>
        /// <param name="id">Optional tool call identifier.</param>
        public ServerToolCallPart(string name, IDictionary<string, object> serverToolCall, string? id = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ServerToolCall = serverToolCall ?? throw new ArgumentNullException(nameof(serverToolCall));
            Id = id;
        }

        /// <summary>Gets the tool name.</summary>
        public string Name { get; }

        /// <summary>Gets the tool call payload.</summary>
        public IDictionary<string, object> ServerToolCall { get; }

        /// <summary>Gets the optional tool call identifier.</summary>
        public string? Id { get; }

        /// <inheritdoc/>
        public string Type => "server_tool_call";
    }

    /// <summary>
    /// Server-side tool response.
    /// </summary>
    public sealed class ServerToolCallResponsePart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerToolCallResponsePart"/> class.
        /// </summary>
        /// <param name="serverToolCallResponse">The tool response payload.</param>
        /// <param name="id">Optional tool call identifier.</param>
        public ServerToolCallResponsePart(IDictionary<string, object> serverToolCallResponse, string? id = null)
        {
            ServerToolCallResponse = serverToolCallResponse ?? throw new ArgumentNullException(nameof(serverToolCallResponse));
            Id = id;
        }

        /// <summary>Gets the tool response payload.</summary>
        public IDictionary<string, object> ServerToolCallResponse { get; }

        /// <summary>Gets the optional tool call identifier.</summary>
        public string? Id { get; }

        /// <inheritdoc/>
        public string Type => "server_tool_call_response";
    }

    /// <summary>
    /// Extensible part for custom / future types.
    /// </summary>
    public sealed class GenericPart : IMessagePart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericPart"/> class.
        /// </summary>
        /// <param name="type">The custom type discriminator.</param>
        /// <param name="data">Optional payload dictionary.</param>
        public GenericPart(string type, IDictionary<string, object>? data = null)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Data = data ?? new Dictionary<string, object>();
        }

        /// <inheritdoc/>
        public string Type { get; }

        /// <summary>Gets the payload dictionary.</summary>
        public IDictionary<string, object> Data { get; }
    }

    /// <summary>
    /// Custom JSON converter that serializes <see cref="IMessagePart"/> using the runtime concrete type,
    /// ensuring all properties of derived types (TextPart, ToolCallRequestPart, etc.) are included.
    /// </summary>
    internal sealed class MessagePartConverter : JsonConverter<IMessagePart>
    {
        /// <inheritdoc/>
        public override IMessagePart Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Deserialization of IMessagePart is not supported.");
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, IMessagePart value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
