// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests;

/// <summary>
/// Unit tests for McpToolRegistrationService.AddToolServersToAgent method.
/// Tests parameter validation, authentication token handling, and tool enumeration.
/// </summary>
public class AddToolServersToAgent_Tests : McpToolRegistrationServiceTestBase
{
    [Fact]
    public async Task WithNullChatClient_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var mockTurnContext = CreateMockTurnContext();

        // Act
        var act = () => service.AddToolServersToAgent(
            chatClient: null!,
            agentInstructions: "instructions",
            initialTools: new List<AITool>(),
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("chatClient");
    }

    [Fact]
    public async Task CallsGetAuthTokenAsync()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mockTurnContext = CreateMockTurnContext();
        SetupMocksForAddToolServers();
        var service = CreateService();

        // Act
        await service.AddToolServersToAgent(
            chatClient: mockChatClient.Object,
            agentInstructions: "instructions",
            initialTools: new List<AITool>(),
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        McpToolEnumerationServiceMock.Verify(
            x => x.GetAuthTokenAsync(
                It.IsAny<UserAuthorization>(),
                "handler",
                mockTurnContext.Object,
                TestAuthToken),
            Times.Once);
    }

    [Fact]
    public async Task CallsEnumerateToolsFromServersAsync()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mockTurnContext = CreateMockTurnContext();
        SetupMocksForAddToolServers();
        var service = CreateService();

        // Act
        await service.AddToolServersToAgent(
            chatClient: mockChatClient.Object,
            agentInstructions: "instructions",
            initialTools: new List<AITool>(),
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: mockTurnContext.Object,
            authToken: TestAuthToken);

        // Assert
        McpToolEnumerationServiceMock.Verify(
            x => x.EnumerateToolsFromServersAsync(
                TestAgentUserId,
                TestAuthToken,
                mockTurnContext.Object,
                It.Is<ToolOptions>(o => o.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance)),
            Times.Once);
    }

    [Fact]
    public async Task UsesCorrectUserAgentConfiguration()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mockTurnContext = CreateMockTurnContext();
        ToolOptions? capturedToolOptions = null;
        SetupMocksForAddToolServers(options => capturedToolOptions = options);
        var service = CreateService();

        // Act
        await service.AddToolServersToAgent(
            chatClient: mockChatClient.Object,
            agentInstructions: "instructions",
            initialTools: new List<AITool>(),
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
