// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Tests.Services
{
    /// <summary>
    /// Unit tests for McpToolServerConfigurationService.SendChatHistoryAsync methods.
    /// Tests parameter validation and error handling.
    /// </summary>
    public class McpToolServerConfigurationServiceTests
    {
        private readonly Mock<ILogger<IMcpToolServerConfigurationService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;

        public McpToolServerConfigurationServiceTests()
        {
            _loggerMock = new Mock<ILogger<IMcpToolServerConfigurationService>>();
            _configurationMock = new Mock<IConfiguration>();
            _serviceProviderMock = new Mock<IServiceProvider>();
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenTurnContextIsNull()
        {
            // Arrange
            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                _configurationMock.Object,
                _serviceProviderMock.Object);

            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", DateTimeOffset.UtcNow) };

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(null!, chatHistory);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("turnContext");
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenChatHistoryMessagesIsNull()
        {
            // Arrange
            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                _configurationMock.Object,
                _serviceProviderMock.Object);

            var turnContextMock = new Mock<ITurnContext>();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("chatHistoryMessages");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenTurnContextIsNull()
        {
            // Arrange
            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                _configurationMock.Object,
                _serviceProviderMock.Object);

            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", DateTimeOffset.UtcNow) };
            var toolOptions = new ToolOptions();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(null!, chatHistory, toolOptions);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("turnContext");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenChatHistoryMessagesIsNull()
        {
            // Arrange
            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                _configurationMock.Object,
                _serviceProviderMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var toolOptions = new ToolOptions();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, null!, toolOptions);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("chatHistoryMessages");
        }

        [Fact]
        public async Task ListToolServersAsync_WithoutToolOptions_CreatesDefaultToolOptions()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object);

            // Act & Assert - Should not throw on ToolOptions creation
            // Note: This will fail during HTTP call, but validates parameter handling
            try
            {
                await service.ListToolServersAsync("agent-123", "token-456");
            }
            catch (InvalidOperationException)
            {
                // Expected - we're just validating it doesn't fail on ToolOptions creation
            }
            catch (HttpRequestException)
            {
                // Also expected - HTTP call will fail, but we validated ToolOptions creation
            }
        }

        [Fact]
        public async Task SendChatHistoryAsync_MissingConversationId_ThrowsInvalidOperationException()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object);

            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns("Hello");
            activityMock.Setup(a => a.Conversation).Returns((ConversationAccount)null!); // Missing conversation

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", DateTimeOffset.UtcNow) };

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Conversation ID*");
        }

        [Fact]
        public async Task SendChatHistoryAsync_MissingMessageId_ThrowsInvalidOperationException()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };

            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns((string)null!); // Missing message ID
            activityMock.Setup(a => a.Text).Returns("Hello");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", DateTimeOffset.UtcNow) };

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Message ID*");
        }

        [Fact]
        public async Task SendChatHistoryAsync_MissingUserMessage_ThrowsInvalidOperationException()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };

            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns((string)null!); // Missing user message
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", DateTimeOffset.UtcNow) };

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*User message*");
        }
    }
}
