// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests.TestHelpers;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests;

/// <summary>
/// Unit tests for McpToolRegistrationService.SendChatHistoryAsync(ChatMessageStore, ITurnContext) methods.
/// Tests parameter validation, chat message retrieval from store, and error handling for ChatMessageStore overloads.
/// </summary>
public class SendChatHistoryAsync_ChatMessageStore_Tests : McpToolRegistrationServiceTestBase
{
    [Fact]
    public async Task WithNullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync((ChatMessageStore)null!, turnContextMock.Object);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("chatMessageStore");
    }

    [Fact]
    public async Task WithNullTurnContext_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var storeMock = new TestChatMessageStore(new List<ChatMessage>());

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(storeMock, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("turnContext");
    }

    [Fact]
    public async Task WithNullToolOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var storeMock = new TestChatMessageStore(new List<ChatMessage>());
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(storeMock, turnContextMock.Object, null!);

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
        Func<Task> act = async () => await service.SendChatHistoryAsync((ChatMessageStore)null!, null!, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WithEmptyStore_ReturnsSuccessWithoutCallingService()
    {
        // Arrange
        var service = CreateService();
        var store = new TestChatMessageStore(new List<ChatMessage>());
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(store, turnContextMock.Object);

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
    public async Task WithEmptyStoreAndToolOptions_ReturnsSuccessWithoutCallingService()
    {
        // Arrange
        var service = CreateService();
        var store = new TestChatMessageStore(new List<ChatMessage>());
        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();

        // Act
        var result = await service.SendChatHistoryAsync(store, turnContextMock.Object, toolOptions);

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
    public async Task WithValidStore_RetrievesAndSendsMessages()
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
            new ChatMessage(ChatRole.User, "Hello") { MessageId = "msg-1", CreatedAt = DateTimeOffset.UtcNow },
            new ChatMessage(ChatRole.Assistant, "Hi there!") { MessageId = "msg-2", CreatedAt = DateTimeOffset.UtcNow }
        };

        var store = new TestChatMessageStore(chatMessages);
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(store, turnContextMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();

        // Verify the underlying service was called with the messages from the store
        McpServerConfigurationServiceMock.Verify(
            s => s.SendChatHistoryAsync(
                turnContextMock.Object,
                It.Is<ChatHistoryMessage[]>(messages =>
                    messages.Length == 2 &&
                    messages[0].Id == "msg-1" &&
                    messages[1].Id == "msg-2"),
                It.Is<ToolOptions>(opts =>
                    opts.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithValidStoreAndToolOptions_RetrievesAndSendsMessages()
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
            new ChatMessage(ChatRole.User, "Test") { MessageId = "msg-1", CreatedAt = DateTimeOffset.UtcNow }
        };

        var store = new TestChatMessageStore(chatMessages);
        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();

        // Act
        var result = await service.SendChatHistoryAsync(store, turnContextMock.Object, toolOptions);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task WhenStoreThrowsException_ReturnsFailedResult()
    {
        // Arrange
        var service = CreateService();
        var store = new TestChatMessageStore(throwException: true);
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(store, turnContextMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle();

        // Verify error was logged
        LoggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to retrieve and send chat history from ChatMessageStore")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
            new ChatMessage(ChatRole.User, "Test") { MessageId = "msg-1", CreatedAt = DateTimeOffset.UtcNow }
        };

        var store = new TestChatMessageStore(chatMessages);
        var turnContextMock = new Mock<ITurnContext>();

        // Act
        var result = await service.SendChatHistoryAsync(store, turnContextMock.Object, customToolOptions);

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
    public async Task WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = CreateService();
        var store = new TestChatMessageStore(new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Test") { MessageId = "msg-1", CreatedAt = DateTimeOffset.UtcNow }
        });
        var turnContextMock = new Mock<ITurnContext>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(store, turnContextMock.Object, cts.Token);

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

    [Fact]
    public async Task WithCancelledTokenAndToolOptions_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = CreateService();
        var store = new TestChatMessageStore(new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Test") { MessageId = "msg-1", CreatedAt = DateTimeOffset.UtcNow }
        });
        var turnContextMock = new Mock<ITurnContext>();
        var toolOptions = new ToolOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.SendChatHistoryAsync(store, turnContextMock.Object, toolOptions, cts.Token);

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
