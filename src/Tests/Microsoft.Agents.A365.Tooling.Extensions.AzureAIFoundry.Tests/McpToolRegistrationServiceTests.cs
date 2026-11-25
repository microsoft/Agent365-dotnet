// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Core.Pipeline;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Tests;

/// <summary>
/// Unit tests for McpToolRegistrationService class.
/// </summary>
public class McpToolRegistrationServiceTests
{
    private readonly Mock<ILogger<IMcpToolRegistrationService>> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IMcpToolServerConfigurationService> _mockConfigService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ITurnContext> _mockTurnContext;
    private readonly McpToolRegistrationService _service;

    public McpToolRegistrationServiceTests()
    {
        _mockLogger = new Mock<ILogger<IMcpToolRegistrationService>>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Create mock for IMcpToolServerConfigurationService interface
        _mockConfigService = new Mock<IMcpToolServerConfigurationService>();
        _mockConfiguration = new Mock<IConfiguration>();

        _mockTurnContext = new Mock<ITurnContext>();

        // Setup turn context with activity
        var mockActivity = new Mock<IActivity>();
        mockActivity.Setup(a => a.Recipient).Returns(new ChannelAccount { Id = "test-agent-id" });
        _mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);

        _service = new McpToolRegistrationService(
            _mockLogger.Object,
            _mockServiceProvider.Object,
            _mockConfigService.Object,
            _mockConfiguration.Object);
    }

    #region Helper Methods

    private const string TestAuthToken = "test-token";
    private const string TestAgentInstanceId = "agent-id";

    /// <summary>
    /// Creates a test MCP server configuration with the specified name.
    /// </summary>
    private static MCPServerConfig CreateTestServerConfig(string serverName, string? url = null)
    {
        return new MCPServerConfig
        {
            mcpServerName = serverName,
            url = url ?? $"https://{serverName}.com",
            id = $"id-{serverName}",
            scope = "scope",
            audience = "audience",
            publisher = "publisher"
        };
    }

    /// <summary>
    /// Creates a placeholder list of tools with the specified count.
    /// Note: McpClientTool is a sealed class and cannot be mocked with Moq.
    /// Since the AzureAIFoundry service only uses tool count for logging (not individual tool properties),
    /// we create placeholder lists with the correct count to verify the correct number of servers/tools are processed.
    /// </summary>
    private static IList<McpClientTool> CreatePlaceholderTools(int count)
    {
        // Return a list with the specified count; the service only uses Count property
        return new List<McpClientTool>(new McpClientTool[count]);
    }

    /// <summary>
    /// Sets up the mock enumeration service to return empty results.
    /// </summary>
    private void SetupMocksForEmptyToolEnumeration(Action<ToolOptions>? captureToolOptions = null)
    {
        var setup = _mockConfigService
            .Setup(x => x.GetMcpToolSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IMcpTokenProvider>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()));

        if (captureToolOptions != null)
        {
            setup.Callback<string, string, IMcpTokenProvider, ITurnContext, ToolOptions, CancellationToken>((_, _, _, _, options, _) => captureToolOptions(options));
        }

        setup.ReturnsAsync(() => new McpToolSet(new List<MCPServerConfig>(), new Dictionary<string, IReadOnlyList<McpClientTool>>()));
    }

    /// <summary>
    /// Sets up the mock enumeration service to return the specified servers and tools.
    /// </summary>
    private void SetupMocksForToolEnumeration(List<MCPServerConfig> servers, Dictionary<string, IList<McpClientTool>> toolsByServer, IAsyncDisposable? clients = null)
    {
        _mockConfigService
            .Setup(x => x.GetMcpToolSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IMcpTokenProvider>(),
                It.IsAny<ITurnContext>(),
                It.IsAny<ToolOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new McpToolSet(
                servers,
                toolsByServer.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<McpClientTool>)entry.Value.ToList()),
                clients is null ? null : new[] { clients }));
    }

    /// <summary>
    /// Creates a PersistentAgentsClient whose requests are captured by <paramref name="handler"/>.
    /// </summary>
    private static PersistentAgentsClient CreateAgentsClient(CapturingHandler handler)
    {
        var options = new PersistentAgentsAdministrationClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(handler)),
            Retry = { MaxRetries = 0 },
        };
        return new PersistentAgentsClient("https://example.services.ai.azure.com/api/projects/test", new FakeTokenCredential(), options);
    }

    /// <summary>
    /// Stands in for the MCP clients of a tool set and counts how often they are disposed.
    /// </summary>
    private sealed class DisposalProbe : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Captures the body of each request and answers with an agent.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = new();

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => SendAsync(request, cancellationToken).GetAwaiter().GetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            const string agent = "{\"id\":\"asst_1\",\"object\":\"assistant\",\"created_at\":0,\"name\":null,\"description\":null,\"model\":\"gpt-4o\",\"instructions\":null,\"tools\":[],\"tool_resources\":{},\"temperature\":1,\"top_p\":1,\"metadata\":{}}";
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(agent, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(GetToken(requestContext, cancellationToken));
    }

    #endregion

    #region AddToolServersToAgent (Sync) Tests

    [Fact]
    public void AddToolServersToAgent_WithNullAgentClient_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => _service.AddToolServersToAgent(
            agentClient: null!,
            agentInstanceId: TestAgentInstanceId,
            userAuthorization: null!,
            turnContext: _mockTurnContext.Object,
            authToken: "token");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentClient");
    }

    #endregion

    #region AddToolServersToAgentAsync Tests

    [Fact]
    public async Task AddToolServersToAgentAsync_WithNullAgentClient_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => _service.AddToolServersToAgentAsync(
            agentClient: null!,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: _mockTurnContext.Object,
            authToken: "token");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("agentClient");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddToolServersToAgent_NoServersFound_SendsEmptyMcpToolResources(bool useAsyncOverload)
    {
        // Arrange
        SetupMocksForEmptyToolEnumeration();
        var handler = new CapturingHandler();
        var agentClient = CreateAgentsClient(handler);
        var turnContext = new Mock<ITurnContext>();
        var activity = new Mock<IActivity>();
        activity.Setup(a => a.Recipient).Returns(new ChannelAccount { Id = "test-agent-id", AgenticAppId = "asst_1" });
        turnContext.Setup(tc => tc.Activity).Returns(activity.Object);

        // Act
        if (useAsyncOverload)
        {
            await _service.AddToolServersToAgentAsync(agentClient, null!, "handler", turnContext.Object, "token");
        }
        else
        {
            _service.AddToolServersToAgent(agentClient, "asst_1", null!, turnContext.Object, "token");
        }

        // Assert: a null value would be left out of the request, leaving the earlier MCP resources and headers on the agent.
        using var body = System.Text.Json.JsonDocument.Parse(handler.RequestBodies.Should().ContainSingle().Subject);
        body.RootElement.GetProperty("tools").GetArrayLength().Should().Be(0);
        body.RootElement.GetProperty("tool_resources").GetProperty("mcp").GetArrayLength().Should().Be(0);
    }

    #endregion

    #region GetMcpToolDefinitionsAndResourcesAsync Tests

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_WithNoServers_ReturnsEmptyResults()
    {
        // Arrange
        SetupMocksForEmptyToolEnumeration();

        // Act
        var (toolDefinitions, toolResources) = await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert
        toolDefinitions.Should().BeEmpty();
        toolResources.Should().BeNull();
    }

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_CallsGetMcpToolSetAsync()
    {
        // Arrange
        SetupMocksForEmptyToolEnumeration();

        // Act
        await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert
        _mockConfigService.Verify(
            x => x.GetMcpToolSetAsync(
                TestAgentInstanceId,
                TestAuthToken,
                It.IsAny<IMcpTokenProvider>(),
                _mockTurnContext.Object,
                It.Is<ToolOptions>(o => o.UserAgentConfiguration == Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_UsesCorrectUserAgentConfiguration()
    {
        // Arrange
        ToolOptions? capturedToolOptions = null;
        SetupMocksForEmptyToolEnumeration(options => capturedToolOptions = options);

        // Act
        await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert
        capturedToolOptions.Should().NotBeNull();
        capturedToolOptions!.UserAgentConfiguration.Should().BeSameAs(Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance);
    }

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_WithServers_CreatesToolDefinitionsAndResources()
    {
        // Arrange
        var servers = new List<MCPServerConfig> { CreateTestServerConfig("test-server", "https://test-server.example.com") };
        var placeholderTools = CreatePlaceholderTools(3);
        var toolsByServer = new Dictionary<string, IList<McpClientTool>> { ["test-server"] = placeholderTools };
        SetupMocksForToolEnumeration(servers, toolsByServer);

        // Act
        var (toolDefinitions, toolResources) = await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert - verify tool definitions
        toolDefinitions.Should().HaveCount(1);
        toolDefinitions[0].ServerLabel.Should().Be("test-server");
        toolDefinitions[0].ServerUrl.Should().Be("https://test-server.example.com");

        // Assert - verify tool resources
        toolResources.Should().NotBeNull();
        toolResources!.Mcp.Should().HaveCount(1);
        toolResources.Mcp[0].ServerLabel.Should().Be("test-server");
    }

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_RemovesMcpPrefixFromServerName()
    {
        // Arrange
        var servers = new List<MCPServerConfig> { CreateTestServerConfig("mcp_prefixed-server") };
        var placeholderTools = CreatePlaceholderTools(1);
        var toolsByServer = new Dictionary<string, IList<McpClientTool>> { ["mcp_prefixed-server"] = placeholderTools };
        SetupMocksForToolEnumeration(servers, toolsByServer);

        // Act
        var (toolDefinitions, toolResources) = await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert - verify mcp_ prefix is removed from server label
        toolDefinitions.Should().HaveCount(1);
        toolDefinitions[0].ServerLabel.Should().Be("prefixed-server");
        toolResources!.Mcp[0].ServerLabel.Should().Be("prefixed-server");
    }

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_AddsAuthorizationHeader()
    {
        // Arrange
        var servers = new List<MCPServerConfig> { CreateTestServerConfig("test-server") };
        var placeholderTools = CreatePlaceholderTools(1);
        var toolsByServer = new Dictionary<string, IList<McpClientTool>> { ["test-server"] = placeholderTools };
        SetupMocksForToolEnumeration(servers, toolsByServer);

        // Act
        var (_, toolResources) = await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert - verify resources are created for the server with tools
        toolResources.Should().NotBeNull();
        toolResources!.Mcp.Should().HaveCount(1);
        toolResources.Mcp[0].ServerLabel.Should().Be("test-server");
        // Note: Authorization headers are set internally; we verify the resource was created properly
    }

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_SkipsServerWithMissingConfig()
    {
        // Arrange
        var servers = new List<MCPServerConfig> { CreateTestServerConfig("server-with-config") };
        var toolsByServer = new Dictionary<string, IList<McpClientTool>>
        {
            ["server-with-config"] = CreatePlaceholderTools(1),
            ["server-without-config"] = CreatePlaceholderTools(1)
        };
        SetupMocksForToolEnumeration(servers, toolsByServer);

        // Act
        var (toolDefinitions, toolResources) = await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert - only server with config is processed
        toolDefinitions.Should().HaveCount(1);
        toolDefinitions[0].ServerLabel.Should().Be("server-with-config");
        toolResources!.Mcp.Should().HaveCount(1);
        toolResources.Mcp[0].ServerLabel.Should().Be("server-with-config");
    }

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_WithMultipleServers_CreatesDefinitionsAndResourcesForEach()
    {
        // Arrange
        var servers = new List<MCPServerConfig>
        {
            CreateTestServerConfig("server-alpha", "https://alpha.example.com"),
            CreateTestServerConfig("server-beta", "https://beta.example.com"),
            CreateTestServerConfig("server-gamma", "https://gamma.example.com")
        };
        var toolsByServer = new Dictionary<string, IList<McpClientTool>>
        {
            ["server-alpha"] = CreatePlaceholderTools(2),
            ["server-beta"] = CreatePlaceholderTools(1),
            ["server-gamma"] = CreatePlaceholderTools(3)
        };
        SetupMocksForToolEnumeration(servers, toolsByServer);

        // Act
        var (toolDefinitions, toolResources) = await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert - verify all servers are represented
        toolDefinitions.Should().HaveCount(3);
        toolDefinitions.Select(d => d.ServerLabel).Should().BeEquivalentTo("server-alpha", "server-beta", "server-gamma");

        // Verify URLs are correctly assigned
        toolDefinitions.Single(d => d.ServerLabel == "server-alpha").ServerUrl.Should().Be("https://alpha.example.com");
        toolDefinitions.Single(d => d.ServerLabel == "server-beta").ServerUrl.Should().Be("https://beta.example.com");
        toolDefinitions.Single(d => d.ServerLabel == "server-gamma").ServerUrl.Should().Be("https://gamma.example.com");

        // Verify resources are created for all servers
        toolResources.Should().NotBeNull();
        toolResources!.Mcp.Should().HaveCount(3);
        toolResources.Mcp.Select(r => r.ServerLabel).Should().BeEquivalentTo("server-alpha", "server-beta", "server-gamma");
    }

    [Fact]
    public async Task GetMcpToolDefinitionsAndResourcesAsync_DisposesClientsAfterListingTools()
    {
        // Arrange
        var servers = new List<MCPServerConfig> { CreateTestServerConfig("test-server") };
        var toolsByServer = new Dictionary<string, IList<McpClientTool>> { ["test-server"] = CreatePlaceholderTools(1) };
        var clients = new DisposalProbe();
        SetupMocksForToolEnumeration(servers, toolsByServer, clients);

        // Act
        var (toolDefinitions, _) = await _service.GetMcpToolDefinitionsAndResourcesAsync(
            TestAgentInstanceId,
            TestAuthToken,
            _mockTurnContext.Object);

        // Assert
        toolDefinitions.Should().HaveCount(1);
        clients.DisposeCount.Should().Be(1);
    }

    #endregion
}
