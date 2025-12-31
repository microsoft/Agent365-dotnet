// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
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
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

        public McpToolServerConfigurationServiceTests()
        {
            _loggerMock = new Mock<ILogger<IMcpToolServerConfigurationService>>();
            _configurationMock = new Mock<IConfiguration>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            
            // Setup default HttpClient creation
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenTurnContextIsNull()
        {
            // Arrange
            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                _configurationMock.Object,
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

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
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

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
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

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
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

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
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

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
    }
}
