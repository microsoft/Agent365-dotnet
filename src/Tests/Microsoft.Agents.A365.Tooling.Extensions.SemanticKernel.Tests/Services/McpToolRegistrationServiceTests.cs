// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Tests.Services
{
    /// <summary>
    /// Unit tests for McpToolRegistrationService class.
    /// Tests parameter validation, chat history conversion, tool registration, and delegation to underlying services.
    /// </summary>
    public class McpToolRegistrationServiceTests
    {
        private readonly Mock<ILogger<IMcpToolRegistrationService>> _loggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IMcpToolServerConfigurationService> _mcpServerConfigurationServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly string _testJwtToken;

        public McpToolRegistrationServiceTests()
        {
            _loggerMock = new Mock<ILogger<IMcpToolRegistrationService>>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _mcpServerConfigurationServiceMock = new Mock<IMcpToolServerConfigurationService>();
            _configurationMock = new Mock<IConfiguration>();

            // Create a valid JWT token for testing
            _testJwtToken = CreateTestJwtToken("test-app-id");
        }

        #region Helper Methods

        /// <summary>
        /// Creates a new instance of McpToolRegistrationService with all mocked dependencies.
        /// </summary>
        private McpToolRegistrationService CreateService()
        {
            return new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);
        }

        /// <summary>
        /// Creates a valid JWT token with an appid claim for testing purposes.
        /// </summary>
        private static string CreateTestJwtToken(string appId)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-at-least-32-bytes-long"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("appid", appId),
                new Claim("azp", appId)
            };

            var token = new JwtSecurityToken(
                issuer: "test-issuer",
                audience: "test-audience",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Creates a mock turn context with activity setup.
        /// </summary>
        private static Mock<ITurnContext> CreateMockTurnContext()
        {
            var mockTurnContext = new Mock<ITurnContext>();
            var mockActivity = new Mock<IActivity>();
            var recipient = new ChannelAccount { Id = "test-agent-id" };
            mockActivity.Setup(a => a.Recipient).Returns(recipient);
            mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);
            return mockTurnContext;
        }

        /// <summary>
        /// Sets up the mock enumeration service for a standard test scenario with empty results.
        /// Note: McpClientTool is a sealed class and cannot be mocked with Moq. Since the SemanticKernel
        /// service calls AsKernelFunction() on tools, we cannot use placeholder/null tools.
        /// Empty tool lists still provide value by testing service orchestration, parameter passing,
        /// and proper handling of the no-tools scenario.
        /// </summary>
        private void SetupMocksForEmptyToolEnumeration(Action<ToolOptions>? captureToolOptions = null)
        {
            var setup = _mcpServerConfigurationServiceMock
                .Setup(x => x.EnumerateToolsFromServersAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()));

            if (captureToolOptions != null)
            {
                setup.Callback<string, string, ITurnContext, ToolOptions, CancellationToken>((_, _, _, options, _) => captureToolOptions(options));
            }

            setup.ReturnsAsync((new List<MCPServerConfig>(), new Dictionary<string, IList<McpClientTool>>()));
        }

        #endregion

        #region AddToolServersToAgentAsync Tests

        [Fact]
        public async Task AddToolServersToAgentAsync_WithNullKernel_ThrowsArgumentNullException()
        {
            // Arrange
            var service = CreateService();
            var mockTurnContext = CreateMockTurnContext();

            // Act
            var act = () => service.AddToolServersToAgentAsync(
                kernel: null!,
                userAuthorization: null!,
                authHandlerName: "handler",
                turnContext: mockTurnContext.Object,
                authToken: _testJwtToken);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("kernel");
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_CallsEnumerateToolsFromServersAsync()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            var mockTurnContext = CreateMockTurnContext();
            SetupMocksForEmptyToolEnumeration();
            var service = CreateService();

            // Act
            await service.AddToolServersToAgentAsync(
                kernel: kernel,
                userAuthorization: null!,
                authHandlerName: "handler",
                turnContext: mockTurnContext.Object,
                authToken: _testJwtToken);

            // Assert
            _mcpServerConfigurationServiceMock.Verify(
                x => x.EnumerateToolsFromServersAsync(
                    It.IsAny<string>(),
                    _testJwtToken,
                    mockTurnContext.Object,
                    It.Is<ToolOptions>(o => o.UserAgentConfiguration == Agent365SemanticKernelSdkUserAgentConfiguration.Instance),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_WithNoServers_DoesNotAddPlugins()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            var mockTurnContext = CreateMockTurnContext();
            SetupMocksForEmptyToolEnumeration();
            var service = CreateService();

            // Act
            await service.AddToolServersToAgentAsync(
                kernel: kernel,
                userAuthorization: null!,
                authHandlerName: "handler",
                turnContext: mockTurnContext.Object,
                authToken: _testJwtToken);

            // Assert
            kernel.Plugins.Should().BeEmpty();
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_UsesCorrectUserAgentConfiguration()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            var mockTurnContext = CreateMockTurnContext();
            ToolOptions? capturedToolOptions = null;
            SetupMocksForEmptyToolEnumeration(options => capturedToolOptions = options);
            var service = CreateService();

            // Act
            await service.AddToolServersToAgentAsync(
                kernel: kernel,
                userAuthorization: null!,
                authHandlerName: "handler",
                turnContext: mockTurnContext.Object,
                authToken: _testJwtToken);

            // Assert
            capturedToolOptions.Should().NotBeNull();
            capturedToolOptions!.UserAgentConfiguration.Should().BeSameAs(Agent365SemanticKernelSdkUserAgentConfiguration.Instance);
        }

        #endregion

        #region SendChatHistoryAsync Tests

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenTurnContextIsNull()
        {
            // Arrange
            var service = CreateService();
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
            var service = CreateService();
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
            var service = CreateService();
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
            var service = CreateService();
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
            var service = CreateService();
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
            var service = CreateService();
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
            var service = CreateService();
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

            var service = CreateService();
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

            var service = CreateService();
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

            var service = CreateService();
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

            var service = CreateService();
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

            var service = CreateService();
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

            var service = CreateService();
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

            var service = CreateService();
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

            var service = CreateService();
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

        #endregion
    }
}
