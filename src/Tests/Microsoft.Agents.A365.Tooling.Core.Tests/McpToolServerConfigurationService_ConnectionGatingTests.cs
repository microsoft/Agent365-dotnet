// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// Tests for MCP connection gating: gateway response parsing (wrapped + legacy), aggregate/per-server
/// connection metadata, and the <see cref="McpConnectionsRequiredException"/> raised by
/// <c>ListToolServersAsync</c> when configured servers are not connection-ready.
/// </summary>
public class McpToolServerConfigurationService_ConnectionGatingTests
{
    private const string WrappedPendingJson = """
    {
      "mcpServers": [
        {
          "mcpServerName": "mcp_Salesforce",
          "id": "id-1",
          "url": "https://s/mcp_Salesforce",
          "scope": "McpServers.Salesforce.All",
          "audience": "",
          "allConnectionsUrl": "https://all/sf",
          "missingConnectionsUrl": "https://missing/sf",
          "connectivityStatus": "Pending"
        }
      ],
      "allConnectionsUrl": "https://all",
      "missingConnectionsUrl": "https://missing",
      "connectivityStatus": "Pending"
    }
    """;

    private const string WrappedReadyJson = """
    {
      "mcpServers": [
        {
          "mcpServerName": "s",
          "id": "id-1",
          "url": "https://s",
          "allConnectionsUrl": "https://all",
          "missingConnectionsUrl": "https://missing",
          "connectivityStatus": "Ready"
        }
      ],
      "allConnectionsUrl": "https://all",
      "missingConnectionsUrl": "https://missing",
      "connectivityStatus": "Ready"
    }
    """;

    private const string LegacyArrayJson = """
    [ { "mcpServerName": "s", "id": "id-1", "url": "https://s" } ]
    """;

    // ─── ParseGatewayResponse ────────────────────────────────────────────────

    [Fact]
    public void ParseGatewayResponse_Wrapped_ParsesServersAndAggregateMetadata()
    {
        var result = ParseGateway(WrappedPendingJson);

        var server = result.Servers.Should().ContainSingle().Subject;
        server.mcpServerName.Should().Be("mcp_Salesforce");
        server.allConnectionsUrl.Should().Be("https://all/sf");
        server.missingConnectionsUrl.Should().Be("https://missing/sf");
        server.connectivityStatus.Should().Be("Pending");

        result.AllConnectionsUrl.Should().Be("https://all");
        result.MissingConnectionsUrl.Should().Be("https://missing");
        result.ConnectivityStatus.Should().Be("Pending");
    }

    [Fact]
    public void ParseGatewayResponse_Ready_NullsMissingConnectionsUrl()
    {
        var result = ParseGateway(WrappedReadyJson);

        var server = result.Servers.Should().ContainSingle().Subject;
        server.connectivityStatus.Should().Be("Ready");
        server.missingConnectionsUrl.Should().BeNull();

        result.ConnectivityStatus.Should().Be("Ready");
        result.MissingConnectionsUrl.Should().BeNull();
        result.AllConnectionsUrl.Should().Be("https://all");
    }

    [Fact]
    public void ParseGatewayResponse_LegacyArray_ReturnsServersWithoutAggregate()
    {
        var result = ParseGateway(LegacyArrayJson);

        result.Servers.Should().ContainSingle();
        result.ConnectivityStatus.Should().BeNull();
        result.AllConnectionsUrl.Should().BeNull();
        result.MissingConnectionsUrl.Should().BeNull();
    }

    [Fact]
    public void ParseGatewayResponse_EmptyMcpServers_ReturnsEmptyWithAggregate()
    {
        var result = ParseGateway("""{ "mcpServers": [], "connectivityStatus": "Ready" }""");

        result.Servers.Should().BeEmpty();
        result.ConnectivityStatus.Should().Be("Ready");
    }

    [Fact]
    public void ParseGatewayResponse_UnexpectedStructure_Throws()
    {
        Action act = () => ParseGateway("""{ "foo": 1 }""");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Unexpected JSON structure*");
    }

    // ─── EnforceConnectionReadiness ──────────────────────────────────────────

