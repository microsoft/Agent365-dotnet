// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Settings.Models;
using Microsoft.Agents.A365.Settings.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace Microsoft.Agents.A365.Settings.Tests;

/// <summary>
/// Unit tests for the AgentSettingsService class.
/// </summary>
[TestClass]
public class AgentSettingsServiceTests
{
    private readonly Mock<ILogger<AgentSettingsService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AgentSettingsServiceTests()
    {
        _mockLogger = new Mock<ILogger<AgentSettingsService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [TestMethod]
    public async Task GetSettingsTemplateByAgentTypeAsync_WithValidAgentType_ReturnsTemplate()
    {
        // Arrange
        var expectedTemplate = new AgentSettingsTemplate
        {
            Id = "template-123",
            AgentType = "custom-agent",
            Name = "Test Template",
            Version = "1.0"
        };

        SetupHttpResponse(HttpStatusCode.OK, expectedTemplate);
        var service = CreateService();

        // Act
        var result = await service.GetSettingsTemplateByAgentTypeAsync("custom-agent", "test-token");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("template-123");
        result.AgentType.Should().Be("custom-agent");
        result.Name.Should().Be("Test Template");
    }

    [TestMethod]
    public async Task GetSettingsTemplateByAgentTypeAsync_WithNotFoundResponse_ReturnsNull()
    {
        // Arrange
        SetupHttpResponse<AgentSettingsTemplate>(HttpStatusCode.NotFound, null);
        var service = CreateService();

        // Act
        var result = await service.GetSettingsTemplateByAgentTypeAsync("non-existent", "test-token");

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GetSettingsTemplateByAgentTypeAsync_WithNullAgentType_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Func<Task> act = async () => await service.GetSettingsTemplateByAgentTypeAsync(null!, "test-token");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Agent type*");
    }

    [TestMethod]
    public async Task GetSettingsTemplateByAgentTypeAsync_WithEmptyAuthToken_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Func<Task> act = async () => await service.GetSettingsTemplateByAgentTypeAsync("agent-type", "");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Auth token*");
    }

    [TestMethod]
    public async Task SetSettingsTemplateByAgentTypeAsync_WithValidTemplate_ReturnsUpdatedTemplate()
    {
        // Arrange
        var template = new AgentSettingsTemplate
        {
            Id = "template-123",
            AgentType = "custom-agent",
            Name = "Test Template"
        };

        SetupHttpResponse(HttpStatusCode.OK, template);
        var service = CreateService();

        // Act
        var result = await service.SetSettingsTemplateByAgentTypeAsync("custom-agent", template, "test-token");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("template-123");
    }

    [TestMethod]
    public async Task SetSettingsTemplateByAgentTypeAsync_WithNullTemplate_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Func<Task> act = async () => await service.SetSettingsTemplateByAgentTypeAsync("agent-type", null!, "test-token");
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("template");
    }

    [TestMethod]
    public async Task GetSettingsByAgentInstanceAsync_WithValidInstanceId_ReturnsSettings()
    {
        // Arrange
        var expectedSettings = new AgentSettings
        {
            Id = "settings-123",
            AgentInstanceId = "instance-456",
            AgentType = "custom-agent"
        };

        SetupHttpResponse(HttpStatusCode.OK, expectedSettings);
        var service = CreateService();

        // Act
        var result = await service.GetSettingsByAgentInstanceAsync("instance-456", "test-token");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("settings-123");
        result.AgentInstanceId.Should().Be("instance-456");
    }

    [TestMethod]
    public async Task GetSettingsByAgentInstanceAsync_WithNotFoundResponse_ReturnsNull()
    {
        // Arrange
        SetupHttpResponse<AgentSettings>(HttpStatusCode.NotFound, null);
        var service = CreateService();

        // Act
        var result = await service.GetSettingsByAgentInstanceAsync("non-existent", "test-token");

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GetSettingsByAgentInstanceAsync_WithNullInstanceId_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Func<Task> act = async () => await service.GetSettingsByAgentInstanceAsync(null!, "test-token");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Agent instance ID*");
    }

    [TestMethod]
    public async Task SetSettingsByAgentInstanceAsync_WithValidSettings_ReturnsUpdatedSettings()
    {
        // Arrange
        var settings = new AgentSettings
        {
            Id = "settings-123",
            AgentInstanceId = "instance-456",
            AgentType = "custom-agent"
        };

        SetupHttpResponse(HttpStatusCode.OK, settings);
        var service = CreateService();

        // Act
        var result = await service.SetSettingsByAgentInstanceAsync("instance-456", settings, "test-token");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("settings-123");
    }

    [TestMethod]
    public async Task SetSettingsByAgentInstanceAsync_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Func<Task> act = async () => await service.SetSettingsByAgentInstanceAsync("instance-456", null!, "test-token");
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new AgentSettingsService(null!, _mockConfiguration.Object, _httpClient);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [TestMethod]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new AgentSettingsService(_mockLogger.Object, null!, _httpClient);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [TestMethod]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new AgentSettingsService(_mockLogger.Object, _mockConfiguration.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    private AgentSettingsService CreateService()
    {
        return new AgentSettingsService(_mockLogger.Object, _mockConfiguration.Object, _httpClient);
    }

    private void SetupHttpResponse<T>(HttpStatusCode statusCode, T? content)
    {
        var response = new HttpResponseMessage(statusCode);
        if (content != null)
        {
            response.Content = new StringContent(JsonSerializer.Serialize(content, _jsonOptions));
        }

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}
