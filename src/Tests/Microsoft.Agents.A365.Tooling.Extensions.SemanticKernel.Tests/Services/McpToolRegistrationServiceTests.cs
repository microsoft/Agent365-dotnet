// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Tests.Services
{
    /// <summary>
    /// Unit tests for McpToolRegistrationService.SendChatHistoryAsync methods.
    /// Tests parameter validation, chat history conversion, and delegation to underlying service.
    /// </summary>
    public class McpToolRegistrationServiceTests
    {
        private readonly Mock<ILogger<IMcpToolRegistrationService>> _loggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IMcpToolServerConfigurationService> _mcpServerConfigurationServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;

        public McpToolRegistrationServiceTests()
        {
            _loggerMock = new Mock<ILogger<IMcpToolRegistrationService>>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _mcpServerConfigurationServiceMock = new Mock<IMcpToolServerConfigurationService>();
            _configurationMock = new Mock<IConfiguration>();
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenTurnContextIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Hello");

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(null!, chatHistory);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("turnContext");
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenChatHistoryIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("chatHistory");
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsOperationCanceledException_WhenCancellationTokenIsCanceled()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Hello");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenTurnContextIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Hello");
            var toolOptions = new ToolOptions();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(null!, chatHistory, toolOptions);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("turnContext");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenChatHistoryIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var toolOptions = new ToolOptions();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, null!, toolOptions);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("chatHistory");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenToolOptionsIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Hello");

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("toolOptions");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsOperationCanceledException_WhenCancellationTokenIsCanceled()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Hello");
            var toolOptions = new ToolOptions();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, toolOptions, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task SendChatHistoryAsync_CreatesDefaultToolOptions_WhenNotProvided()
        {
            // Arrange
            _mcpServerConfigurationServiceMock
                .Setup(s => s.SendChatHistoryAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success);

            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Hello");

            // Act
            await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            _mcpServerConfigurationServiceMock.Verify(
                s => s.SendChatHistoryAsync(
                    turnContextMock.Object,
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.Is<ToolOptions>(opts => opts.UserAgentConfiguration == Agent365SemanticKernelSdkUserAgentConfiguration.Instance),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendChatHistoryAsync_ConvertsChatHistoryToMessages_Correctly()
        {
            // Arrange
            ChatHistoryMessage[]? capturedMessages = null;
            _mcpServerConfigurationServiceMock
                .Setup(s => s.SendChatHistoryAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, ChatHistoryMessage[], ToolOptions, CancellationToken>((_, messages, _, _) =>
                {
                    capturedMessages = messages;
                })
                .ReturnsAsync(OperationResult.Success);

            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Hello, how are you?");
            chatHistory.AddAssistantMessage("I'm doing great, thank you!");
            chatHistory.AddSystemMessage("System notification");

            var toolOptions = new ToolOptions();

            // Act
            await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, toolOptions);

            // Assert
            capturedMessages.Should().NotBeNull();
            capturedMessages.Should().HaveCount(3);

            capturedMessages![0].Role.Should().Be("user");
            capturedMessages[0].Content.Should().Be("Hello, how are you?");
            capturedMessages[0].Id.Should().NotBeNullOrEmpty();

            capturedMessages[1].Role.Should().Be("assistant");
            capturedMessages[1].Content.Should().Be("I'm doing great, thank you!");
            capturedMessages[1].Id.Should().NotBeNullOrEmpty();

            capturedMessages[2].Role.Should().Be("system");
            capturedMessages[2].Content.Should().Be("System notification");
            capturedMessages[2].Id.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task SendChatHistoryAsync_HandlesNullContentInChatHistory()
        {
            // Arrange
            ChatHistoryMessage[]? capturedMessages = null;
            _mcpServerConfigurationServiceMock
                .Setup(s => s.SendChatHistoryAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, ChatHistoryMessage[], ToolOptions, CancellationToken>((_, messages, _, _) =>
                {
                    capturedMessages = messages;
                })
                .ReturnsAsync(OperationResult.Success);

            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage((string)null!); // Content is null

            var toolOptions = new ToolOptions();

            // Act
            await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, toolOptions);

            // Assert
            capturedMessages.Should().NotBeNull();
            capturedMessages.Should().HaveCount(1);
            capturedMessages![0].Content.Should().Be(string.Empty); // Null content should become empty string
        }

        [Fact]
        public async Task SendChatHistoryAsync_ReturnsOperationResult_FromUnderlyingService()
        {
            // Arrange
            var expectedResult = OperationResult.Success;
            _mcpServerConfigurationServiceMock
                .Setup(s => s.SendChatHistoryAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Test message");

            var toolOptions = new ToolOptions();

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, toolOptions);

            // Assert
            result.Should().BeSameAs(expectedResult);
        }

        [Fact]
        public async Task SendChatHistoryAsync_PassesCancellationToken_ToUnderlyingService()
        {
            // Arrange
            CancellationToken capturedToken = default;
            _mcpServerConfigurationServiceMock
                .Setup(s => s.SendChatHistoryAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, ChatHistoryMessage[], ToolOptions, CancellationToken>((_, _, _, token) =>
                {
                    capturedToken = token;
                })
                .ReturnsAsync(OperationResult.Success);

            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Test");

            var toolOptions = new ToolOptions();
            using var cts = new CancellationTokenSource();

            // Act
            await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, toolOptions, cts.Token);

            // Assert
            capturedToken.Should().Be(cts.Token);
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithoutToolOptions_DelegatesToOverloadWithToolOptions()
        {
            // Arrange
            _mcpServerConfigurationServiceMock
                .Setup(s => s.SendChatHistoryAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success);

            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Hello");

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeTrue();

            // Verify that the underlying service was called with default ToolOptions
            _mcpServerConfigurationServiceMock.Verify(
                s => s.SendChatHistoryAsync(
                    turnContextMock.Object,
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.Is<ToolOptions>(opts => opts.UserAgentConfiguration == Agent365SemanticKernelSdkUserAgentConfiguration.Instance),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendChatHistoryAsync_GeneratesUniqueIdsForEachMessage()
        {
            // Arrange
            ChatHistoryMessage[]? capturedMessages = null;
            _mcpServerConfigurationServiceMock
                .Setup(s => s.SendChatHistoryAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, ChatHistoryMessage[], ToolOptions, CancellationToken>((_, messages, _, _) =>
                {
                    capturedMessages = messages;
                })
                .ReturnsAsync(OperationResult.Success);

            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Message 1");
            chatHistory.AddUserMessage("Message 2");
            chatHistory.AddUserMessage("Message 3");

            var toolOptions = new ToolOptions();

            // Act
            await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, toolOptions);

            // Assert
            capturedMessages.Should().NotBeNull();
            capturedMessages.Should().HaveCount(3);

            var ids = capturedMessages!.Select(m => m.Id).ToList();
            ids.Should().OnlyHaveUniqueItems();
            ids.Should().AllSatisfy(id => id.Should().NotBeNullOrEmpty());
        }

        [Fact]
        public async Task SendChatHistoryAsync_SetsTimestampForEachMessage()
        {
            // Arrange
            ChatHistoryMessage[]? capturedMessages = null;
            _mcpServerConfigurationServiceMock
                .Setup(s => s.SendChatHistoryAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ChatHistoryMessage[]>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, ChatHistoryMessage[], ToolOptions, CancellationToken>((_, messages, _, _) =>
                {
                    capturedMessages = messages;
                })
                .ReturnsAsync(OperationResult.Success);

            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Message 1");
            chatHistory.AddAssistantMessage("Message 2");

            var toolOptions = new ToolOptions();
            var beforeCall = DateTimeOffset.UtcNow;

            // Act
            await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, toolOptions);

            var afterCall = DateTimeOffset.UtcNow;

            // Assert
            capturedMessages.Should().NotBeNull();
            capturedMessages.Should().HaveCount(2);

            capturedMessages!.Should().AllSatisfy(message =>
            {
                message.Timestamp.Should().BeOnOrAfter(beforeCall);
                message.Timestamp.Should().BeOnOrBefore(afterCall);
            });
        }
    }
}
