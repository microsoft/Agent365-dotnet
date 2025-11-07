// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;

namespace ToolingUnitTests.Core;

[TestClass]
public class MCPServerConfigTests
{
    [TestMethod]
    public void MCPServerConfig_WithValidProperties_CreatesInstance()
    {
        // Arrange & Act
        var config = new MCPServerConfig
        {
            mcpServerName = "mailtools",
            id = "mcp-mail-001",
            url = "https://mailtools.example.com/mcp",
            scope = "mail.read mail.send",
            audience = "api://mailtools",
            publisher = "Microsoft"
        };

        // Assert
        config.Should().NotBeNull();
        config.mcpServerName.Should().Be("mailtools");
        config.id.Should().Be("mcp-mail-001");
        config.url.Should().Be("https://mailtools.example.com/mcp");
        config.scope.Should().Be("mail.read mail.send");
        config.audience.Should().Be("api://mailtools");
        config.publisher.Should().Be("Microsoft");
    }

    [TestMethod]
    public void MCPServerConfig_MailTools_HasCorrectProperties()
    {
        // Arrange & Act
        var mailToolsConfig = new MCPServerConfig
        {
            mcpServerName = "mailtools",
            id = "mail-server-id",
            url = "https://api.mail.com/mcp",
            scope = "mail.readwrite",
            audience = "api://mail",
            publisher = "Microsoft"
        };

        // Assert
        mailToolsConfig.mcpServerName.Should().Be("mailtools");
        mailToolsConfig.url.Should().Contain("mail");
    }

    [TestMethod]
    public void MCPServerConfig_CalendarTools_HasCorrectProperties()
    {
        // Arrange & Act
        var calendarToolsConfig = new MCPServerConfig
        {
            mcpServerName = "calendartools",
            id = "calendar-server-id",
            url = "https://api.calendar.com/mcp",
            scope = "calendar.readwrite",
            audience = "api://calendar",
            publisher = "Microsoft"
        };

        // Assert
        calendarToolsConfig.mcpServerName.Should().Be("calendartools");
        calendarToolsConfig.url.Should().Contain("calendar");
    }

    [TestMethod]
    public void MCPServerConfig_SharePointTools_HasCorrectProperties()
    {
        // Arrange & Act
        var sharePointToolsConfig = new MCPServerConfig
        {
            mcpServerName = "sharepointtools",
            id = "sharepoint-server-id",
            url = "https://api.sharepoint.com/mcp",
            scope = "sites.readwrite",
            audience = "api://sharepoint",
            publisher = "Microsoft"
        };

        // Assert
        sharePointToolsConfig.mcpServerName.Should().Be("sharepointtools");
        sharePointToolsConfig.url.Should().Contain("sharepoint");
    }

    [TestMethod]
    public void MCPServerConfig_MultipleServers_CanBeCreated()
    {
        // Arrange & Act
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

        // Assert
        servers.Should().HaveCount(3);
        servers.Select(s => s.mcpServerName).Should().Contain(new[] { "mailtools", "calendartools", "sharepointtools" });
    }

    [TestMethod]
    public void MCPServerConfig_WithEmptyStrings_CreatesInstance()
    {
        // Arrange & Act
        var config = new MCPServerConfig
        {
            mcpServerName = "",
            id = "",
            url = "",
            scope = "",
            audience = "",
            publisher = ""
        };

        // Assert
        config.Should().NotBeNull();
        config.mcpServerName.Should().BeEmpty();
        config.url.Should().BeEmpty();
    }

    [TestMethod]
    public void MCPServerConfig_WithSpecialCharacters_CreatesInstance()
    {
        // Arrange & Act
        var config = new MCPServerConfig
        {
            mcpServerName = "mail-tools_v2.0",
            id = "id-123-abc",
            url = "https://mail.com/mcp?version=2.0&test=true",
            scope = "mail.read mail.send",
            audience = "api://mail-service",
            publisher = "Microsoft Corp."
        };

        // Assert
        config.Should().NotBeNull();
        config.mcpServerName.Should().Be("mail-tools_v2.0");
        config.url.Should().Contain("?version=2.0");
    }

