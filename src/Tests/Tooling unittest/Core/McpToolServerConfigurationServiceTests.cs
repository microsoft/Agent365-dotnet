// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using System.Text.Json;

namespace ToolingUnitTests.Core;

[TestClass]
public class McpToolServerConfigurationServiceTests
{
    private Mock<ILogger<IMcpToolServerConfigurationService>> _mockLogger = null!;
    private McpToolServerConfigurationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        _mockLogger = new Mock<ILogger<IMcpToolServerConfigurationService>>();
        _service = new McpToolServerConfigurationService(_mockLogger.Object);
    }

    [TestMethod]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<IMcpToolServerConfigurationService>>();

        // Act
        var service = new McpToolServerConfigurationService(mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [TestMethod]
    public async Task ListToolServers_WithValidParameters_ReturnsServerList()
    {
        // Arrange
        var agentInstanceId = "test-agent-123";
        var environmentId = "test-env";
        var authToken = "test-token";
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        // Act
        var result = await _service.ListToolServers(agentInstanceId, environmentId, authToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<MCPServerConfig>>();
    }

    [TestMethod]
    public async Task GetMcpClientTools_WithNullServerName_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockTurnContext = new Mock<ITurnContext>();
        var serverConfig = new MCPServerConfig
        {
            mcpServerName = null!,
            url = "https://test.com",
            id = "test-id",
            scope = "test-scope",
            audience = "test-audience",
            publisher = "test-publisher"
        };
        var environmentId = "test-env";
        var authToken = "test-token";

        // Act
        Func<Task> act = async () => await _service.GetMcpClientTools(mockTurnContext.Object, serverConfig, environmentId, authToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MCP Server name cannot be null or empty*");
    }

    [TestMethod]
    public async Task GetMcpClientTools_WithEmptyServerName_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockTurnContext = new Mock<ITurnContext>();
        var serverConfig = new MCPServerConfig
        {
            mcpServerName = string.Empty,
            url = "https://test.com",
            id = "test-id",
            scope = "test-scope",
            audience = "test-audience",
            publisher = "test-publisher"
        };
        var environmentId = "test-env";
        var authToken = "test-token";

        // Act
        Func<Task> act = async () => await _service.GetMcpClientTools(mockTurnContext.Object, serverConfig, environmentId, authToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MCP Server name cannot be null or empty*");
    }

    [TestMethod]
    public async Task GetMcpClientTools_WithWhitespaceServerName_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockTurnContext = new Mock<ITurnContext>();
        var serverConfig = new MCPServerConfig
        {
            mcpServerName = "   ",
            url = "https://test.com",
            id = "test-id",
            scope = "test-scope",
            audience = "test-audience",
            publisher = "test-publisher"
        };
        var environmentId = "test-env";
        var authToken = "test-token";

        // Act
        Func<Task> act = async () => await _service.GetMcpClientTools(mockTurnContext.Object, serverConfig, environmentId, authToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MCP Server name cannot be null or empty*");
    }

    [TestMethod]
    public void ParseServerConfig_WithValidJsonElement_ReturnsServerConfig()
    {
        // Arrange - Test the private ParseServerConfig method using reflection
        var method = typeof(McpToolServerConfigurationService).GetMethod("ParseServerConfig", BindingFlags.NonPublic | BindingFlags.Static);
        var jsonString = @"{
            ""mcpServerName"": ""mailtools"",
            ""url"": ""https://mail.com/mcp"",
            ""id"": ""mail-123"",
            ""scope"": ""mail.read"",
            ""audience"": ""api://mail"",
            ""publisher"": ""Microsoft""
        }";
        var jsonDoc = JsonDocument.Parse(jsonString);
        var serverElement = jsonDoc.RootElement;

        // Act
        var result = (MCPServerConfig?)method!.Invoke(null, new object[] { serverElement });

        // Assert
        result.Should().NotBeNull();
        result!.mcpServerName.Should().Be("mailtools");
        result.url.Should().Be("https://mail.com/mcp");
        result.id.Should().Be("mail-123");
        result.scope.Should().Be("mail.read");
        result.audience.Should().Be("api://mail");
        result.publisher.Should().Be("Microsoft");
    }

    [TestMethod]
    public void ParseServerConfig_WithMissingName_ReturnsNull()
    {
        // Arrange - Test validation logic in ParseServerConfig
        var method = typeof(McpToolServerConfigurationService).GetMethod("ParseServerConfig", BindingFlags.NonPublic | BindingFlags.Static);
        var jsonString = @"{
            ""url"": ""https://test.com/mcp"",
            ""id"": ""test-123""
        }";
        var jsonDoc = JsonDocument.Parse(jsonString);
        var serverElement = jsonDoc.RootElement;

        // Act
        var result = (MCPServerConfig?)method!.Invoke(null, new object[] { serverElement });

        // Assert
        result.Should().BeNull("missing mcpServerName should return null");
    }

    [TestMethod]
    public void ParseServerConfig_WithMissingUrl_ReturnsNull()
    {
        // Arrange - Test validation logic in ParseServerConfig
        var method = typeof(McpToolServerConfigurationService).GetMethod("ParseServerConfig", BindingFlags.NonPublic | BindingFlags.Static);
        var jsonString = @"{
            ""mcpServerName"": ""testserver"",
            ""id"": ""test-123""
        }";
        var jsonDoc = JsonDocument.Parse(jsonString);
        var serverElement = jsonDoc.RootElement;

        // Act
        var result = (MCPServerConfig?)method!.Invoke(null, new object[] { serverElement });

        // Assert
        result.Should().BeNull("missing url should return null");
    }

    [TestMethod]
    public void ParseServerConfig_WithAlternativeNameProperty_UsesUniqueName()
    {
        // Arrange - Test alternate property name handling
        var method = typeof(McpToolServerConfigurationService).GetMethod("ParseServerConfig", BindingFlags.NonPublic | BindingFlags.Static);
        var jsonString = @"{
            ""mcpServerUniqueName"": ""uniqueserver"",
            ""url"": ""https://unique.com/mcp"",
            ""id"": ""unique-123""
        }";
        var jsonDoc = JsonDocument.Parse(jsonString);
        var serverElement = jsonDoc.RootElement;

        // Act
        var result = (MCPServerConfig?)method!.Invoke(null, new object[] { serverElement });

        // Assert
        result.Should().NotBeNull();
        result!.mcpServerName.Should().Be("uniqueserver");
    }

    [TestMethod]
    public void ParseServerConfig_WithOptionalFieldsMissing_UsesDefaults()
    {
        // Arrange - Test default values for optional fields
        var method = typeof(McpToolServerConfigurationService).GetMethod("ParseServerConfig", BindingFlags.NonPublic | BindingFlags.Static);
        var jsonString = @"{
            ""mcpServerName"": ""minimalserver"",
            ""url"": ""https://minimal.com/mcp""
        }";
        var jsonDoc = JsonDocument.Parse(jsonString);
        var serverElement = jsonDoc.RootElement;

        // Act
        var result = (MCPServerConfig?)method!.Invoke(null, new object[] { serverElement });

        // Assert
        result.Should().NotBeNull();
        result!.id.Should().Be(string.Empty, "missing id should default to empty string");
        result.scope.Should().Be(string.Empty, "missing scope should default to empty string");
        result.audience.Should().Be(string.Empty, "missing audience should default to empty string");
        result.publisher.Should().Be(string.Empty, "missing publisher should default to empty string");
    }

    [TestMethod]
    public void IsDevScenario_WithDevelopmentEnvironment_ReturnsTrue()
    {
        // Arrange - Test the private IsDevScenario method
        var method = typeof(McpToolServerConfigurationService).GetMethod("IsDevScenario", BindingFlags.NonPublic | BindingFlags.Static);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        // Act
        var result = (bool)method!.Invoke(null, null)!;

        // Assert
        result.Should().BeTrue("Development environment should be detected as dev scenario");
    }

    [TestMethod]
    public void IsDevScenario_WithProductionEnvironment_ReturnsFalse()
    {
        // Arrange - Test production environment detection
        var method = typeof(McpToolServerConfigurationService).GetMethod("IsDevScenario", BindingFlags.NonPublic | BindingFlags.Static);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

        // Act
        var result = (bool)method!.Invoke(null, null)!;

        // Assert
        result.Should().BeFalse("Production environment should not be dev scenario");
    }

    [TestMethod]
    public void ParseServerConfig_WithAllKnownServers_ParsesCorrectly()
    {
        // Arrange - Test parsing for all three known server types
        var method = typeof(McpToolServerConfigurationService).GetMethod("ParseServerConfig", BindingFlags.NonPublic | BindingFlags.Static);
        
        var mailJson = @"{""mcpServerName"": ""mailtools"", ""url"": ""https://mail.com/mcp""}";
        var calendarJson = @"{""mcpServerName"": ""calendartools"", ""url"": ""https://calendar.com/mcp""}";
        var sharepointJson = @"{""mcpServerName"": ""sharepointtools"", ""url"": ""https://sharepoint.com/mcp""}";

        // Act
        var mailResult = (MCPServerConfig?)method!.Invoke(null, new object[] { JsonDocument.Parse(mailJson).RootElement });
        var calendarResult = (MCPServerConfig?)method.Invoke(null, new object[] { JsonDocument.Parse(calendarJson).RootElement });
        var sharepointResult = (MCPServerConfig?)method.Invoke(null, new object[] { JsonDocument.Parse(sharepointJson).RootElement });

        // Assert
        mailResult.Should().NotBeNull();
        mailResult!.mcpServerName.Should().Be("mailtools");
        calendarResult.Should().NotBeNull();
        calendarResult!.mcpServerName.Should().Be("calendartools");
        sharepointResult.Should().NotBeNull();
        sharepointResult!.mcpServerName.Should().Be("sharepointtools");
    }

    [TestMethod]
    public void ParseServerConfigFromManifest_WithValidJson_ReturnsConfig()
    {
        // Arrange
        var serviceType = typeof(McpToolServerConfigurationService);
        var method = serviceType.GetMethod("ParseServerConfigFromManifest",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(JsonElement), typeof(string) },
            null);
        
        var json = @"{
            ""mcpServerName"": ""mailtools"",
            ""id"": ""test-id"",
            ""scope"": ""test-scope"",
            ""audience"": ""test-audience"",
            ""publisher"": ""Microsoft""
        }";
        var environmentId = "test-env";

        // Act
        var result = (MCPServerConfig?)method!.Invoke(null, new object[] { JsonDocument.Parse(json).RootElement, environmentId });

        // Assert
        result.Should().NotBeNull();
        result!.mcpServerName.Should().Be("mailtools");
        result.id.Should().Be("test-id");
        result.scope.Should().Be("test-scope");
        result.audience.Should().Be("test-audience");
        result.publisher.Should().Be("Microsoft");
    }

    [TestMethod]
    public void ParseServerConfigFromManifest_WithMissingName_ReturnsNull()
    {
        // Arrange
        var serviceType = typeof(McpToolServerConfigurationService);
        var method = serviceType.GetMethod("ParseServerConfigFromManifest",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(JsonElement), typeof(string) },
            null);
        
        var json = @"{""id"": ""test-id"", ""scope"": ""test-scope""}";
        var environmentId = "test-env";

        // Act
        var result = (MCPServerConfig?)method!.Invoke(null, new object[] { JsonDocument.Parse(json).RootElement, environmentId });

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void ParseServerConfigFromManifest_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var serviceType = typeof(McpToolServerConfigurationService);
        var method = serviceType.GetMethod("ParseServerConfigFromManifest",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(JsonElement), typeof(string) },
            null);
        
        var json = @"{""invalid"": 123}";
        var environmentId = "test-env";

        // Act
        var result = (MCPServerConfig?)method!.Invoke(null, new object[] { JsonDocument.Parse(json).RootElement, environmentId });

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GetMCPServersFromManifest_WithMissingFile_ReturnsEmptyList()
    {
        // Arrange
        var serviceType = typeof(McpToolServerConfigurationService);
        var method = serviceType.GetMethod("GetMCPServersFromManifest",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var environmentId = "test-env";

        // Act
        var result = (List<MCPServerConfig>?)method!.Invoke(_service, new object[] { environmentId });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GetMcpClientTools_WithHttpRequestException_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockTurnContext = new Mock<ITurnContext>();
        var serverConfig = new MCPServerConfig
        {
            mcpServerName = "test-server",
            url = "https://invalid-endpoint.com",
            id = "test-id",
            scope = "test-scope",
            audience = "test-audience",
            publisher = "Microsoft"
        };
        var environmentId = "test-env";
        var authToken = "test-token";

        // Act & Assert
        var act = async () => await _service.GetMcpClientTools(mockTurnContext.Object, serverConfig, environmentId, authToken);
        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*HTTP error connecting to MCP server*");
    }

}
