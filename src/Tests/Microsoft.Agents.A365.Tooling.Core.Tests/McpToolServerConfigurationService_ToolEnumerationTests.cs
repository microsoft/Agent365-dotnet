// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Moq;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// Unit tests for McpToolServerConfigurationService tool enumeration methods.
/// </summary>
public class McpToolServerConfigurationService_ToolEnumerationTests
{
    private readonly Mock<ILogger<IMcpToolServerConfigurationService>> _mockLogger;
    private readonly Mock<ITurnContext> _mockTurnContext;
    private readonly Mock<McpToolServerConfigurationService> _mockService;

    public McpToolServerConfigurationService_ToolEnumerationTests()
    {
        _mockLogger = new Mock<ILogger<IMcpToolServerConfigurationService>>();
        _mockTurnContext = new Mock<ITurnContext>();

        var mockConfiguration = new Mock<IConfiguration>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();

        // Create a partial mock that calls the real enumeration methods
        // but allows us to mock the underlying ListToolServersAsync and GetMcpClientToolsAsync
        _mockService = new Mock<McpToolServerConfigurationService>(
            MockBehavior.Default,
            _mockLogger.Object,
            mockConfiguration.Object,
            mockServiceProvider.Object,
            mockHttpClientFactory.Object)
        {
            CallBase = true
        };
    }

    #region EnumerateToolsFromServersAsync Tests

    [Fact]
    public async Task EnumerateToolsFromServersAsync_WhenListServersFails_ReturnsEmptyResult()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var (servers, toolsByServer) = await _mockService.Object.EnumerateToolsFromServersAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Assert
        servers.Should().BeEmpty();
        toolsByServer.Should().BeEmpty();
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_WhenNoServersConfigured_ReturnsEmptyResult()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(new List<MCPServerConfig>());

        // Act
        var (servers, toolsByServer) = await _mockService.Object.EnumerateToolsFromServersAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Assert
        servers.Should().BeEmpty();
        toolsByServer.Should().BeEmpty();
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_FiltersInvalidServers_WithMissingName()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        var servers = new List<MCPServerConfig>
        {
            CreateServerConfig(null!, "http://valid-url.com"), // Invalid - no name
            CreateServerConfig("valid-server", "http://valid-url.com") // Valid
        };

        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(servers);

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "valid-server"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .ReturnsAsync(new List<McpClientTool>());

        // Act
        var (resultServers, toolsByServer) = await _mockService.Object.EnumerateToolsFromServersAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Assert
        resultServers.Should().HaveCount(2); // Original servers returned
        toolsByServer.Should().ContainKey("valid-server");
        toolsByServer.Should().HaveCount(1); // Only valid server processed
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_FiltersInvalidServers_WithMissingUrl()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        var servers = new List<MCPServerConfig>
        {
            CreateServerConfig("server-no-url", null!), // Invalid - no URL
            CreateServerConfig("valid-server", "http://valid-url.com") // Valid
        };

        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(servers);

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "valid-server"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .ReturnsAsync(new List<McpClientTool>());

        // Act
        var (_, toolsByServer) = await _mockService.Object.EnumerateToolsFromServersAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Assert
        toolsByServer.Should().ContainKey("valid-server");
        toolsByServer.Should().HaveCount(1);
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_EnumeratesToolsFromMultipleServers()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        var servers = new List<MCPServerConfig>
        {
            CreateServerConfig("server1", "http://server1.com"),
            CreateServerConfig("server2", "http://server2.com")
        };

        var tools1 = new List<McpClientTool>();
        var tools2 = new List<McpClientTool>();

        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(servers);

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "server1"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .ReturnsAsync(tools1);

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "server2"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .ReturnsAsync(tools2);

        // Act
        var (resultServers, toolsByServer) = await _mockService.Object.EnumerateToolsFromServersAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Assert
        resultServers.Should().HaveCount(2);
        toolsByServer.Should().ContainKey("server1");
        toolsByServer.Should().ContainKey("server2");
        toolsByServer["server1"].Should().BeSameAs(tools1);
        toolsByServer["server2"].Should().BeSameAs(tools2);
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_HandlesIndividualServerFailures()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        var servers = new List<MCPServerConfig>
        {
            CreateServerConfig("failing-server", "http://failing.com"),
            CreateServerConfig("working-server", "http://working.com")
        };