    [TestMethod]
    public void MCPServerConfig_WithLongStrings_CreatesInstance()
    {
        // Arrange & Act
        var longString = new string('a', 1000);
        var config = new MCPServerConfig
        {
            mcpServerName = longString,
            id = longString,
            url = $"https://example.com/{longString}",
            scope = longString,
            audience = longString,
            publisher = longString
        };

        // Assert
        config.Should().NotBeNull();
        config.mcpServerName.Should().HaveLength(1000);
    }

    [TestMethod]
    public void MCPServerConfig_WithUnicodeCharacters_CreatesInstance()
    {
        // Arrange & Act
        var config = new MCPServerConfig
        {
            mcpServerName = "邮件工具",
            id = "测试-id",
            url = "https://mail.com/mcp",
            scope = "mail.читать",
            audience = "api://почта",
            publisher = "マイクロソフト"
        };

        // Assert
        config.Should().NotBeNull();
        config.mcpServerName.Should().Be("邮件工具");
        config.publisher.Should().Be("マイクロソフト");
    }

    [TestMethod]
    public void MCPServerConfig_PropertySetters_UpdateValues()
    {
        // Arrange
        var config = new MCPServerConfig
        {
            mcpServerName = "mailtools",
            id = "mail-id",
            url = "https://mail.com/mcp",
            scope = "mail.read",
            audience = "api://mail",
            publisher = "Microsoft"
        };

        // Act
        config.mcpServerName = "newmailtools";
        config.id = "new-mail-id";
        config.url = "https://newmail.com/mcp";
        config.scope = "mail.readwrite";
        config.audience = "api://newmail";
        config.publisher = "NewPublisher";

        // Assert
        config.mcpServerName.Should().Be("newmailtools");
        config.id.Should().Be("new-mail-id");
        config.url.Should().Be("https://newmail.com/mcp");
        config.scope.Should().Be("mail.readwrite");
        config.audience.Should().Be("api://newmail");
        config.publisher.Should().Be("NewPublisher");
    }

    [TestMethod]
    public void MCPServerConfig_Equality_SameValues_AreNotEqual()
    {
        // Arrange
        var config1 = new MCPServerConfig
        {
            mcpServerName = "mailtools",
            id = "mail-id",
            url = "https://mail.com/mcp",
            scope = "mail.read",
            audience = "api://mail",
            publisher = "Microsoft"
        };

        var config2 = new MCPServerConfig
        {
            mcpServerName = "mailtools",
            id = "mail-id",
            url = "https://mail.com/mcp",
            scope = "mail.read",
            audience = "api://mail",
            publisher = "Microsoft"
        };

        // Act & Assert
        config1.Should().NotBeSameAs(config2);
        config1.mcpServerName.Should().Be(config2.mcpServerName);
    }

    [TestMethod]
    public void MCPServerConfig_WithHttpsUrl_CreatesInstance()
    {
        // Arrange & Act
        var config = new MCPServerConfig
        {
            mcpServerName = "securemail",
            id = "secure-id",
            url = "https://secure.mail.com:443/mcp/v1",
            scope = "mail.read",
            audience = "api://mail",
            publisher = "Microsoft"
        };

        // Assert
        config.url.Should().StartWith("https://");
        config.url.Should().Contain(":443");
    }

    [TestMethod]
    public void MCPServerConfig_WithMultipleScopes_CreatesInstance()
    {
        // Arrange & Act
        var config = new MCPServerConfig
        {
            mcpServerName = "mailtools",
            id = "mail-id",
            url = "https://mail.com/mcp",
            scope = "mail.read mail.send mail.delete calendars.read",
            audience = "api://mail",
            publisher = "Microsoft"
        };

        // Assert
        config.scope.Should().Contain("mail.read");
        config.scope.Should().Contain("mail.send");
        config.scope.Should().Contain("calendars.read");
    }
}
