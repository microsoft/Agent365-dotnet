// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// Tests for MCP client lifetime: <see cref="McpToolSet"/> and <see cref="McpToolServerConfigurationService.GetMcpToolSetAsync"/>.
/// </summary>
public sealed class McpToolSetTests : IDisposable
{
    private const string TurnToken = "token-of-this-call";
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    private readonly FakeMcpServer _server = new();
    private readonly Mock<McpToolServerConfigurationService> _service;
    private readonly Mock<ITurnContext> _turnContext = new();
    private readonly TurnContextStateCollection _stackState = new();

    public McpToolSetTests()
    {
        _turnContext.Setup(t => t.StackState).Returns(_stackState);

        _service = new Mock<McpToolServerConfigurationService>(
            MockBehavior.Default,
            new Mock<ILogger<IMcpToolServerConfigurationService>>().Object,
            new Mock<IConfiguration>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<IHttpClientFactory>().Object)
        { CallBase = true };
        _service.Object.McpHttpMessageHandlerFactory = _server.CreateHandler;
        _service
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync(() => new List<MCPServerConfig> { Server("mcp_MailTools"), Server("mcp_CalendarTools") });
    }

    public void Dispose() => _stackState.Dispose();

    private static MCPServerConfig Server(string name) =>
        new() { mcpServerName = name, id = $"id-{name}", url = $"http://mcp.test/agents/servers/{name}" };

    private Task<McpToolSet> GetToolSetAsync(CancellationToken cancellationToken = default) =>
        _service.Object.GetMcpToolSetAsync("agent-id", "discovery-token", new FixedTokenProvider(TurnToken), _turnContext.Object, new ToolOptions(), cancellationToken)
            .WaitAsync(s_timeout);

    [Fact]
    public async Task GetMcpToolSetAsync_DisposeAsync_ClosesStreamsEndsSessionsAndDisposesHandlers()
    {
        // Arrange
        var toolSet = await GetToolSetAsync();
        toolSet.ToolsByServer.Keys.Should().BeEquivalentTo("mcp_MailTools", "mcp_CalendarTools");
        toolSet.Should().HaveCount(2);
        (await FakeMcpServer.WaitUntilAsync(() => _server.OpenGetStreams == 2)).Should().BeTrue("each client opens a GET stream");

        // Act
        await toolSet.DisposeAsync().AsTask().WaitAsync(s_timeout);

        // Assert
        _server.OpenGetStreams.Should().Be(0);
        _server.DeletedSessions.Select(s => s.SessionId).Should().BeEquivalentTo(_server.CreatedSessions);
        _server.DeletedSessions.Select(s => s.Authorization).Should().AllBe($"Bearer {TurnToken}");
        _server.DisposedHandlers.Should().Be(2);
    }

    [Fact]
    public async Task GetMcpToolSetAsync_ToolsCallServerWithTokenOfThisCall_AndCantBeInvokedAfterDispose()
    {
        // Arrange
        var toolSet = await GetToolSetAsync();
        AIFunction tool = toolSet.ToolsByServer["mcp_MailTools"].Single();

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments()).AsTask().WaitAsync(s_timeout);
        await toolSet.DisposeAsync().AsTask().WaitAsync(s_timeout);
        var invokeAfterDispose = async () => await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        result.Should().BeOfType<JsonElement>().Which.GetRawText().Should().Contain($"Bearer {TurnToken}");
        await invokeAfterDispose.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task GetMcpToolSetAsync_ServerFailsToListTools_SkipsServerAndDisposesItsClient()
    {
        // Arrange
        _server.FailToolsListPath = "/agents/servers/mcp_CalendarTools";

        // Act
        var toolSet = await GetToolSetAsync();

        // Assert
        toolSet.ToolsByServer.Keys.Should().Equal("mcp_MailTools");
        toolSet.Servers.Should().HaveCount(2);
        _server.DisposedHandlers.Should().Be(1);
        await toolSet.DisposeAsync().AsTask().WaitAsync(s_timeout);
        _server.DisposedHandlers.Should().Be(2);
        _server.OpenGetStreams.Should().Be(0);
    }

    [Fact]
    public async Task GetMcpToolSetAsync_CancelledAfterOneServerResponded_DisposesEveryClientAndThrows()
    {
        // Arrange: the second server's tool listing waits until the call is cancelled.
        using var cancellation = new CancellationTokenSource();
        _server.ToolsListGate = new TaskCompletionSource().Task;
        _server.ToolsListGatePath = "/agents/servers/mcp_CalendarTools";
        var call = GetToolSetAsync(cancellation.Token);
        (await FakeMcpServer.WaitUntilAsync(() => _server.CreatedSessions.Count == 2)).Should().BeTrue();

        // Act
        cancellation.Cancel();

        // Assert
        await call.Awaiting(c => c.WaitAsync(s_timeout)).Should().ThrowAsync<OperationCanceledException>();
        _server.DisposedHandlers.Should().Be(2);
        _server.OpenGetStreams.Should().Be(0);
    }

    [Fact]
    public async Task GetMcpClientToolsAsync_ServerFailsToListTools_DisposesItsClient()
    {
        // Arrange
        var server = Server("mcp_MailTools");
        _server.FailToolsListPath = new Uri(server.url).AbsolutePath;

        // Act
        var act = () => _service.Object.GetMcpClientToolsAsync(_turnContext.Object, server, TurnToken, new ToolOptions()).WaitAsync(s_timeout);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _server.DisposedHandlers.Should().Be(1);
    }

    [Fact]
    public async Task McpToolSet_DisposeAsync_DisposesEachResourceOnceAndIgnoresErrors()
    {
        // Arrange
        var failing = new Mock<IAsyncDisposable>();
        failing.Setup(d => d.DisposeAsync()).ThrowsAsync(new InvalidOperationException("dispose failed"));
        var probe = new Mock<IAsyncDisposable>();
        var toolSet = new McpToolSet(
            Array.Empty<MCPServerConfig>(),
            new Dictionary<string, IReadOnlyList<McpClientTool>>(),
            new[] { failing.Object, probe.Object });

        // Act
        var act = async () =>
        {
            await toolSet.DisposeAsync();
            await toolSet.DisposeAsync();
        };

        // Assert
        await act.Should().NotThrowAsync();
        failing.Verify(d => d.DisposeAsync(), Times.Once);
        probe.Verify(d => d.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GetMcpToolSetAsync_DefaultInterfaceImplementation_ReturnsToolsFromEnumerateToolsFromServersAsync()
    {
        // Arrange: another implementation that only implements EnumerateToolsFromServersAsync.
        var servers = new List<MCPServerConfig> { Server("mcp_MailTools") };
        var tools = new List<McpClientTool>();
        var implementation = new Mock<IMcpToolServerConfigurationService> { CallBase = true };
        implementation
            .Setup(x => x.EnumerateToolsFromServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IMcpTokenProvider>(), It.IsAny<ITurnContext>(), It.IsAny<ToolOptions>()))
            .ReturnsAsync((servers, new Dictionary<string, IList<McpClientTool>> { ["mcp_MailTools"] = tools }));

        // Act
        var toolSet = await implementation.Object.GetMcpToolSetAsync("agent-id", "token", new FixedTokenProvider(TurnToken), _turnContext.Object, new ToolOptions());

        // Assert
        toolSet.Servers.Should().Equal(servers);
        toolSet.ToolsByServer.Keys.Should().Equal("mcp_MailTools");
    }

    private sealed class FixedTokenProvider(string token) : IMcpTokenProvider
    {
        public Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default) => Task.FromResult(token);
    }
}
