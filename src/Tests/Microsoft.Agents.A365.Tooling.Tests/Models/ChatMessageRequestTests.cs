// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Tests.Models
{
    /// <summary>
    /// Unit tests for ChatMessageRequest class.
    /// Tests serialization, deserialization, and constructor behavior.
    /// </summary>
    public class ChatMessageRequestTests
    {
        [Fact]
        public void ChatMessageRequest_ConstructorInitializesProperties()
        {
            // Arrange
            var conversationId = "conv-123";
            var messageId = "msg-456";
            var userMessage = "Hello!";
            var chatHistory = new[]
            {
                new ChatHistoryMessage { Id = "msg-1", Role = "user", Content = "Hi", Timestamp = DateTimeOffset.UtcNow }
            };

            // Act
            var request = new ChatMessageRequest(conversationId, messageId, userMessage, chatHistory);

            // Assert
            request.ConversationId.Should().Be(conversationId);
            request.MessageId.Should().Be(messageId);
            request.UserMessage.Should().Be(userMessage);
            request.ChatHistory.Should().BeSameAs(chatHistory);
        }

        [Fact]
        public void ChatMessageRequest_SerializesToJson()
        {
            // Arrange
            var chatHistory = new[]
            {
                new ChatHistoryMessage
                {
                    Id = "msg-1",
                    Role = "user",
                    Content = "Previous message",
                    Timestamp = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero)
                }
            };
            var request = new ChatMessageRequest("conv-789", "msg-999", "Current message", chatHistory);

            // Act
            var json = JsonSerializer.Serialize(request);

            // Assert
            json.Should().Contain("\"conversationId\":\"conv-789\"");
            json.Should().Contain("\"messageId\":\"msg-999\"");
            json.Should().Contain("\"userMessage\":\"Current message\"");
            json.Should().Contain("\"chatHistory\":");
            json.Should().Contain("\"Previous message\"");
        }

        [Fact]
        public void ChatMessageRequest_DeserializesFromJson()
        {
            // Arrange
            var json = """
                {
                    "conversationId": "conv-abc",
                    "messageId": "msg-def",
                    "userMessage": "Test message",
                    "chatHistory": [
                        {
                            "id": "msg-1",
                            "role": "user",
                            "content": "First message",
                            "timestamp": "2024-01-15T10:00:00Z"
                        },
                        {
                            "id": "msg-2",
                            "role": "assistant",
                            "content": "Second message",
                            "timestamp": "2024-01-15T10:01:00Z"
                        }
                    ]
                }
                """;

            // Act
            var request = JsonSerializer.Deserialize<ChatMessageRequest>(json);

            // Assert
            request.Should().NotBeNull();
            request!.ConversationId.Should().Be("conv-abc");
            request.MessageId.Should().Be("msg-def");
            request.UserMessage.Should().Be("Test message");
            request.ChatHistory.Should().HaveCount(2);
            request.ChatHistory[0].Content.Should().Be("First message");
            request.ChatHistory[1].Content.Should().Be("Second message");
        }

        [Fact]
        public void ChatMessageRequest_SupportsEmptyChatHistory()
        {
            // Arrange
            var emptyChatHistory = Array.Empty<ChatHistoryMessage>();

            // Act
            var request = new ChatMessageRequest("conv-001", "msg-001", "Hello", emptyChatHistory);

            // Assert
            request.ChatHistory.Should().BeEmpty();
        }

        [Fact]
        public void ChatMessageRequest_PreservesMultipleChatHistoryMessages()
        {
            // Arrange
            var chatHistory = new[]
            {
                new ChatHistoryMessage { Id = "1", Role = "user", Content = "Message 1", Timestamp = DateTimeOffset.UtcNow },
                new ChatHistoryMessage { Id = "2", Role = "assistant", Content = "Message 2", Timestamp = DateTimeOffset.UtcNow },
                new ChatHistoryMessage { Id = "3", Role = "user", Content = "Message 3", Timestamp = DateTimeOffset.UtcNow }
            };

            // Act
            var request = new ChatMessageRequest("conv-multi", "msg-multi", "Latest message", chatHistory);
            var json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<ChatMessageRequest>(json);

            // Assert
            deserialized!.ChatHistory.Should().HaveCount(3);
            deserialized.ChatHistory[0].Content.Should().Be("Message 1");
            deserialized.ChatHistory[1].Content.Should().Be("Message 2");
            deserialized.ChatHistory[2].Content.Should().Be("Message 3");
        }

        [Fact]
        public void ChatMessageRequest_PropertiesAreSettable()
        {
            // Arrange
            var request = new ChatMessageRequest("conv-1", "msg-1", "Message 1", Array.Empty<ChatHistoryMessage>());

            // Act
            request.ConversationId = "conv-2";
            request.MessageId = "msg-2";
            request.UserMessage = "Message 2";
            request.ChatHistory = new[] { new ChatHistoryMessage { Id = "new", Role = "user", Content = "New", Timestamp = DateTimeOffset.UtcNow } };

            // Assert
            request.ConversationId.Should().Be("conv-2");
            request.MessageId.Should().Be("msg-2");
            request.UserMessage.Should().Be("Message 2");
            request.ChatHistory.Should().HaveCount(1);
        }
    }
}
