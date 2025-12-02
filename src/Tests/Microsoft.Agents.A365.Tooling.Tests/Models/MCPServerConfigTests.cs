// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;

namespace Microsoft.Agents.A365.Tooling.Tests.Models;

[TestClass]
public class MCPServerConfigTests
{
    [TestMethod]
    public void MCPServerConfig_WithValidProperties_CreatesInstance()
    {
        // Arrange & Act
        var config = new MCPServerConfig
        {
            mcpServerName = TestConstants.MailToolsServerName,
            id = TestConstants.MailServerId,
            url = TestConstants.MailToolsUrl,
            scope = TestConstants.MailScope,
            audience = TestConstants.MailAudience,
            publisher = TestConstants.Publisher
        };

        // Assert
        config.Should().NotBeNull();
        config.mcpServerName.Should().Be(TestConstants.MailToolsServerName);
        config.id.Should().Be(TestConstants.MailServerId);
        config.url.Should().Be(TestConstants.MailToolsUrl);
        config.scope.Should().Be(TestConstants.MailScope);
        config.audience.Should().Be(TestConstants.MailAudience);
        config.publisher.Should().Be(TestConstants.Publisher);
    }

    [TestMethod]
    public void MCPServerConfig_PropertySetters_UpdateValues()
    {
        // Arrange
        var config = new MCPServerConfig
        {
            mcpServerName = TestConstants.MailToolsServerName,
            id = TestConstants.MailServerId,
            url = TestConstants.MailToolsUrl,
            scope = TestConstants.MailScope,
            audience = TestConstants.MailAudience,
            publisher = TestConstants.Publisher
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
}
