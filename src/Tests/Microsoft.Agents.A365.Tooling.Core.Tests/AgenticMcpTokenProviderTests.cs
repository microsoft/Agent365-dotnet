// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Utils;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// Tests for IMcpTokenProvider scope routing using <see cref="FakeTokenProvider"/>.
///
/// AgenticMcpTokenProvider requires a live UserAuthorization + OBO flow, so it is not
/// unit-testable in isolation. These tests verify the scope-routing logic that
/// AgenticMcpTokenProvider delegates to: <see cref="Utility.ResolveTokenScopeForServer"/>.
/// The integration between AgenticMcpTokenProvider and AgenticAuthenticationService is
/// covered by integration tests.
/// </summary>
public class AgenticMcpTokenProviderScopeRoutingTests
{
    private const string AtgAppId = Constants.Authentication.AtgAppId;
    private const string AtgScope = $"{AtgAppId}/.default";
    private const string V2AudienceId = "11111111-2222-3333-4444-555555555555";
    private const string V2Scope = $"{V2AudienceId}/.default";

    private static FakeTokenProvider MakeProvider(params (string scope, string token)[] map) =>
        new FakeTokenProvider(map);

    private static MCPServerConfig V1Server(string name = "v1") =>
        new() { mcpServerName = name, id = $"id-{name}", url = "http://v1" };

    private static MCPServerConfig V2Server(string name = "v2") =>
        new() { mcpServerName = name, id = $"id-{name}", url = "http://v2", audience = V2AudienceId };

    // ─── V1 routing ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Provider_V1Server_NullAudience_RoutesToAtgScope()
    {
        var provider = MakeProvider((AtgScope, "v1-token"));
        var token = await provider.GetTokenAsync(V1Server());
        token.Should().Be("v1-token");
    }

    [Fact]
    public async Task Provider_V1Server_EmptyAudience_RoutesToAtgScope()
    {
        var server = new MCPServerConfig { mcpServerName = "s", id = "i", url = "u", audience = "" };
        var provider = MakeProvider((AtgScope, "v1-token"));
        var token = await provider.GetTokenAsync(server);
        token.Should().Be("v1-token");
    }

    [Fact]
    public async Task Provider_V1Server_AtgAudienceId_RoutesToAtgScope()
    {
        var server = new MCPServerConfig
        {
            mcpServerName = "s", id = "i", url = "u", audience = AtgAppId
        };
        var provider = MakeProvider((AtgScope, "v1-token"));
        var token = await provider.GetTokenAsync(server);
        token.Should().Be("v1-token");
    }

    [Fact]
    public async Task Provider_V1Server_AtgAudienceIdUppercase_RoutesToAtgScope()
    {
        var server = new MCPServerConfig
        {
            mcpServerName = "s", id = "i", url = "u",
            audience = AtgAppId.ToUpperInvariant()
        };
        var provider = MakeProvider((AtgScope, "v1-token"));
        var token = await provider.GetTokenAsync(server);
        token.Should().Be("v1-token");
    }

    // ─── V2 routing ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Provider_V2Server_RoutesToPerAudienceScope()
    {
        var provider = MakeProvider((V2Scope, "v2-token"));
        var token = await provider.GetTokenAsync(V2Server());
        token.Should().Be("v2-token");
    }

    [Fact]
    public async Task Provider_TwoDifferentV2Audiences_RouteToSeparateScopes()
    {
        const string aud2 = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        const string scope2 = $"{aud2}/.default";
        var server2 = new MCPServerConfig { mcpServerName = "v2b", id = "i", url = "u", audience = aud2 };

        var provider = MakeProvider((V2Scope, "token-a"), (scope2, "token-b"));

        var a = await provider.GetTokenAsync(V2Server());
        var b = await provider.GetTokenAsync(server2);

        a.Should().Be("token-a");
        b.Should().Be("token-b");
    }

    [Fact]
    public async Task Provider_ApiPrefixedAudience_RoutesToPerAudienceScope()
    {
        // Arrange — gateway may send audience already as "api://<guid>"; /.default is appended, not doubled.
        const string apiPrefixedAudience = $"api://{V2AudienceId}";
        const string expectedScope = $"{apiPrefixedAudience}/.default";
        var server = new MCPServerConfig { mcpServerName = "s", id = "i", url = "u", audience = apiPrefixedAudience };

        var provider = MakeProvider((expectedScope, "api-prefixed-token"));
        var token = await provider.GetTokenAsync(server);
        token.Should().Be("api-prefixed-token");
    }

    // ─── V2 audience + scope ─────────────────────────────────────────────────

    [Fact]
    public async Task Provider_V2AudienceAndScope_ReturnsCombinedScope()
    {
        // The gateway returns scope as a bare permission name; full OAuth scope = {audience}/{scope}.
        const string permission = "Tools.ListInvoke.All";
        const string combinedScope = $"{V2AudienceId}/{permission}";
        var server = new MCPServerConfig
        {
            mcpServerName = "s", id = "i", url = "u",
            audience = V2AudienceId,
            scope = permission
        };
        var provider = MakeProvider((combinedScope, "v2-scoped-token"));
        var token = await provider.GetTokenAsync(server);
        token.Should().Be("v2-scoped-token");
    }

    [Fact]
    public async Task Provider_V1ServerWithScope_ScopeIgnored_RoutesToAtgScope()
    {
        // V1 servers may have a scope field (e.g. "McpServers.Calendar.All") but it is ignored;
        // the ATG shared token is used instead.
        var server = new MCPServerConfig
        {
            mcpServerName = "s", id = "i", url = "u",
            scope = "McpServers.Calendar.All"  // V1 permission name — ignored
        };
        var provider = MakeProvider((AtgScope, "v1-token"));
        var token = await provider.GetTokenAsync(server);
        token.Should().Be("v1-token");
    }

    // ─── Token deduplication via TrackingTokenProvider ───────────────────────

    [Fact]
    public async Task TrackingProvider_MultipleV1Calls_CallsProviderEachTime()
    {
        // TrackingTokenProvider has no built-in cache; deduplication lives in
        // AttachPerAudienceTokensAsync (which dedupes by scope before calling the provider).
        // This test verifies TrackingTokenProvider tracks call counts faithfully.
        var tracking = new TrackingTokenProvider(AtgScope, "shared-v1");

        await tracking.GetTokenAsync(V1Server("a"));
        await tracking.GetTokenAsync(V1Server("b"));
        await tracking.GetTokenAsync(V1Server("c"));

        tracking.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task TrackingProvider_V1AndV2Calls_TwoExchanges()
    {
        // Use FakeTokenProvider (which also has per-scope caching via Utility) for mixed scenario
        var provider = MakeProvider((AtgScope, "v1"), (V2Scope, "v2"));

        var t1 = await provider.GetTokenAsync(V1Server());
        var t2 = await provider.GetTokenAsync(V2Server());
        var t3 = await provider.GetTokenAsync(V1Server("v1-again")); // same scope → cached

        t1.Should().Be("v1");
        t2.Should().Be("v2");
        t3.Should().Be("v1");
    }

    // ─── Cancellation ────────────────────────────────────────────────────────

    [Fact]
    public async Task Provider_CancelledToken_ThrowsOperationCancelled()
    {
        var provider = MakeProvider((AtgScope, "tok"));
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => provider.GetTokenAsync(V1Server(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
