// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
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
using ModelContextProtocol.Client;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Tests;

/// <summary>
/// Unit tests for McpToolRegistrationService class.
/// </summary>
public class McpToolRegistrationServiceTests
{
    private readonly Mock<ILogger<IMcpToolRegistrationService>> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<McpToolEnumerationService> _mockEnumerationService;
    private readonly Mock<ITurnContext> _mockTurnContext;
    private readonly McpToolRegistrationService _service;
    private readonly string _testJwtToken;

    public McpToolRegistrationServiceTests()
    {
        _mockLogger = new Mock<ILogger<IMcpToolRegistrationService>>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Create a valid JWT token for testing
        _testJwtToken = CreateTestJwtToken("test-app-id");

        // Create mock for McpToolEnumerationService
        var mockEnumLogger = new Mock<ILogger<McpToolEnumerationService>>();
        var mockConfigService = new Mock<IMcpToolServerConfigurationService>();
        var mockConfiguration = new Mock<IConfiguration>();

        _mockEnumerationService = new Mock<McpToolEnumerationService>(
            mockEnumLogger.Object,
            mockConfigService.Object,
            mockConfiguration.Object);

        _mockTurnContext = new Mock<ITurnContext>();

        // Setup turn context with activity
        var mockActivity = new Mock<IActivity>();
        var recipient = new ChannelAccount { Id = "test-agent-id" };
        mockActivity.Setup(a => a.Recipient).Returns(recipient);
        _mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);

        _service = new McpToolRegistrationService(
            _mockLogger.Object,
            _mockServiceProvider.Object,
            _mockEnumerationService.Object);
    }

    #region Helper Methods

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
    /// Sets up the mock enumeration service for a standard test scenario with empty results.
    /// Note: McpClientTool is a sealed class and cannot be mocked with Moq. Since the SemanticKernel
    /// service calls AsKernelFunction() on tools, we cannot use placeholder/null tools.
    /// Empty tool lists still provide value by testing service orchestration, parameter passing,
    /// and proper handling of the no-tools scenario.
    /// </summary>
    private void SetupMocksForEmptyToolEnumeration(Action<ToolOptions>? captureToolOptions = null)
    {
        _mockEnumerationService
            .Setup(x => x.GetAuthTokenAsync(
                It.IsAny<UserAuthorization>(),
                It.IsAny<string>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<string?>()))
            .ReturnsAsync(_testJwtToken);

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

    #endregion

    [Fact]
    public async Task AddToolServersToAgentAsync_WithNullKernel_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => _service.AddToolServersToAgentAsync(
            kernel: null!,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: "token");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("kernel");
    }

    [Fact]
    public async Task AddToolServersToAgentAsync_CallsGetAuthTokenAsync()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        SetupMocksForEmptyToolEnumeration();

        // Act
        await _service.AddToolServersToAgentAsync(
            kernel: kernel,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: _testJwtToken);

        // Assert
        _mockEnumerationService.Verify(
            x => x.GetAuthTokenAsync(
                It.IsAny<UserAuthorization>(),
                "handler",
                _mockTurnContext.Object,
                _testJwtToken),
            Times.Once);
    }

    [Fact]
    public async Task AddToolServersToAgentAsync_CallsEnumerateToolsFromServersAsync()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        SetupMocksForEmptyToolEnumeration();

        // Act
        await _service.AddToolServersToAgentAsync(
            kernel: kernel,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: _testJwtToken);

        // Assert
        _mockEnumerationService.Verify(
            x => x.EnumerateToolsFromServersAsync(
                It.IsAny<string>(),
                _testJwtToken,
                _mockTurnContext.Object,
                It.Is<ToolOptions>(o => o.UserAgentConfiguration == Agent365SemanticKernelSdkUserAgentConfiguration.Instance)),
            Times.Once);
    }

    [Fact]
    public async Task AddToolServersToAgentAsync_WithNoServers_DoesNotAddPlugins()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        SetupMocksForEmptyToolEnumeration();

        // Act
        await _service.AddToolServersToAgentAsync(
            kernel: kernel,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: _testJwtToken);

        // Assert
        kernel.Plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task AddToolServersToAgentAsync_UsesCorrectUserAgentConfiguration()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        ToolOptions? capturedToolOptions = null;
        SetupMocksForEmptyToolEnumeration(options => capturedToolOptions = options);

        // Act
        await _service.AddToolServersToAgentAsync(
            kernel: kernel,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: _testJwtToken);

        // Assert
        capturedToolOptions.Should().NotBeNull();
        capturedToolOptions!.UserAgentConfiguration.Should().BeSameAs(Agent365SemanticKernelSdkUserAgentConfiguration.Instance);
    }
}
