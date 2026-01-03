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
/// Unit tests for McpToolRegistrationService.SendChatHistoryAsync(IEnumerable&lt;ChatMessage&gt;, ITurnContext, ToolOptions).
/// Tests parameter validation, chat message conversion, and error handling for the overload with ToolOptions.
/// </summary>
public class SendChatHistoryAsync_IEnumerable_WithToolOptions_Tests : McpToolRegistrationServiceTestBase
{
    [Fact]
    public async Task WithNullChatMessagesAndToolOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync((IEnumerable<ChatMessage>)null!, turnContextMock.Object, toolOptions);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("chatMessages");
    }

    [Fact]
    public async Task WithNullTurnContextAndToolOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var chatMessages = new List<ChatMessage>();
        var toolOptions = new ToolOptions();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(chatMessages, null!, toolOptions);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("turnContext");
    }

    [Fact]
    public async Task WithNullToolOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var chatMessages = new List<ChatMessage>();
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("toolOptions");
    }

    [Fact]
    public async Task WithAllParametersNull_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync((IEnumerable<ChatMessage>)null!, null!, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WithEmptyChatMessagesAndToolOptions_ReturnsSuccessWithoutCallingService()
    {
        // Arrange
        var service = CreateService();
        var chatMessages = new List<ChatMessage>();
        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, toolOptions);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify underlying service was NOT called
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ChatHistoryMessage[]>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WithValidChatMessagesAndToolOptions_CallsServiceWithConvertedMessages()
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
            new ChatMessage(ChatRole.User, "Test")
            {
                MessageId = "msg-1",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, toolOptions);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task WithCustomToolOptions_PassesOptionsToService()
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

        var mockUserAgentConfig = new Mock<IUserAgentConfiguration>();
        mockUserAgentConfig.Setup(c => c.ProductName).Returns("CustomProduct");
        mockUserAgentConfig.Setup(c => c.OrchestratorName).Returns("CustomOrchestrator");
        mockUserAgentConfig.Setup(c => c.Version).Returns("1.0.0");

        var customToolOptions = new ToolOptions
        {
            UserAgentConfiguration = mockUserAgentConfig.Object
        };

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
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, customToolOptions);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify the custom tool options were passed through
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.IsAny<ChatHistoryMessage[]>(),
                customToolOptions,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithoutToolOptions_UsesDefaultAgentFrameworkConfiguration()
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
        result.Succeeded.Should().BeTrue();

        // Verify default AgentFramework configuration was used
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.IsAny<ChatHistoryMessage[]>(),
                It.Is<ToolOptions>(opts =>
                    opts.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithMultipleMessageTypes_ConvertsAllRoles()
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

        var timestamp1 = DateTimeOffset.UtcNow;
        var timestamp2 = timestamp1.AddSeconds(10);
        var timestamp3 = timestamp1.AddSeconds(20);
        var timestamp4 = timestamp1.AddSeconds(30);

        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "What's the weather?") { MessageId = "msg-1", CreatedAt = timestamp1 },
            new ChatMessage(ChatRole.Assistant, "Let me check for you.") { MessageId = "msg-2", CreatedAt = timestamp2 },
            new ChatMessage(ChatRole.System, "System notification") { MessageId = "msg-3", CreatedAt = timestamp3 },
            new ChatMessage(ChatRole.Tool, "Weather data retrieved") { MessageId = "msg-4", CreatedAt = timestamp4 }
        };

        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, toolOptions);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify all message types were converted correctly
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.Is<ChatHistoryMessage[]>(messages =>
                    messages.Length == 4 &&
                    !string.IsNullOrEmpty(messages[0].Role) &&
                    !string.IsNullOrEmpty(messages[1].Role) &&
                    !string.IsNullOrEmpty(messages[2].Role) &&
                    !string.IsNullOrEmpty(messages[3].Role)),
                toolOptions,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithMultipleMessages_LogsCorrectCount()
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
            new ChatMessage(ChatRole.User, "Message 1") { MessageId = "msg-1", CreatedAt = DateTimeOffset.UtcNow },
            new ChatMessage(ChatRole.Assistant, "Message 2") { MessageId = "msg-2", CreatedAt = DateTimeOffset.UtcNow },
            new ChatMessage(ChatRole.User, "Message 3") { MessageId = "msg-3", CreatedAt = DateTimeOffset.UtcNow }
        };

        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, toolOptions);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify information log about conversion
        LoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Converted") && v.ToString()!.Contains("3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task WithNullTextContent_UsesEmptyString()
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
            new ChatMessage(ChatRole.User, (string?)null) // Null text
            {
                MessageId = "msg-1",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();

        // Act
        var result = await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, toolOptions);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify empty string was used for null text
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.Is<ChatHistoryMessage[]>(messages =>
                    messages.Length == 1 &&
                    messages[0].Content == string.Empty),
                toolOptions,
                It.IsAny<CancellationToken>()),
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
        var toolOptions = new ToolOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(chatMessages, turnContextMock.Object, toolOptions, cts.Token);

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
