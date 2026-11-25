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
using Moq.Language;
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
        private readonly List<DisposalProbe> _clientsPerCall = new();

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
                .Setup(x => x.GetMcpToolSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IMcpTokenProvider>(),
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()));

            if (captureToolOptions != null)
            {
                setup.Callback<string, string, IMcpTokenProvider, ITurnContext, ToolOptions, CancellationToken>((_, _, _, _, options, _) => captureToolOptions(options));
            }

            setup.ReturnsAsync(() => CreateToolSet());
        }

        /// <summary>
        /// Sets up the mock enumeration service to return the given MCP server names, one array per call.
        /// Each server has an empty tool list, so the service adds one empty plugin per server name.
        /// The MCP clients of each call are represented by a <see cref="DisposalProbe"/> in <see cref="_clientsPerCall"/>.
        /// Returns the sequence so a test can add later results, such as a failure.
        /// </summary>
        private ISetupSequentialResult<Task<McpToolSet>> SetupToolEnumerationSequence(params string[][] serverNamesPerCall)
        {
            var sequence = _mcpServerConfigurationServiceMock
                .SetupSequence(x => x.GetMcpToolSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IMcpTokenProvider>(),
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()));

            foreach (var toolSet in serverNamesPerCall.Select(CreateToolSet))
            {
                sequence = sequence.ReturnsAsync(toolSet);
            }

            return sequence;
        }

        /// <summary>
        /// Creates a tool set with an empty tool list for each server name, owning a new <see cref="DisposalProbe"/>.
        /// </summary>
        private McpToolSet CreateToolSet(params string[] serverNames)
        {
            var clients = new DisposalProbe();
            _clientsPerCall.Add(clients);
            return new McpToolSet(
                Array.Empty<MCPServerConfig>(),
                serverNames.ToDictionary(name => name, _ => (IReadOnlyList<McpClientTool>)Array.Empty<McpClientTool>()),
                new[] { clients });
        }

        /// <summary>
        /// Calls AddToolServersToAgentAsync on the given kernel with the standard test arguments.
        /// </summary>
        private Task AddToolServersAsync(McpToolRegistrationService service, Kernel kernel)
        {
            return service.AddToolServersToAgentAsync(
                kernel: kernel,
                userAuthorization: null!,
                authHandlerName: "handler",
                turnContext: CreateMockTurnContext().Object,
                authToken: _testJwtToken);
        }

        /// <summary>
        /// Stands in for the MCP clients of a tool set and counts how often they are disposed.
        /// </summary>
        private sealed class DisposalProbe : IAsyncDisposable
        {
            public int DisposeCount { get; private set; }

            public ValueTask DisposeAsync()
            {
                DisposeCount++;
                return ValueTask.CompletedTask;
            }
        }

        /// <summary>
        /// An implementation that only forwards AddToolServersToAgentAsync, so RemoveToolServersAsync uses the interface default.
        /// </summary>
        private sealed class ForwardingRegistrationService(IMcpToolRegistrationService inner) : IMcpToolRegistrationService
        {
            public Task AddToolServersToAgentAsync(Kernel kernel, UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? authToken = null)
                => inner.AddToolServersToAgentAsync(kernel, userAuthorization, authHandlerName, turnContext, authToken);

            public Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistory chatHistory, CancellationToken cancellationToken = default)
                => inner.SendChatHistoryAsync(turnContext, chatHistory, cancellationToken);

            public Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistory chatHistory, ToolOptions toolOptions, CancellationToken cancellationToken = default)
                => inner.SendChatHistoryAsync(turnContext, chatHistory, toolOptions, cancellationToken);
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
        public async Task AddToolServersToAgentAsync_CallsGetMcpToolSetAsync()
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
                x => x.GetMcpToolSetAsync(
                    It.IsAny<string>(),
                    _testJwtToken,
                    It.IsAny<IMcpTokenProvider>(),
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

        [Fact]
        public async Task AddToolServersToAgentAsync_ReusedKernelAndNoServers_RemovesPluginsFromEarlierCall()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" }, Array.Empty<string>());
            var service = CreateService();
            await AddToolServersAsync(service, kernel);
            kernel.Plugins.Select(p => p.Name).Should().Equal("mcp_CalendarTools");

            // Act
            await AddToolServersAsync(service, kernel);

            // Assert
            kernel.Plugins.Should().BeEmpty();
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_ReusedKernelAndDifferentServers_KeepsOnlyPluginsFromCurrentCall()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" }, new[] { "mcp_MailTools" });
            var service = CreateService();
            await AddToolServersAsync(service, kernel);

            // Act
            await AddToolServersAsync(service, kernel);

            // Assert
            kernel.Plugins.Select(p => p.Name).Should().Equal("mcp_MailTools");
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_SameServerReturnedTwice_ReplacesPluginFromEarlierCall()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" }, new[] { "mcp_CalendarTools" });
            var service = CreateService();
            await AddToolServersAsync(service, kernel);
            var pluginFromEarlierCall = kernel.Plugins["mcp_CalendarTools"];

            // Act
            var act = () => AddToolServersAsync(service, kernel);

            // Assert
            await act.Should().NotThrowAsync();
            kernel.Plugins.Should().ContainSingle();
            kernel.Plugins["mcp_CalendarTools"].Should().NotBeSameAs(pluginFromEarlierCall);
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_ClonedKernel_RemovesPluginsFromEarlierCall()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" }, new[] { "mcp_MailTools" });
            var service = CreateService();
            await AddToolServersAsync(service, kernel);
            var clonedKernel = kernel.Clone();

            // Act
            await AddToolServersAsync(service, clonedKernel);

            // Assert
            clonedKernel.Plugins.Select(p => p.Name).Should().Equal("mcp_MailTools");
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_ReusedKernel_KeepsCallerAddedPlugins()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            var callerPlugin = kernel.Plugins.AddFromFunctions(
                "CallerPlugin",
                new[] { KernelFunctionFactory.CreateFromMethod(() => "pong", "Ping") });
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" }, Array.Empty<string>());
            var service = CreateService();
            await AddToolServersAsync(service, kernel);
            kernel.Plugins.Select(p => p.Name).Should().BeEquivalentTo("CallerPlugin", "mcp_CalendarTools");

            // Act
            await AddToolServersAsync(service, kernel);

            // Assert
            kernel.Plugins.Should().Contain(callerPlugin);
        }

        [Theory]
        [InlineData("identity resolution")]
        [InlineData("discovery")]
        public async Task AddToolServersToAgentAsync_ReusedKernelAndFailingCall_RemovesPluginsFromEarlierCall(string failingStep)
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            var callerPlugin = kernel.Plugins.AddFromFunctions(
                "CallerPlugin",
                new[] { KernelFunctionFactory.CreateFromMethod(() => "pong", "Ping") });
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" })
                .ThrowsAsync(new InvalidOperationException("discovery failed"));
            var service = CreateService();
            await AddToolServersAsync(service, kernel);
            kernel.Plugins.Select(p => p.Name).Should().BeEquivalentTo("CallerPlugin", "mcp_CalendarTools");

            // Act: a malformed token fails agent identity resolution; otherwise the second enumeration fails.
            var act = () => service.AddToolServersToAgentAsync(
                kernel: kernel,
                userAuthorization: null!,
                authHandlerName: "handler",
                turnContext: CreateMockTurnContext().Object,
                authToken: failingStep == "discovery" ? _testJwtToken : "not-a-jwt");

            // Assert
            await act.Should().ThrowAsync<Exception>();
            kernel.Plugins.Should().ContainSingle().Which.Should().BeSameAs(callerPlugin);
            _clientsPerCall[0].DisposeCount.Should().Be(1);
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_ReusedKernel_DisposesClientsOfEarlierCall()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" }, new[] { "mcp_CalendarTools" });
            var service = CreateService();
            await AddToolServersAsync(service, kernel);

            // Act
            await AddToolServersAsync(service, kernel);

            // Assert
            _clientsPerCall[0].DisposeCount.Should().Be(1);
            _clientsPerCall[1].DisposeCount.Should().Be(0);
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_ClonedKernel_DoesNotDisposeClientsOfOriginalKernel()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" }, new[] { "mcp_MailTools" });
            var service = CreateService();
            await AddToolServersAsync(service, kernel);
            var originalPlugin = kernel.Plugins["mcp_CalendarTools"];
            var clonedKernel = kernel.Clone();

            // Act
            await AddToolServersAsync(service, clonedKernel);

            // Assert
            _clientsPerCall[0].DisposeCount.Should().Be(0);
            kernel.Plugins.Should().ContainSingle().Which.Should().BeSameAs(originalPlugin);
        }

        [Fact]
        public async Task RemoveToolServersAsync_RemovesMcpPluginsAndDisposesClients_KeepsCallerPlugins()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            var callerPlugin = kernel.Plugins.AddFromFunctions(
                "CallerPlugin",
                new[] { KernelFunctionFactory.CreateFromMethod(() => "pong", "Ping") });
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools", "mcp_MailTools" });
            var service = CreateService();
            await AddToolServersAsync(service, kernel);

            // Act
            await service.RemoveToolServersAsync(kernel);
            await service.RemoveToolServersAsync(kernel);

            // Assert
            kernel.Plugins.Should().ContainSingle().Which.Should().BeSameAs(callerPlugin);
            _clientsPerCall[0].DisposeCount.Should().Be(1);
        }

        [Fact]
        public async Task RemoveToolServersAsync_ClonedKernel_DisposesOnlyClientsAddedToTheClone()
        {
            // Arrange: the base kernel has MCP plugins, and a per-message copy registers its own.
            var baseKernel = Kernel.CreateBuilder().Build();
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" }, new[] { "mcp_MailTools" });
            var service = CreateService();
            await AddToolServersAsync(service, baseKernel);
            var turnKernel = baseKernel.Clone();
            await AddToolServersAsync(service, turnKernel);

            // Act
            await service.RemoveToolServersAsync(turnKernel);

            // Assert
            turnKernel.Plugins.Should().BeEmpty();
            _clientsPerCall[1].DisposeCount.Should().Be(1);
            _clientsPerCall[0].DisposeCount.Should().Be(0);
            baseKernel.Plugins.Select(p => p.Name).Should().Equal("mcp_CalendarTools");
        }

        [Fact]
        public async Task RemoveToolServersAsync_InterfaceDefault_RemovesMcpPluginsAndDisposesClients()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools" });
            IMcpToolRegistrationService service = new ForwardingRegistrationService(CreateService());
            await service.AddToolServersToAgentAsync(kernel, null!, "handler", CreateMockTurnContext().Object, _testJwtToken);

            // Act
            await service.RemoveToolServersAsync(kernel);

            // Assert
            kernel.Plugins.Should().BeEmpty();
            _clientsPerCall[0].DisposeCount.Should().Be(1);
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_CallInProgressForSameKernel_ThrowsInvalidOperationException()
        {
            // Arrange: the first call waits for discovery.
            var kernel = Kernel.CreateBuilder().Build();
            var discovery = new TaskCompletionSource<McpToolSet>();
            _mcpServerConfigurationServiceMock
                .SetupSequence(x => x.GetMcpToolSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IMcpTokenProvider>(),
                    It.IsAny<ITurnContext>(),
                    It.IsAny<ToolOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(discovery.Task)
                .ReturnsAsync(CreateToolSet("mcp_MailTools"))
                .ReturnsAsync(CreateToolSet("mcp_CalendarTools"));
            var service = CreateService();
            var firstCall = AddToolServersAsync(service, kernel);

            // Act
            var secondCall = () => AddToolServersAsync(service, kernel);
            var removeCall = () => service.RemoveToolServersAsync(kernel);

            // Assert
            await secondCall.Should().ThrowAsync<InvalidOperationException>();
            await removeCall.Should().ThrowAsync<InvalidOperationException>();
            var clonedKernel = kernel.Clone();
            await AddToolServersAsync(service, clonedKernel);
            clonedKernel.Plugins.Select(p => p.Name).Should().Equal("mcp_MailTools");

            discovery.SetResult(CreateToolSet("mcp_TeamsTools"));
            await firstCall;
            kernel.Plugins.Select(p => p.Name).Should().Equal("mcp_TeamsTools");
            await AddToolServersAsync(service, kernel);
            kernel.Plugins.Select(p => p.Name).Should().Equal("mcp_CalendarTools");
        }

        [Fact]
        public async Task AddToolServersToAgentAsync_PluginNameTakenByCallerPlugin_RemovesPluginsOfThisCallAndDisposesClients()
        {
            // Arrange
            var kernel = Kernel.CreateBuilder().Build();
            var callerPlugin = kernel.Plugins.AddFromFunctions(
                "mcp_MailTools",
                new[] { KernelFunctionFactory.CreateFromMethod(() => "pong", "Ping") });
            SetupToolEnumerationSequence(new[] { "mcp_CalendarTools", "mcp_MailTools" });
            var service = CreateService();

            // Act
            var act = () => AddToolServersAsync(service, kernel);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>();
            kernel.Plugins.Should().ContainSingle().Which.Should().BeSameAs(callerPlugin);
            _clientsPerCall[0].DisposeCount.Should().Be(1);
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
