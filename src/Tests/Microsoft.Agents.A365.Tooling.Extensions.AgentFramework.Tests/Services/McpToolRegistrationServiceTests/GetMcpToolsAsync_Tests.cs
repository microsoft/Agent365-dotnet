// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests;

/// <summary>
/// Unit tests for McpToolRegistrationService.GetMcpToolsAsync method.
/// Tests parameter validation, authentication token handling, and tool enumeration.
/// </summary>
public class GetMcpToolsAsync_Tests : McpToolRegistrationServiceTestBase
{
    [Fact]
    public async Task CallsEnumerateToolsFromServersAsync()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        SetupMocksForGetMcpTools();
        var service = CreateService();

        // Act
        await service.GetMcpToolsAsync(
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        McpServerConfigurationServiceMock.Verify(
            x => x.EnumerateToolsFromServersAsync(
                TestAgentUserId,
                TestAuthToken,
                It.IsAny<IMcpTokenProvider>(),
                mockTurnContext.Object,
                It.Is<ToolOptions>(o => o.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReturnsEmptyListWhenNoTools()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        SetupMocksForGetMcpTools();
        var service = CreateService();

        // Act
        var result = await service.GetMcpToolsAsync(
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UsesCorrectUserAgentConfiguration()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        ToolOptions? capturedToolOptions = null;
        SetupMocksForGetMcpTools(options => capturedToolOptions = options);
        var service = CreateService();

        // Act
        await service.GetMcpToolsAsync(
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        capturedToolOptions.Should().NotBeNull();
        capturedToolOptions!.UserAgentConfiguration.Should().BeSameAs(Agent365AgentFrameworkSdkUserAgentConfiguration.Instance);
    }
}
