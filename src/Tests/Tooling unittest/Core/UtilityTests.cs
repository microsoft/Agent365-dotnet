// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Utils;

namespace ToolingUnitTests.Core;

[TestClass]
public class UtilityTests
{
    [TestMethod]
    public void GetToolingGatewayForDigitalWorker_WithValidAgentId_ReturnsCorrectUrl()
    {
        // Arrange
        var agentInstanceId = "test-agent-123";

        // Act
        var result = Utility.GetToolingGatewayForDigitalWorker(agentInstanceId);

        // Assert
        result.Should().Contain("agents");
        result.Should().Contain(agentInstanceId);
        result.Should().Contain("mcpServers");
    }

    [TestMethod]
    public void GetMcpBaseUrl_ReturnsValidUrl()
    {
        // Arrange & Act
        var result = Utility.GetMcpBaseUrl();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith("http");
    }

    [TestMethod]
    public void BuildMcpServerUrl_WithValidInputs_ReturnsCorrectUrl()
    {
        // Arrange
        var environmentId = "test-env-id";
        var serverName = "mailtools";

        // Act
        var result = Utility.BuildMcpServerUrl(environmentId, serverName);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain(serverName);
    }

    [TestMethod]
    public void BuildMcpServerUrl_WithDifferentServers_ReturnsCorrectUrls()
    {
        // Arrange
        var environmentId = "test-env-id";

        // Act
        var mailUrl = Utility.BuildMcpServerUrl(environmentId, "mailtools");
        var calendarUrl = Utility.BuildMcpServerUrl(environmentId, "calendartools");
        var sharepointUrl = Utility.BuildMcpServerUrl(environmentId, "sharepointtools");

        // Assert
        mailUrl.Should().Contain("mailtools");
        calendarUrl.Should().Contain("calendartools");
        sharepointUrl.Should().Contain("sharepointtools");
    }

    [TestMethod]
    public void GetToolsMode_ReturnsValidToolsMode()
    {
        // Arrange & Act
        var result = Utility.GetToolsMode();

        // Assert
        result.Should().BeOneOf(ToolsMode.MCPPlatform, ToolsMode.MockMCPServer);
    }

    [TestMethod]
    public void GetToolsMode_WithMockServerEnvironmentVariable_ReturnsMockMCPServer()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("TOOLS_MODE");
        Environment.SetEnvironmentVariable("TOOLS_MODE", "MockMCPServer");

        try
        {
            // Act
            var result = Utility.GetToolsMode();

            // Assert
            result.Should().Be(ToolsMode.MockMCPServer);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("TOOLS_MODE", originalValue);
        }
    }

    [TestMethod]
    public void GetToolsMode_WithDefaultValue_ReturnsMCPPlatform()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("TOOLS_MODE");
        Environment.SetEnvironmentVariable("TOOLS_MODE", null);

        try
        {
            // Act
            var result = Utility.GetToolsMode();

            // Assert
            result.Should().Be(ToolsMode.MCPPlatform);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("TOOLS_MODE", originalValue);
        }
    }

    [TestMethod]
    public void UseEnvironmentId_WithTrueValue_ReturnsTrue()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("USE_ENVIRONMENT_ID");
        Environment.SetEnvironmentVariable("USE_ENVIRONMENT_ID", "true");

        try
        {
            // Act
            var result = Utility.UseEnvironmentId();

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("USE_ENVIRONMENT_ID", originalValue);
        }
    }

    [TestMethod]
    public void UseEnvironmentId_WithFalseValue_ReturnsFalse()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("USE_ENVIRONMENT_ID");
        Environment.SetEnvironmentVariable("USE_ENVIRONMENT_ID", "false");

        try
        {
            // Act
            var result = Utility.UseEnvironmentId();

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("USE_ENVIRONMENT_ID", originalValue);
        }
    }
}