        var workingTools = new List<McpClientTool>();

        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(servers);

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "failing-server"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .ThrowsAsync(new Exception("Server connection failed"));

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "working-server"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .ReturnsAsync(workingTools);

        // Act
        var (resultServers, toolsByServer) = await _mockService.Object.EnumerateToolsFromServersAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Assert
        resultServers.Should().HaveCount(2);
        toolsByServer.Should().HaveCount(1);
        toolsByServer.Should().ContainKey("working-server");
        toolsByServer.Should().NotContainKey("failing-server");
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_EnumeratesInParallel()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        var servers = new List<MCPServerConfig>
        {
            CreateServerConfig("server1", "http://server1.com"),
            CreateServerConfig("server2", "http://server2.com"),
            CreateServerConfig("server3", "http://server3.com")
        };

        var callOrder = new List<string>();
        var tcs1 = new TaskCompletionSource<IList<McpClientTool>>();
        var tcs2 = new TaskCompletionSource<IList<McpClientTool>>();
        var tcs3 = new TaskCompletionSource<IList<McpClientTool>>();

        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(servers);

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "server1"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .Returns(async () =>
            {
                lock (callOrder) callOrder.Add("server1-start");
                var result = await tcs1.Task;
                lock (callOrder) callOrder.Add("server1-end");
                return result;
            });

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "server2"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .Returns(async () =>
            {
                lock (callOrder) callOrder.Add("server2-start");
                var result = await tcs2.Task;
                lock (callOrder) callOrder.Add("server2-end");
                return result;
            });

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "server3"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .Returns(async () =>
            {
                lock (callOrder) callOrder.Add("server3-start");
                var result = await tcs3.Task;
                lock (callOrder) callOrder.Add("server3-end");
                return result;
            });

        // Act
        var enumerationTask = _mockService.Object.EnumerateToolsFromServersAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Give time for all tasks to start
        await Task.Delay(50);

        // All servers should have started before any completes (parallel execution)
        lock (callOrder)
        {
            callOrder.Should().Contain("server1-start");
            callOrder.Should().Contain("server2-start");
            callOrder.Should().Contain("server3-start");
            callOrder.Should().NotContain("server1-end");
            callOrder.Should().NotContain("server2-end");
            callOrder.Should().NotContain("server3-end");
        }

        // Complete all tasks
        tcs1.SetResult(new List<McpClientTool>());
        tcs2.SetResult(new List<McpClientTool>());
        tcs3.SetResult(new List<McpClientTool>());

        var (_, toolsByServer) = await enumerationTask;

        // Assert
        toolsByServer.Should().HaveCount(3);
    }

    #endregion

    #region EnumerateAllToolsAsync Tests

    [Fact]
    public async Task EnumerateAllToolsAsync_ReturnsFlatListOfAllTools()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        var servers = new List<MCPServerConfig>
        {
            CreateServerConfig("server1", "http://server1.com"),
            CreateServerConfig("server2", "http://server2.com")
        };

        // Note: McpClientTool is from ModelContextProtocol.Client and may not be easily mockable
        // We'll use empty lists for now
        var tools1 = new List<McpClientTool>();
        var tools2 = new List<McpClientTool>();

        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(servers);

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "server1"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .ReturnsAsync(tools1);

        _mockService
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "server2"),
                It.IsAny<string>(),
                It.IsAny<ToolOptions>()))
            .ReturnsAsync(tools2);

        // Act
        var result = await _mockService.Object.EnumerateAllToolsAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // Both tool lists were empty
    }

    [Fact]
    public async Task EnumerateAllToolsAsync_WhenNoServers_ReturnsEmptyList()
    {
        // Arrange
        var toolOptions = new ToolOptions();
        _mockService
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(new List<MCPServerConfig>());

        // Act
        var result = await _mockService.Object.EnumerateAllToolsAsync(
            "agent-id",
            "auth-token",
            _mockTurnContext.Object,
            toolOptions);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    // V1 server helper — no audience so the legacy (no-tokenProvider) overload is used safely.
    private static MCPServerConfig CreateServerConfig(string name, string url)
    {
        return new MCPServerConfig
        {
            mcpServerName = name,
            url = url,
            id = $"id-{name}",
            scope = "scope",
            audience = null, // V1: no per-audience token needed
            publisher = "publisher"
        };
    }

    #endregion
}
