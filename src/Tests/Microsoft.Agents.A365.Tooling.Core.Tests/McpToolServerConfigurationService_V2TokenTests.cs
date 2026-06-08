// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Utils;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Moq;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// Tests for V1/V2 per-audience token attachment and the token-provider overload of
/// EnumerateToolsFromServersAsync.
/// </summary>
public class McpToolServerConfigurationService_V2TokenTests
{
    private const string AtgAppId = Constants.Authentication.AtgAppId;
    private const string V2AudienceId = "11111111-2222-3333-4444-555555555555";
    private const string SharedAuthToken = "shared-atg-token";

    private readonly Mock<ILogger<IMcpToolServerConfigurationService>> _logger;
    private readonly Mock<IConfiguration> _configuration;
    private readonly Mock<ITurnContext> _turnContext;
    private readonly Mock<McpToolServerConfigurationService> _service;

    public McpToolServerConfigurationService_V2TokenTests()
    {
        _logger = new Mock<ILogger<IMcpToolServerConfigurationService>>();
        _configuration = new Mock<IConfiguration>();
        _turnContext = new Mock<ITurnContext>();

        _service = new Mock<McpToolServerConfigurationService>(
            MockBehavior.Default,
            _logger.Object,
            _configuration.Object,
            new Mock<IServiceProvider>().Object,
            new Mock<IHttpClientFactory>().Object)
        { CallBase = true };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static MCPServerConfig V1Server(string name = "v1") =>
        new() { mcpServerName = name, id = $"id-{name}", url = "http://v1" };

    private static MCPServerConfig V2Server(string name = "v2") =>
        new() { mcpServerName = name, id = $"id-{name}", url = "http://v2", audience = V2AudienceId };

    private static FakeTokenProvider TokenProvider(params (string scope, string token)[] mappings) =>
        new FakeTokenProvider(mappings);

    private void SetupListServers(IEnumerable<MCPServerConfig> servers) =>
        _service
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(servers.ToList());

    // ─── AttachPerAudienceTokens (via ListToolServersWithTokensAsync) ─────────

    [Fact]
    public async Task ListToolServersWithTokensAsync_V1Server_AttachesAtgToken()
    {
        // Arrange
        var server = V1Server();
        SetupListServers([server]);
        var atgScope = $"{AtgAppId}/.default";
        var provider = TokenProvider((atgScope, "v1-bearer-token"));

        // Act
        var result = await _service.Object.ListToolServersWithTokensAsync(
            "agent-id", SharedAuthToken, provider, new ToolOptions());

        // Assert
        result.Should().HaveCount(1);
        var s = result[0];
        s.Headers.Should().ContainKey("Authorization");
        s.Headers!["Authorization"].Should().Be("Bearer v1-bearer-token");
    }

    [Fact]
    public async Task ListToolServersWithTokensAsync_V2Server_AttachesPerAudienceToken()
    {
        // Arrange
        var server = V2Server();
        SetupListServers([server]);
        var v2Scope = $"{V2AudienceId}/.default";
        var provider = TokenProvider((v2Scope, "v2-bearer-token"));

        // Act
        var result = await _service.Object.ListToolServersWithTokensAsync(
            "agent-id", SharedAuthToken, provider, new ToolOptions());

        // Assert
        var s = result[0];
        s.Headers!["Authorization"].Should().Be("Bearer v2-bearer-token");
    }

    [Fact]
    public async Task ListToolServersWithTokensAsync_MixedServers_AttachesCorrectTokensToEach()
    {
        // Arrange
        var v1 = V1Server("v1");
        var v2 = V2Server("v2");
        SetupListServers([v1, v2]);

        var atgScope = $"{AtgAppId}/.default";
        var v2Scope = $"{V2AudienceId}/.default";
        var provider = TokenProvider((atgScope, "token-v1"), (v2Scope, "token-v2"));

        // Act
        var result = await _service.Object.ListToolServersWithTokensAsync(
            "agent-id", SharedAuthToken, provider, new ToolOptions());

        // Assert — each server gets its own token
        var v1Result = result.First(s => s.mcpServerName == "v1");
        var v2Result = result.First(s => s.mcpServerName == "v2");

        v1Result.Headers!["Authorization"].Should().Be("Bearer token-v1");
        v2Result.Headers!["Authorization"].Should().Be("Bearer token-v2");
    }

    [Fact]
    public async Task ListToolServersWithTokensAsync_MultipleV1Servers_ProviderCalledOnceForAtgScope()
    {
        // Arrange — three V1 servers should share one OBO exchange
        SetupListServers([V1Server("v1a"), V1Server("v1b"), V1Server("v1c")]);
        var atgScope = $"{AtgAppId}/.default";
        var trackingProvider = new TrackingTokenProvider(atgScope, "shared-token");

        // Act
        await _service.Object.ListToolServersWithTokensAsync(
            "agent-id", SharedAuthToken, trackingProvider, new ToolOptions());

        // Assert — provider was asked for the same scope three times but only invoked once (cached)
        trackingProvider.CallCount.Should().Be(1);
    }

    // ─── ResolveEffectiveToken ────────────────────────────────────────────────

    [Fact]
    public void ResolveEffectiveToken_ServerWithBearerHeader_ReturnsStrippedToken()
    {
        // Arrange — server carries an Authorization header set by AttachPerAudienceTokensAsync
        var server = V2Server();
        server.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = "Bearer per-server-token"
        };

        // Act
        var token = McpToolServerConfigurationService.ResolveEffectiveToken(server, "fallback");

        // Assert — "Bearer " prefix is stripped; the per-server credential is used
        token.Should().Be("per-server-token");
    }

