// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Tooling.Models;

/// <summary>
/// Represents the request payload for a real-time threat protection check on a chat message.
/// </summary>
public class ChatMessageRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessageRequest"/> class.
    /// </summary>
    /// <param name="conversationId">The unique identifier for the conversation.</param>
    /// <param name="messageId">The unique identifier for the message within the conversation.</param>
    /// <param name="userMessage">The content of the user's message.</param>
    /// <param name="chatHistory">The chat history messages.</param>
    public ChatMessageRequest(string conversationId, string messageId, string userMessage, ChatHistoryMessage[] chatHistory)
    {
        ConversationId = conversationId;
        MessageId = messageId;
        UserMessage = userMessage;
        ChatHistory = chatHistory;
    }

    /// <summary>
    /// Gets or sets the unique identifier for the conversation.
    /// </summary>
    [JsonPropertyName("conversationId")]
    [Required]
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the message within the conversation.
    /// </summary>
    [JsonPropertyName("messageId")]
    [Required]
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the content of the user's message.
    /// </summary>
    [JsonPropertyName("userMessage")]
    [Required]
    public string UserMessage { get; set; }

    /// <summary>
    /// Gets or sets the chat history as a JSON element.
    /// </summary>
    [JsonPropertyName("chatHistory")]
    [Required]
    public ChatHistoryMessage[] ChatHistory { get; set; }
}
