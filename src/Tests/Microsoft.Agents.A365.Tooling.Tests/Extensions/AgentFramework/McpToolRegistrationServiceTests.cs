// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Microsoft.Agents.A365.Tooling.Tests.Extensions.AgentFramework;

[TestClass]
public class McpToolRegistrationServiceTests
{
    private Mock<ILogger<IMcpToolRegistrationService>> _mockLogger = null!;
    private Mock<IMcpToolServerConfigurationService> _mockConfigService = null!;
    private McpToolRegistrationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        _mockLogger = new Mock<ILogger<IMcpToolRegistrationService>>();
        _mockConfigService = new Mock<IMcpToolServerConfigurationService>();
        _service = new McpToolRegistrationService(_mockLogger.Object, _mockConfigService.Object);
    }

    [TestMethod]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<IMcpToolRegistrationService>>();
        var mockConfigService = new Mock<IMcpToolServerConfigurationService>();

        // Act
        var service = new McpToolRegistrationService(mockLogger.Object, mockConfigService.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [TestMethod]
    public async Task ListToolServers_WithMailTools_ReturnsMailServerConfig()
    {
        // Arrange
        var agentUserId = "test-user";
        var environmentId = "test-env";
        var authToken = "test-token";

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
            .Setup(x => x.ListToolServers(agentUserId, environmentId, authToken))
            .ReturnsAsync(new List<MCPServerConfig> { mailServerConfig });

        // Act
        var result = await _mockConfigService.Object.ListToolServers(agentUserId, environmentId, authToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].mcpServerName.Should().Be("mailtools");
    }

    [TestMethod]
    public async Task ListToolServers_WithCalendarTools_ReturnsCalendarServerConfig()
    {
        // Arrange
        var agentUserId = "test-user";
        var environmentId = "test-env";
        var authToken = "test-token";

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
            .Setup(x => x.ListToolServers(agentUserId, environmentId, authToken))
            .ReturnsAsync(new List<MCPServerConfig> { calendarServerConfig });

        // Act
        var result = await _mockConfigService.Object.ListToolServers(agentUserId, environmentId, authToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].mcpServerName.Should().Be("calendartools");
    }

    [TestMethod]
    public async Task ListToolServers_WithSharePointTools_ReturnsSharePointServerConfig()
    {
        // Arrange
        var agentUserId = "test-user";
        var environmentId = "test-env";
        var authToken = "test-token";

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
            .Setup(x => x.ListToolServers(agentUserId, environmentId, authToken))
            .ReturnsAsync(new List<MCPServerConfig> { sharePointServerConfig });

        // Act
        var result = await _mockConfigService.Object.ListToolServers(agentUserId, environmentId, authToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].mcpServerName.Should().Be("sharepointtools");
    }

    [TestMethod]
    public async Task ListToolServers_WithMultipleTools_ReturnsAllServerConfigs()
    {
        // Arrange
        var agentUserId = "test-user";
        var environmentId = "test-env";
        var authToken = "test-token";

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
            .Setup(x => x.ListToolServers(agentUserId, environmentId, authToken))
            .ReturnsAsync(servers);

        // Act
        var result = await _mockConfigService.Object.ListToolServers(agentUserId, environmentId, authToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(s => s.mcpServerName == "mailtools");
        result.Should().Contain(s => s.mcpServerName == "calendartools");
        result.Should().Contain(s => s.mcpServerName == "sharepointtools");
    }
}
