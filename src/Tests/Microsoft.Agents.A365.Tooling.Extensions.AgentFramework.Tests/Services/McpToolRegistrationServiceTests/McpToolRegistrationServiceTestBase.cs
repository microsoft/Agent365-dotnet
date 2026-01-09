// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Moq;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests;

/// <summary>
/// Base class for McpToolRegistrationService tests, providing common test setup.
/// </summary>
public abstract class McpToolRegistrationServiceTestBase
{
    protected const string TestAuthToken = "test-token";
    protected const string TestAgentUserId = "user-id";

    protected readonly Mock<ILogger<IMcpToolRegistrationService>> LoggerMock;
    protected readonly Mock<IMcpToolServerConfigurationService> McpServerConfigurationServiceMock;
    protected readonly Mock<IMcpToolEnumerationService> McpToolEnumerationServiceMock;

    protected McpToolRegistrationServiceTestBase()
    {
        LoggerMock = new Mock<ILogger<IMcpToolRegistrationService>>();
        McpServerConfigurationServiceMock = new Mock<IMcpToolServerConfigurationService>();
        McpToolEnumerationServiceMock = new Mock<IMcpToolEnumerationService>();
    }

    /// <summary>
    /// Creates a new instance of McpToolRegistrationService with mocked dependencies.
    /// </summary>
    protected McpToolRegistrationService CreateService()
    {
        return new McpToolRegistrationService(
            LoggerMock.Object,
            McpServerConfigurationServiceMock.Object,
            McpToolEnumerationServiceMock.Object);
    }

    /// <summary>
    /// Creates a mock turn context with activity setup.
    /// </summary>
    protected static Mock<ITurnContext> CreateMockTurnContext()
    {
        var mockTurnContext = new Mock<ITurnContext>();
        var mockActivity = new Mock<IActivity>();
        var recipient = new ChannelAccount { Id = "test-agent-id" };
        mockActivity.Setup(a => a.Recipient).Returns(recipient);
        mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);
        return mockTurnContext;
    }

    /// <summary>
    /// Helper method to check if a string is a valid GUID.
    /// </summary>
    protected static bool IsValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }

    /// <summary>
    /// Sets up mocks for AddToolServersToAgent tests with empty tool enumeration results.
    /// Note: McpClientTool is a sealed class and cannot be mocked with Moq. Since the AgentFramework
    /// service casts tools to AITool, we cannot use placeholder/null tools.
    /// Empty tool lists still provide value by testing service orchestration, parameter passing,
    /// and proper handling of the no-tools scenario.
    /// </summary>
    protected void SetupMocksForAddToolServers(Action<ToolOptions>? captureToolOptions = null)
    {
        McpToolEnumerationServiceMock
            .Setup(x => x.GetAuthTokenAsync(
                It.IsAny<Microsoft.Agents.Builder.App.UserAuth.UserAuthorization>(),
                It.IsAny<string>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<string?>()))
            .ReturnsAsync(TestAuthToken);

        var setup = McpToolEnumerationServiceMock
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
    protected void SetupMocksForGetMcpTools(Action<ToolOptions>? captureToolOptions = null)
    {
        McpToolEnumerationServiceMock
            .Setup(x => x.GetAuthTokenAsync(
                It.IsAny<Microsoft.Agents.Builder.App.UserAuth.UserAuthorization>(),
                It.IsAny<string>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<string?>()))
            .ReturnsAsync(TestAuthToken);

        var setup = McpToolEnumerationServiceMock
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
}
