// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
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

        [Fact]
        public async Task SendChatHistoryAsync_MissingConversationId_ThrowsInvalidOperationException()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

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
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

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
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

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

        [Fact]
        public async Task SendChatHistoryAsync_SuccessfulRequest_CompletesWithoutException()
        {
            // Arrange
            var expectedEndpoint = "https://test.example.com/agents/real-time-threat-protection/chat-message";
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            // Setup mock HTTP message handler to return success response
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns("Hello, how are you?");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var chatHistory = new[] 
            { 
                new ChatHistoryMessage("1", "user", "Hi", timestamp),
                new ChatHistoryMessage("2", "assistant", "Hello!", timestamp.AddSeconds(1))
            };

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeTrue();
            result.Errors.Should().BeEmpty();

            // Verify HTTP request was made
            mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == expectedEndpoint),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendChatHistoryAsync_SuccessfulRequest_SerializesRequestCorrectly()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            string? capturedRequestBody = null;

            // Setup mock HTTP message handler to capture and return success response
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                {
                    capturedRequestBody = await req.Content!.ReadAsStringAsync();
                })
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationId = "conv-456";
            var messageId = "msg-789";
            var userMessage = "What is the weather?";
            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

            var conversationAccount = new ConversationAccount { Id = conversationId };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns(messageId);
            activityMock.Setup(a => a.Text).Returns(userMessage);
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var chatHistory = new[] 
            { 
                new ChatHistoryMessage("1", "user", "Hi", timestamp),
                new ChatHistoryMessage("2", "assistant", "Hello! How can I help?", timestamp.AddSeconds(1))
            };

            // Act
            await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            capturedRequestBody.Should().NotBeNullOrEmpty();
            
            var deserializedRequest = JsonSerializer.Deserialize<ChatMessageRequest>(capturedRequestBody!);
            deserializedRequest.Should().NotBeNull();
            deserializedRequest!.ConversationId.Should().Be(conversationId);
            deserializedRequest.MessageId.Should().Be(messageId);
            deserializedRequest.UserMessage.Should().Be(userMessage);
            deserializedRequest.ChatHistory.Should().HaveCount(2);
            deserializedRequest.ChatHistory[0].Id.Should().Be("1");
            deserializedRequest.ChatHistory[0].Role.Should().Be("user");
            deserializedRequest.ChatHistory[0].Content.Should().Be("Hi");
            deserializedRequest.ChatHistory[1].Id.Should().Be("2");
            deserializedRequest.ChatHistory[1].Role.Should().Be("assistant");
        }

        [Fact]
        public async Task SendChatHistoryAsync_SuccessfulRequest_UsesCorrectEndpoint()
        {
            // Arrange
            var customEndpoint = "https://custom.endpoint.com";
            var expectedFullEndpoint = $"{customEndpoint}/agents/real-time-threat-protection/chat-message";
            
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns(customEndpoint);

            Uri? capturedUri = null;

            // Setup mock HTTP message handler to capture request URI
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                {
                    capturedUri = req.RequestUri;
                })
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-999" };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-999");
            activityMock.Setup(a => a.Text).Returns("Test message");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Test", timestamp) };

            // Act
            await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            capturedUri.Should().NotBeNull();
            capturedUri!.ToString().Should().Be(expectedFullEndpoint);
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_SuccessfulRequest_CompletesWithoutException()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            // Setup mock HTTP message handler to return success response
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns("Hello with options");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", timestamp) };
            var toolOptions = new ToolOptions();

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory, toolOptions);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeTrue();
            result.Errors.Should().BeEmpty();

            // Verify HTTP request was made
            mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, "Bad Request", "Invalid request format")]
        [InlineData(HttpStatusCode.Unauthorized, "Unauthorized", "Unauthorized access")]
        [InlineData(HttpStatusCode.Forbidden, "Forbidden", "Access forbidden")]
        [InlineData(HttpStatusCode.NotFound, "Not Found", "Endpoint not found")]
        [InlineData(HttpStatusCode.InternalServerError, "Internal Server Error", "Internal server error")]
        public async Task SendChatHistoryAsync_NonSuccessStatusCode_LogsErrorAndReturnsFailureResult(
            HttpStatusCode statusCode, string expectedStatusText, string errorMessage)
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var errorResponseContent = $"{{\"error\": \"{errorMessage}\"}}";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(errorResponseContent, Encoding.UTF8, "application/json")
                });

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns("Test message");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", timestamp) };

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            var error = result.Errors.First();
            error.Message.Should().Contain(expectedStatusText); // EnsureSuccessStatusCode uses status text, not custom message
            error.Exception.Should().NotBeNull();
            error.Exception.Should().BeOfType<HttpRequestException>();
            var httpEx = error.Exception as HttpRequestException;
            httpEx!.StatusCode.Should().Be(statusCode);

            // Verify error was logged with correct status code
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedStatusText)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendChatHistoryAsync_HttpRequestException_ReturnsFailureResult()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns("Test message");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", timestamp) };

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            var error = result.Errors.First();
            error.Message.Should().Contain("Connection refused");
            error.Exception.Should().NotBeNull();
            error.Exception.Should().BeOfType<HttpRequestException>();

            // Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP error")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendChatHistoryAsync_TaskCanceledException_ReturnsFailureResult()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timed out"));

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns("Test message");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", timestamp) };

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            var error = result.Errors.First();
            error.Message.Should().Contain("Request timed out");
            error.Exception.Should().NotBeNull();
            error.Exception.Should().BeOfType<TaskCanceledException>();

            // Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timeout")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendChatHistoryAsync_GenericException_ReturnsFailureResult()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error occurred"));

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns("Test message");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", timestamp) };

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            var error = result.Errors.First();
            error.Message.Should().Contain("Unexpected error occurred");
            error.Exception.Should().NotBeNull();
            error.Exception.Should().BeOfType<InvalidOperationException>();

            // Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendChatHistoryAsync_HttpRequestExceptionWithStatusCode_ReturnsFailureResultWithStatusCode()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.example.com");

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Service unavailable", null, HttpStatusCode.ServiceUnavailable));

            using var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                httpClientFactoryMock.Object);

            var conversationAccount = new ConversationAccount { Id = "conv-123" };
            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns("msg-123");
            activityMock.Setup(a => a.Text).Returns("Test message");
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);

            var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var chatHistory = new[] { new ChatHistoryMessage("1", "user", "Hi", timestamp) };

            // Act
            var result = await service.SendChatHistoryAsync(turnContextMock.Object, chatHistory);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            var error = result.Errors.First();
            error.Exception.Should().NotBeNull();
            error.Exception.Should().BeOfType<HttpRequestException>();

            // Verify the HttpRequestException has the expected status code
            var httpEx = error.Exception as HttpRequestException;
            httpEx.Should().NotBeNull();
            httpEx!.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }
    }
}

