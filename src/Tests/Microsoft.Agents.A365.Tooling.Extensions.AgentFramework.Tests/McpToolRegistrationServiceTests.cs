// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests;

/// <summary>
/// Unit tests for McpToolRegistrationService class.
/// </summary>
public class McpToolRegistrationServiceTests
{
    private readonly Mock<ILogger<IMcpToolRegistrationService>> _mockLogger;
    private readonly Mock<McpToolEnumerationService> _mockEnumerationService;
    private readonly Mock<ITurnContext> _mockTurnContext;
    private readonly McpToolRegistrationService _service;

    public McpToolRegistrationServiceTests()
    {
        _mockLogger = new Mock<ILogger<IMcpToolRegistrationService>>();

        // Create mock for McpToolEnumerationService
        var mockEnumLogger = new Mock<ILogger<McpToolEnumerationService>>();
        var mockConfigService = new Mock<IMcpToolServerConfigurationService>();
        var mockConfiguration = new Mock<IConfiguration>();

        _mockEnumerationService = new Mock<McpToolEnumerationService>(
            mockEnumLogger.Object,
            mockConfigService.Object,
            mockConfiguration.Object);

        _mockTurnContext = new Mock<ITurnContext>();

        _service = new McpToolRegistrationService(
            _mockLogger.Object,
            _mockEnumerationService.Object);
    }

    #region Helper Methods

    private const string TestAuthToken = "test-token";
    private const string TestAgentUserId = "user-id";

    /// <summary>
    /// Sets up mocks for AddToolServersToAgent tests with empty tool enumeration results.
    /// Note: McpClientTool is a sealed class and cannot be mocked with Moq. Since the AgentFramework
    /// service casts tools to AITool, we cannot use placeholder/null tools.
    /// Empty tool lists still provide value by testing service orchestration, parameter passing,
    /// and proper handling of the no-tools scenario.
    /// </summary>
    private void SetupMocksForAddToolServers(Action<ToolOptions>? captureToolOptions = null)
    {
        _mockEnumerationService
            .Setup(x => x.GetAuthTokenAsync(
                It.IsAny<UserAuthorization>(),
                It.IsAny<string>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<string?>()))
            .ReturnsAsync(TestAuthToken);

        var setup = _mockEnumerationService
            .Setup(x => x.EnumerateToolsFromServersAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<ToolOptions>()));

        if (captureToolOptions != null)
        {
            setup.Callback<string, string, ITurnContext, ToolOptions>((_, _, _, options) => captureToolOptions(options));
        }

        setup.ReturnsAsync((new List<MCPServerConfig>(), new Dictionary<string, IList<McpClientTool>>()));
    }

    /// <summary>
    /// Sets up mocks for GetMcpToolsAsync tests with empty tool enumeration results.
    /// Note: McpClientTool is a sealed class and cannot be mocked with Moq. Since the AgentFramework
    /// service casts tools to AITool, we cannot use placeholder/null tools.
    /// Empty tool lists still provide value by testing service orchestration, parameter passing,
    /// and proper handling of the no-tools scenario.
    /// </summary>
    private void SetupMocksForGetMcpTools(Action<ToolOptions>? captureToolOptions = null)
    {
        _mockEnumerationService
            .Setup(x => x.GetAuthTokenAsync(
                It.IsAny<UserAuthorization>(),
                It.IsAny<string>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<string?>()))
            .ReturnsAsync(TestAuthToken);

        var setup = _mockEnumerationService
            .Setup(x => x.EnumerateAllToolsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<ToolOptions>()));

        if (captureToolOptions != null)
        {
            setup.Callback<string, string, ITurnContext, ToolOptions>((_, _, _, options) => captureToolOptions(options));
        }

        setup.ReturnsAsync(new List<McpClientTool>());
    }

    #endregion

    #region AddToolServersToAgent Tests

    [Fact]
    public async Task AddToolServersToAgent_WithNullChatClient_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => _service.AddToolServersToAgent(
            chatClient: null!,
            agentInstructions: "instructions",
            initialTools: new List<AITool>(),
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: "token");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("chatClient");
    }

    [Fact]
    public async Task AddToolServersToAgent_CallsGetAuthTokenAsync()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        SetupMocksForAddToolServers();

        // Act
        await _service.AddToolServersToAgent(
            chatClient: mockChatClient.Object,
            agentInstructions: "instructions",
            initialTools: new List<AITool>(),
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        _mockEnumerationService.Verify(
            x => x.GetAuthTokenAsync(
                It.IsAny<UserAuthorization>(),
                "handler",
                _mockTurnContext.Object,
                TestAuthToken),
            Times.Once);
    }

    [Fact]
    public async Task AddToolServersToAgent_CallsEnumerateToolsFromServersAsync()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        SetupMocksForAddToolServers();

        // Act
        await _service.AddToolServersToAgent(
            chatClient: mockChatClient.Object,
            agentInstructions: "instructions",
            initialTools: new List<AITool>(),
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        _mockEnumerationService.Verify(
            x => x.EnumerateToolsFromServersAsync(
                TestAgentUserId,
                TestAuthToken,
                _mockTurnContext.Object,
                It.Is<ToolOptions>(o => o.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance)),
            Times.Once);
    }

    [Fact]
    public async Task AddToolServersToAgent_UsesCorrectUserAgentConfiguration()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        ToolOptions? capturedToolOptions = null;
        SetupMocksForAddToolServers(options => capturedToolOptions = options);

        // Act
        await _service.AddToolServersToAgent(
            chatClient: mockChatClient.Object,
            agentInstructions: "instructions",
            initialTools: new List<AITool>(),
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        capturedToolOptions.Should().NotBeNull();
        capturedToolOptions!.UserAgentConfiguration.Should().BeSameAs(Agent365AgentFrameworkSdkUserAgentConfiguration.Instance);
    }

    #endregion

    #region GetMcpToolsAsync Tests

    [Fact]
    public async Task GetMcpToolsAsync_CallsGetAuthTokenAsync()
    {
        // Arrange
        SetupMocksForGetMcpTools();

        // Act
        await _service.GetMcpToolsAsync(
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        _mockEnumerationService.Verify(
            x => x.GetAuthTokenAsync(
                It.IsAny<UserAuthorization>(),
                "handler",
                _mockTurnContext.Object,
                TestAuthToken),
            Times.Once);
    }

    [Fact]
    public async Task GetMcpToolsAsync_CallsEnumerateAllToolsAsync()
    {
        // Arrange
        SetupMocksForGetMcpTools();

        // Act
        await _service.GetMcpToolsAsync(
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        _mockEnumerationService.Verify(
            x => x.EnumerateAllToolsAsync(
                TestAgentUserId,
                TestAuthToken,
                _mockTurnContext.Object,
                It.Is<ToolOptions>(o => o.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance)),
            Times.Once);
    }

    [Fact]
    public async Task GetMcpToolsAsync_ReturnsEmptyListWhenNoTools()
    {
        // Arrange
        SetupMocksForGetMcpTools();

        // Act
        var result = await _service.GetMcpToolsAsync(
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMcpToolsAsync_UsesCorrectUserAgentConfiguration()
    {
        // Arrange
        ToolOptions? capturedToolOptions = null;
        SetupMocksForGetMcpTools(options => capturedToolOptions = options);

        // Act
        await _service.GetMcpToolsAsync(
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        capturedToolOptions.Should().NotBeNull();
        capturedToolOptions!.UserAgentConfiguration.Should().BeSameAs(Agent365AgentFrameworkSdkUserAgentConfiguration.Instance);
    }

    #endregion
}