    [Fact]
    public void ResolveEffectiveToken_ServerWithRawTokenHeader_ReturnsHeaderValueAsIs()
    {
        // Arrange — header value has no "Bearer " prefix (raw token string)
        var server = V1Server();
        server.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = "raw-token-no-prefix"
        };

        // Act
        var token = McpToolServerConfigurationService.ResolveEffectiveToken(server, "fallback");

        // Assert — returned verbatim; fallback is not used
        token.Should().Be("raw-token-no-prefix");
    }

    [Fact]
    public void ResolveEffectiveToken_ServerWithNullHeaders_ReturnsFallback()
    {
        // Arrange — V1 server: no headers attached (AttachPerAudienceTokensAsync was not called)
        var server = V1Server();
        server.Headers = null;

        // Act
        var token = McpToolServerConfigurationService.ResolveEffectiveToken(server, "fallback-token");

        // Assert
        token.Should().Be("fallback-token");
    }

    [Fact]
    public void ResolveEffectiveToken_ServerWithNoAuthorizationKey_ReturnsFallback()
    {
        // Arrange — headers dict exists but has no Authorization entry
        var server = V1Server();
        server.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["User-Agent"] = "some-agent"
        };

        // Act
        var token = McpToolServerConfigurationService.ResolveEffectiveToken(server, "fallback-token");

        // Assert
        token.Should().Be("fallback-token");
    }

    [Fact]
    public void ResolveEffectiveToken_ServerWithEmptyAuthorizationHeader_ReturnsFallback()
    {
        // Arrange — header key present but value is whitespace
        var server = V1Server();
        server.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = "   "
        };

        // Act
        var token = McpToolServerConfigurationService.ResolveEffectiveToken(server, "fallback-token");

        // Assert — empty/whitespace value treated as absent; fallback applies
        token.Should().Be("fallback-token");
    }

    // ─── EnumerateToolsFromServersAsync with token provider ─────────────────

    [Fact]
    public async Task EnumerateToolsFromServersAsync_WithTokenProvider_AttachesTokensBeforeEnumerating()
    {
        // Arrange
        var v1 = V1Server("v1");
        var v2 = V2Server("v2");
        SetupListServers([v1, v2]);

        var atgScope = $"{AtgAppId}/.default";
        var v2Scope = $"{V2AudienceId}/.default";
        var provider = TokenProvider((atgScope, "tok-v1"), (v2Scope, "tok-v2"));

        MCPServerConfig? capturedV1 = null;
        MCPServerConfig? capturedV2 = null;

        _service
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "v1"),
                It.IsAny<string>(), It.IsAny<ToolOptions>(), It.IsAny<CancellationToken>()))
            .Callback<ITurnContext, MCPServerConfig, string, ToolOptions, CancellationToken>((_, s, _, _, _) => capturedV1 = s)
            .ReturnsAsync(new List<McpClientTool>());

        _service
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "v2"),
                It.IsAny<string>(), It.IsAny<ToolOptions>(), It.IsAny<CancellationToken>()))
            .Callback<ITurnContext, MCPServerConfig, string, ToolOptions, CancellationToken>((_, s, _, _, _) => capturedV2 = s)
            .ReturnsAsync(new List<McpClientTool>());

        // Act
        var (servers, toolsByServer) = await _service.Object.EnumerateToolsFromServersAsync(
            "agent-id", SharedAuthToken, provider, _turnContext.Object, new ToolOptions());

        // Assert — by the time GetMcpClientToolsAsync was called, headers were already set
        capturedV1.Should().NotBeNull();
        capturedV1!.Headers.Should().ContainKey("Authorization");
        capturedV1.Headers!["Authorization"].Should().Be("Bearer tok-v1");

        capturedV2.Should().NotBeNull();
        capturedV2!.Headers.Should().ContainKey("Authorization");
        capturedV2.Headers!["Authorization"].Should().Be("Bearer tok-v2");

        toolsByServer.Should().ContainKey("v1");
        toolsByServer.Should().ContainKey("v2");
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_WithTokenProvider_WhenListFails_ReturnsEmpty()
    {
        // Arrange
        _service
            .Setup(x => x.ListToolServersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("gateway unreachable"));
        var provider = TokenProvider();

        // Act
        var (servers, toolsByServer) = await _service.Object.EnumerateToolsFromServersAsync(
            "agent-id", SharedAuthToken, provider, _turnContext.Object, new ToolOptions());

        // Assert — graceful empty result, not an exception
        servers.Should().BeEmpty();
        toolsByServer.Should().BeEmpty();
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_WithTokenProvider_SkipsServersWithMissingName()
    {
        // Arrange
        SetupListServers([
            new MCPServerConfig { mcpServerName = null!, id = "id", url = "http://x" },
            V1Server("valid")
        ]);
        var atgScope = $"{AtgAppId}/.default";
        var provider = TokenProvider((atgScope, "tok"));

        _service
            .Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(),
                It.Is<MCPServerConfig>(s => s.mcpServerName == "valid"),
                It.IsAny<string>(), It.IsAny<ToolOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<McpClientTool>());

        // Act
        var (_, toolsByServer) = await _service.Object.EnumerateToolsFromServersAsync(
            "agent-id", SharedAuthToken, provider, _turnContext.Object, new ToolOptions());

        // Assert
        toolsByServer.Should().ContainKey("valid");
        toolsByServer.Should().HaveCount(1);
    }

    // ─── Legacy overload V2 guard ────────────────────────────────────────────

    [Fact]
    public async Task EnumerateToolsFromServersAsync_LegacyPath_V2Server_ThrowsWithMigrationHint()
    {
        // The no-tokenProvider overload cannot perform per-audience OBO; it must throw instead of
        // silently attaching the wrong shared ATG token to a V2 server.
        var productionConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Production" })
            .Build();
        var service = new Mock<McpToolServerConfigurationService>(
            MockBehavior.Default,
            _logger.Object,
            productionConfig,
            new Mock<IServiceProvider>().Object,
            new Mock<IHttpClientFactory>().Object) { CallBase = true };

        service.Setup(x => x.ListToolServersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MCPServerConfig> { V2Server("mail") });

        var act = () => service.Object.EnumerateToolsFromServersAsync(
            "agent-id", SharedAuthToken, _turnContext.Object, new ToolOptions());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*'mail'*require per-audience tokens*");
    }

    [Fact]
    public async Task EnumerateToolsFromServersAsync_LegacyPath_ApiPrefixedAtgAudience_DoesNotThrow()
    {
        // Regression: a gateway that returns the ATG app ID in "api://<guid>" form must still be
        // treated as V1 — the guard should NOT fire and tool enumeration should proceed normally.
        var productionConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Production" })
            .Build();
        var service = new Mock<McpToolServerConfigurationService>(
            MockBehavior.Default,
            _logger.Object,
            productionConfig,
            new Mock<IServiceProvider>().Object,
            new Mock<IHttpClientFactory>().Object) { CallBase = true };

        var v1WithApiAudience = new MCPServerConfig
        {
            mcpServerName = "v1server", id = "id1", url = "http://v1",
            audience = $"api://{AtgAppId}"  // equivalent ATG audience form
        };
        service.Setup(x => x.ListToolServersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ToolOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MCPServerConfig> { v1WithApiAudience });
        service.Setup(x => x.GetMcpClientToolsAsync(
                It.IsAny<ITurnContext>(), It.IsAny<MCPServerConfig>(),
                It.IsAny<string>(), It.IsAny<ToolOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<McpClientTool>());

        // Act — should NOT throw; api://<AtgAppId> is a V1 server
        var act = () => service.Object.EnumerateToolsFromServersAsync(
            "agent-id", SharedAuthToken, _turnContext.Object, new ToolOptions());

        await act.Should().NotThrowAsync();
    }

    // ─── MCPServerConfig.Headers model ───────────────────────────────────────

    [Fact]
    public void MCPServerConfig_Headers_DefaultsToNull()
    {
        var server = V1Server();
        server.Headers.Should().BeNull();
    }

    [Fact]
    public void MCPServerConfig_NullableFields_DefaultToNull()
    {
        var server = new MCPServerConfig { mcpServerName = "s", id = "i", url = "u" };
        server.scope.Should().BeNull();
        server.audience.Should().BeNull();
        server.publisher.Should().BeNull();
        server.Headers.Should().BeNull();
    }

    [Fact]
    public void MCPServerConfig_AllFieldsCanBeSet()
    {
        var server = new MCPServerConfig
        {
            mcpServerName = "s", id = "i", url = "u",
            scope = "sc", audience = "au", publisher = "pub",
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer tok" }
        };
        server.scope.Should().Be("sc");
        server.audience.Should().Be("au");
        server.publisher.Should().Be("pub");
        server.Headers["Authorization"].Should().Be("Bearer tok");
    }
}

