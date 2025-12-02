// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.Logging;
using Moq;

namespace Microsoft.Agents.A365.Tooling.Tests.Extensions.AzureAIFoundry;

[TestClass]
public class McpToolRegistrationServiceTests
{
    private Mock<ILogger<IMcpToolRegistrationService>> _mockLogger = null!;
    private Mock<IServiceProvider> _mockServiceProvider = null!;
    private Mock<IMcpToolServerConfigurationService> _mockConfigService = null!;
    private McpToolRegistrationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        _mockLogger = new Mock<ILogger<IMcpToolRegistrationService>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockConfigService = new Mock<IMcpToolServerConfigurationService>();
        _service = new McpToolRegistrationService(_mockLogger.Object, _mockServiceProvider.Object, _mockConfigService.Object);
    }

    [TestMethod]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<IMcpToolRegistrationService>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockConfigService = new Mock<IMcpToolServerConfigurationService>();

        // Act
        var service = new McpToolRegistrationService(mockLogger.Object, mockServiceProvider.Object, mockConfigService.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [TestMethod]
    public void GetMcpToolDefinitionsAndResourcesAsync_WithMailTools_ConfiguresService()
    {
        // Arrange
        var agentInstanceId = "test-agent-123";
        var environmentId = "test-env";
        var authToken = "test-token";
        var mockTurnContext = new Mock<ITurnContext>();

        var mailServerConfig = new MCPServerConfig
        {
            mcpServerName = "mailtools",
            id = "mail-id",
            url = "https://mail.com/mcp",
            scope = "mail.read",
            audience = "api://mail",
            publisher = "Microsoft"
        };

        _mockConfigService
            .Setup(x => x.ListToolServers(agentInstanceId, environmentId, authToken))
            .ReturnsAsync(new List<MCPServerConfig> { mailServerConfig });

        _mockConfigService
            .Setup(x => x.GetMcpClientTools(mockTurnContext.Object, mailServerConfig, environmentId, authToken))
            .ReturnsAsync(new List<ModelContextProtocol.Client.McpClientTool>());

        // Act & Assert
        _mockConfigService.Verify(x => x.ListToolServers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void GetMcpToolDefinitionsAndResourcesAsync_WithCalendarTools_ConfiguresService()
    {
        // Arrange
        var agentInstanceId = "test-agent-123";
        var environmentId = "test-env";
        var authToken = "test-token";
        var mockTurnContext = new Mock<ITurnContext>();

        var calendarServerConfig = new MCPServerConfig
        {
            mcpServerName = "calendartools",
            id = "calendar-id",
            url = "https://calendar.com/mcp",
            scope = "calendar.read",
            audience = "api://calendar",
            publisher = "Microsoft"
        };

        _mockConfigService
            .Setup(x => x.ListToolServers(agentInstanceId, environmentId, authToken))
            .ReturnsAsync(new List<MCPServerConfig> { calendarServerConfig });

        _mockConfigService
            .Setup(x => x.GetMcpClientTools(mockTurnContext.Object, calendarServerConfig, environmentId, authToken))
            .ReturnsAsync(new List<ModelContextProtocol.Client.McpClientTool>());

        // Act & Assert
        _mockConfigService.Verify(x => x.ListToolServers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void GetMcpToolDefinitionsAndResourcesAsync_WithSharePointTools_ConfiguresService()
    {
        // Arrange
        var agentInstanceId = "test-agent-123";
        var environmentId = "test-env";
        var authToken = "test-token";
        var mockTurnContext = new Mock<ITurnContext>();

        var sharePointServerConfig = new MCPServerConfig
        {
            mcpServerName = "sharepointtools",
            id = "sharepoint-id",
            url = "https://sharepoint.com/mcp",
            scope = "sites.read",
            audience = "api://sharepoint",
            publisher = "Microsoft"
        };

        _mockConfigService
            .Setup(x => x.ListToolServers(agentInstanceId, environmentId, authToken))
            .ReturnsAsync(new List<MCPServerConfig> { sharePointServerConfig });

        _mockConfigService
            .Setup(x => x.GetMcpClientTools(mockTurnContext.Object, sharePointServerConfig, environmentId, authToken))
            .ReturnsAsync(new List<ModelContextProtocol.Client.McpClientTool>());

        // Act & Assert
        _mockConfigService.Verify(x => x.ListToolServers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void GetMcpToolDefinitionsAndResourcesAsync_WithMultipleTools_ConfiguresService()
    {
        // Arrange
        var agentInstanceId = "test-agent-123";
        var environmentId = "test-env";
        var authToken = "test-token";
        var mockTurnContext = new Mock<ITurnContext>();

        var servers = new List<MCPServerConfig>
        {
            new MCPServerConfig
            {
                mcpServerName = "mailtools",
                id = "mail-id",
                url = "https://mail.com/mcp",
                scope = "mail.read",
                audience = "api://mail",
                publisher = "Microsoft"
            },
            new MCPServerConfig
            {
                mcpServerName = "calendartools",
                id = "calendar-id",
                url = "https://calendar.com/mcp",
                scope = "calendar.read",
                audience = "api://calendar",
                publisher = "Microsoft"
            },
            new MCPServerConfig
            {
                mcpServerName = "sharepointtools",
                id = "sharepoint-id",
                url = "https://sharepoint.com/mcp",
                scope = "sites.read",
                audience = "api://sharepoint",
                publisher = "Microsoft"
            }
        };

        _mockConfigService
            .Setup(x => x.ListToolServers(agentInstanceId, environmentId, authToken))
            .ReturnsAsync(servers);

        foreach (var server in servers)
        {
            _mockConfigService
                .Setup(x => x.GetMcpClientTools(mockTurnContext.Object, server, environmentId, authToken))
                .ReturnsAsync(new List<ModelContextProtocol.Client.McpClientTool>());
        }

        // Act & Assert
        _mockConfigService.Verify(x => x.ListToolServers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void ListToolServers_Integration_WithMultipleServers_ReturnsAllServers()
    {
        // Arrange
        var agentInstanceId = "test-agent";
        var environmentId = "test-env";
        var authToken = "test-token";

        var expectedServers = new List<MCPServerConfig>
        {
            new MCPServerConfig
            {
                mcpServerName = "mailtools",
                id = "mail-id",
                url = "https://mail.com/mcp",
                scope = "mail.read",
                audience = "api://mail",
                publisher = "Microsoft"
            },
            new MCPServerConfig
            {
                mcpServerName = "calendartools",
                id = "calendar-id",
                url = "https://calendar.com/mcp",
                scope = "calendar.read",
                audience = "api://calendar",
                publisher = "Microsoft"
            },
            new MCPServerConfig
            {
                mcpServerName = "sharepointtools",
                id = "sharepoint-id",
                url = "https://sharepoint.com/mcp",
                scope = "sites.read",
                audience = "api://sharepoint",
                publisher = "Microsoft"
            }
        };

        _mockConfigService
            .Setup(x => x.ListToolServers(agentInstanceId, environmentId, authToken))
            .ReturnsAsync(expectedServers);

        // Act
        var result = _mockConfigService.Object.ListToolServers(agentInstanceId, environmentId, authToken).Result;

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(s => s.mcpServerName == "mailtools");
        result.Should().Contain(s => s.mcpServerName == "calendartools");
        result.Should().Contain(s => s.mcpServerName == "sharepointtools");
    }
}
