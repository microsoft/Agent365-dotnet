// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests;

/// <summary>
/// Unit tests for McpToolRegistrationService.SendChatHistoryAsync(IEnumerable&lt;ChatMessage&gt;, ITurnContext).
/// Tests parameter validation, chat message conversion, and error handling for the overload without ToolOptions.
/// </summary>
public class SendChatHistoryAsync_IEnumerable_Tests : McpToolRegistrationServiceTestBase
{
    [Fact]
    public async Task WithNullChatMessages_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync((IEnumerable<ChatMessage>)null!, turnContextMock.Object);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("chatMessages");
    }

    [Fact]
    public async Task WithNullTurnContext_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var chatMessages = new List<ChatMessage>();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(chatMessages, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("turnContext");
    }

    [Fact]
    public async Task WithEmptyChatMessages_CallsServiceWithEmptyArray()
    {
        // Arrange
        var expectedResult = OperationResult.Success;
        McpServerConfigurationServiceMock
            .Setup(s => s.SendChatHistoryAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ChatHistoryMessage[]>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var service = CreateService();
        var chatMessages = new List<ChatMessage>();
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify underlying service WAS called with empty array
        // Empty arrays must be passed to the MCP platform for correct behavior
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.Is<ChatHistoryMessage[]>(messages => messages.Length == 0),
                It.Is<ToolOptions>(opts =>
                    opts.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithValidChatMessages_CallsServiceWithConvertedMessages()
    {
        // Arrange
        var expectedResult = OperationResult.Success;
        McpServerConfigurationServiceMock
            .Setup(s => s.SendChatHistoryAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ChatHistoryMessage[]>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var service = CreateService();

        var timestamp1 = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var timestamp2 = new DateTimeOffset(2024, 1, 1, 12, 0, 30, TimeSpan.Zero);

        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Hello, how are you?")
            {
                MessageId = "msg-1",
                CreatedAt = timestamp1
            },
            new ChatMessage(ChatRole.Assistant, "I'm doing well, thank you!")
            {
                MessageId = "msg-2",
                CreatedAt = timestamp2
            }
        };

        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify the underlying service was called with converted messages
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.Is<ChatHistoryMessage[]>(messages =>
                    messages.Length == 2 &&
                    messages[0].Id == "msg-1" &&
                    !string.IsNullOrEmpty(messages[0].Role) &&
                    messages[0].Content == "Hello, how are you?" &&
                    messages[0].Timestamp == timestamp1 &&
                    messages[1].Id == "msg-2" &&
                    !string.IsNullOrEmpty(messages[1].Role) &&
                    messages[1].Content == "I'm doing well, thank you!" &&
                    messages[1].Timestamp == timestamp2),
                It.Is<ToolOptions>(opts =>
                    opts.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithValidChatMessages_PreservesMessageOrder()
    {
        // Arrange
        var expectedResult = OperationResult.Success;
        McpServerConfigurationServiceMock
            .Setup(s => s.SendChatHistoryAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ChatHistoryMessage[]>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var service = CreateService();

        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "First") { MessageId = "1", CreatedAt = DateTimeOffset.UtcNow },
            new ChatMessage(ChatRole.Assistant, "Second") { MessageId = "2", CreatedAt = DateTimeOffset.UtcNow },
            new ChatMessage(ChatRole.User, "Third") { MessageId = "3", CreatedAt = DateTimeOffset.UtcNow }
        };

        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify message order is preserved
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.Is<ChatHistoryMessage[]>(messages =>
                    messages.Length == 3 &&
                    messages[0].Id == "1" &&
                    messages[1].Id == "2" &&
                    messages[2].Id == "3"),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithMissingMessageId_GeneratesNewId()
    {
        // Arrange
        McpServerConfigurationServiceMock
            .Setup(s => s.SendChatHistoryAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ChatHistoryMessage[]>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Success);

        var service = CreateService();

        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Test message")
            {
                MessageId = null, // Missing MessageId
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify a GUID was generated for the missing MessageId
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.Is<ChatHistoryMessage[]>(messages =>
                    messages.Length == 1 &&
                    !string.IsNullOrEmpty(messages[0].Id) &&
                    IsValidGuid(messages[0].Id)),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithMissingTimestamp_UsesCurrentTime()
    {
        // Arrange
        var beforeCall = DateTimeOffset.UtcNow;

        McpServerConfigurationServiceMock
            .Setup(s => s.SendChatHistoryAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ChatHistoryMessage[]>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Success);

        var service = CreateService();

        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Test message")
            {
                MessageId = "msg-1",
                CreatedAt = null // Missing timestamp
            }
        };

        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object);

        var afterCall = DateTimeOffset.UtcNow;

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify a timestamp was generated
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.Is<ChatHistoryMessage[]>(messages =>
                    messages.Length == 1 &&
                    messages[0].Timestamp >= beforeCall &&
                    messages[0].Timestamp <= afterCall),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WhenServiceThrowsException_ReturnsFailedResult()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");
        McpServerConfigurationServiceMock
            .Setup(s => s.SendChatHistoryAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ChatHistoryMessage[]>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var service = CreateService();

        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Test")
            {
                MessageId = "msg-1",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        var error = result.Errors.First();
        error.Exception.Should().Be(expectedException);
        error.Message.Should().Contain("Test exception");

        // Verify error was logged
        LoggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send chat history")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = CreateService();
        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Test")
            {
                MessageId = "msg-1",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };
        var turnContextMock = new Mock<ITurnContext>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Verify underlying service was NOT called
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ChatHistoryMessage[]>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