/// <summary>
/// Simple fake IMcpTokenProvider that returns pre-set tokens by scope.
/// </summary>
internal sealed class FakeTokenProvider : IMcpTokenProvider
{
    private readonly Dictionary<string, string> _map;

    public FakeTokenProvider(params (string scope, string token)[] mappings)
    {
        _map = mappings.ToDictionary(m => m.scope, m => m.token, StringComparer.OrdinalIgnoreCase);
    }

    public Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = Microsoft.Agents.A365.Tooling.Utils.Utility.ResolveTokenScopeForServer(
            server, new ConfigurationBuilder().Build());
        return Task.FromResult(_map.TryGetValue(scope, out var tok) ? tok : "default-token");
    }
}

/// <summary>
/// Token provider that tracks how many OBO exchanges were made (cache dedup verification).
/// Returns the same token for a single scope; fails on unexpected scopes.
/// </summary>
internal sealed class TrackingTokenProvider : IMcpTokenProvider
{
    private readonly string _expectedScope;
    private readonly string _token;
    private int _callCount;

    public int CallCount => _callCount;

    public TrackingTokenProvider(string expectedScope, string token)
    {
        _expectedScope = expectedScope;
        _token = token;
    }

    public Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = Microsoft.Agents.A365.Tooling.Utils.Utility.ResolveTokenScopeForServer(
            server, new ConfigurationBuilder().Build());
        if (!string.Equals(scope, _expectedScope, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unexpected scope '{scope}'");
        Interlocked.Increment(ref _callCount);
        return Task.FromResult(_token);
    }
}