    [Fact]
    public void EnforceConnectionReadiness_Pending_ThrowsWithDetails()
    {
        var service = CreateService();
        var discovery = new McpDiscoveryResult(
            new List<MCPServerConfig>
            {
                new() { mcpServerName = "pending-srv", id = "id-1", url = "http://p", connectivityStatus = "Pending" },
                new() { mcpServerName = "ready-srv", id = "id-2", url = "http://r", connectivityStatus = "Ready" },
            },
            allConnectionsUrl: "https://all",
            missingConnectionsUrl: "https://missing",
            connectivityStatus: "Pending");

        var act = () => service.EnforceConnectionReadiness(discovery);

        var ex = act.Should().Throw<McpConnectionsRequiredException>().Which;
        ex.ConnectivityStatus.Should().Be("Pending");
        ex.MissingConnectionsUrl.Should().Be("https://missing");
        ex.ServerNames.Should().ContainSingle().Which.Should().Be("pending-srv");
    }

    [Fact]
    public void EnforceConnectionReadiness_Ready_DoesNotThrow()
    {
        var service = CreateService();
        var discovery = new McpDiscoveryResult(new List<MCPServerConfig>(), connectivityStatus: "Ready");

        var act = () => service.EnforceConnectionReadiness(discovery);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnforceConnectionReadiness_NullStatus_DoesNotThrow()
    {
        var service = CreateService();
        var discovery = new McpDiscoveryResult(new List<MCPServerConfig>(), connectivityStatus: null);

        var act = () => service.EnforceConnectionReadiness(discovery);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnforceConnectionReadiness_UnknownStatus_Throws()
    {
        var service = CreateService();
        var discovery = new McpDiscoveryResult(new List<MCPServerConfig>(), connectivityStatus: "Frozen");

        var act = () => service.EnforceConnectionReadiness(discovery);

        act.Should().Throw<McpConnectionsRequiredException>()
            .Which.ConnectivityStatus.Should().Be("Frozen");
    }

    // ─── ListToolServersAsync (end-to-end via gateway) ───────────────────────

    [Fact]
    public async Task ListToolServersAsync_PendingGateway_ThrowsConnectionsRequired()
    {
        var service = CreateService(Respond(WrappedPendingJson));

        var ex = await Assert.ThrowsAsync<McpConnectionsRequiredException>(
            () => service.ListToolServersAsync("agent-123", "tok", new ToolOptions()));

        ex.ConnectivityStatus.Should().Be("Pending");
        ex.MissingConnectionsUrl.Should().Be("https://missing");
        ex.ServerNames.Should().Contain("mcp_Salesforce");
    }

    [Fact]
    public async Task ListToolServersAsync_ReadyGateway_ReturnsServers()
    {
        var service = CreateService(Respond(WrappedReadyJson));

        var servers = await service.ListToolServersAsync("agent-123", "tok", new ToolOptions());

        var server = servers.Should().ContainSingle().Subject;
        server.connectivityStatus.Should().Be("Ready");
        server.missingConnectionsUrl.Should().BeNull();
        server.allConnectionsUrl.Should().Be("https://all");
    }

    [Fact]
    public async Task ListToolServersAsync_LegacyArrayGateway_ReturnsServersWithoutGating()
    {
        var service = CreateService(Respond(LegacyArrayJson));

        var servers = await service.ListToolServersAsync("agent-123", "tok", new ToolOptions());

        servers.Should().ContainSingle();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static McpDiscoveryResult ParseGateway(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return McpToolServerConfigurationService.ParseGatewayResponse(doc.RootElement);
    }

    private static FakeHttpMessageHandler Respond(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

    private static McpToolServerConfigurationService CreateService(FakeHttpMessageHandler? handler = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["MCP_PLATFORM_ENDPOINT"]).Returns("https://test.endpoint");

        handler ??= Respond("[]");
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        return new McpToolServerConfigurationService(
            new Mock<ILogger<IMcpToolServerConfigurationService>>().Object,
            config.Object,
            new Mock<IServiceProvider>().Object,
            factory.Object);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
