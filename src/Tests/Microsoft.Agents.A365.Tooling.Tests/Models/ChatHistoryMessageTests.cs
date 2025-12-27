// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Tests.Models
{
    /// <summary>
    /// Unit tests for ChatHistoryMessage class.
    /// Tests serialization, deserialization, and property validation.
    /// </summary>
    public class ChatHistoryMessageTests
    {
        [Fact]
        public void ChatHistoryMessage_CanBeInstantiated()
        {
            // Arrange & Act
            var message = new ChatHistoryMessage
            {
                Id = "msg-123",
                Role = "user",
                Content = "Hello, world!",
                Timestamp = DateTimeOffset.UtcNow
            };

            // Assert
            message.Should().NotBeNull();
            message.Id.Should().Be("msg-123");
            message.Role.Should().Be("user");
            message.Content.Should().Be("Hello, world!");
            message.Timestamp.Should().NotBeNull();
        }

        [Fact]
        public void ChatHistoryMessage_SerializesToJson()
        {
            // Arrange
            var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
            var message = new ChatHistoryMessage
            {
                Id = "msg-456",
                Role = "assistant",
                Content = "How can I help you?",
                Timestamp = timestamp
            };

            // Act
            var json = JsonSerializer.Serialize(message);

            // Assert
            json.Should().Contain("\"id\":\"msg-456\"");
            json.Should().Contain("\"role\":\"assistant\"");
            json.Should().Contain("\"content\":\"How can I help you?\"");
            json.Should().Contain("\"timestamp\":");
        }

        [Fact]
        public void ChatHistoryMessage_DeserializesFromJson()
        {
            // Arrange
            var json = """
                {
                    "id": "msg-789",
                    "role": "user",
                    "content": "What is the weather?",
                    "timestamp": "2024-01-15T10:30:00Z"
                }
                """;

            // Act
            var message = JsonSerializer.Deserialize<ChatHistoryMessage>(json);

            // Assert
            message.Should().NotBeNull();
            message!.Id.Should().Be("msg-789");
            message.Role.Should().Be("user");
            message.Content.Should().Be("What is the weather?");
            message.Timestamp.Should().Be(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero));
        }

        [Fact]
        public void ChatHistoryMessage_AllowsNullableProperties()
        {
            // Arrange & Act
            var message = new ChatHistoryMessage();

            // Assert
            message.Id.Should().BeNull();
            message.Role.Should().BeNull();
            message.Content.Should().BeNull();
            message.Timestamp.Should().BeNull();
        }

        [Fact]
        public void ChatHistoryMessage_SupportsSystemRole()
        {
            // Arrange & Act
            var message = new ChatHistoryMessage
            {
                Id = "sys-001",
                Role = "system",
                Content = "You are a helpful assistant.",
                Timestamp = DateTimeOffset.UtcNow
            };

            // Assert
            message.Role.Should().Be("system");
        }

        [Fact]
        public void ChatHistoryMessage_PreservesTimestampPrecision()
        {
            // Arrange
            var expectedTimestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 45, 123, TimeSpan.FromHours(-5));
            var message = new ChatHistoryMessage
            {
                Id = "msg-001",
                Role = "user",
                Content = "Test",
                Timestamp = expectedTimestamp
            };

            // Act
            var json = JsonSerializer.Serialize(message);
            var deserialized = JsonSerializer.Deserialize<ChatHistoryMessage>(json);

            // Assert
            deserialized!.Timestamp.Should().Be(expectedTimestamp);
        }
    }
}
