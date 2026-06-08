// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// Tests for <see cref="EnvMcpTokenProvider"/> and <see cref="Utility.IsDevScenario"/>.
/// </summary>
public class EnvMcpTokenProviderTests
{
    private static MCPServerConfig Server(string name) =>
        new() { mcpServerName = name, id = $"id-{name}", url = "http://test" };

    private static EnvMcpTokenProvider Provider(IConfiguration config) =>
        new(config, Mock.Of<ILogger>());

    private static IConfiguration Config(params (string key, string value)[] entries)
    {
        var dict = entries.ToDictionary(e => e.key, e => e.value);
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    // ─── Per-server token ────────────────────────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_PerServerEnvVarSet_ReturnsThatToken()
    {
        var config = Config(("BEARER_TOKEN_MCP_MAILTOOLS", "mail-token"));
        var token = await Provider(config).GetTokenAsync(Server("mcp_MailTools"));
        token.Should().Be("mail-token");
    }

    [Fact]
    public async Task GetTokenAsync_PerServerEnvVar_UsesUpperInvariantNormalization()
    {
        // Server name upper-cased: "mcp_calendartools" → "BEARER_TOKEN_MCP_CALENDARTOOLS"
        var config = Config(("BEARER_TOKEN_MCP_CALENDARTOOLS", "cal-token"));
        var token = await Provider(config).GetTokenAsync(Server("mcp_CalendarTools"));
        token.Should().Be("cal-token");
    }

    [Fact]
    public async Task GetTokenAsync_PerServerEnvVar_NormalizesHyphensToUnderscores()
    {
        // Server name "my-mcp-server" → "BEARER_TOKEN_MY_MCP_SERVER"
        var config = Config(("BEARER_TOKEN_MY_MCP_SERVER", "hyphen-token"));
        var token = await Provider(config).GetTokenAsync(Server("my-mcp-server"));
        token.Should().Be("hyphen-token");
    }

    [Fact]
    public async Task GetTokenAsync_PerServerEnvVar_TakesPriorityOverSharedFallback()
    {
        var config = Config(
            ("BEARER_TOKEN_MCP_MAILTOOLS", "per-server-token"),
            ("BEARER_TOKEN", "shared-fallback-token"));

        var token = await Provider(config).GetTokenAsync(Server("mcp_MailTools"));
        token.Should().Be("per-server-token");
    }

    // ─── Shared fallback token ────────────────────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_PerServerVarAbsent_FallsBackToSharedBearerToken()
    {
        var config = Config(("BEARER_TOKEN", "shared-token"));
        var token = await Provider(config).GetTokenAsync(Server("mcp_MailTools"));
        token.Should().Be("shared-token");
    }

    [Fact]
    public async Task GetTokenAsync_MultipleServers_SharedFallbackUsedForAll()
    {
        var config = Config(("BEARER_TOKEN", "shared-token"));
        var provider = Provider(config);

        var t1 = await provider.GetTokenAsync(Server("ServerA"));
        var t2 = await provider.GetTokenAsync(Server("ServerB"));

        t1.Should().Be("shared-token");
        t2.Should().Be("shared-token");
    }


    [Fact]
    public async Task GetTokenAsync_PerServerVarWhitespaceOnly_FallsBackToSharedToken()
    {
        // Whitespace-only value is treated as absent by IsNullOrWhiteSpace check
        var config = Config(
            ("BEARER_TOKEN_MCP_MAILTOOLS", "   "),
            ("BEARER_TOKEN", "shared-token"));

        var token = await Provider(config).GetTokenAsync(Server("mcp_MailTools"));
        token.Should().Be("shared-token");
    }


    // ─── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_CancelledToken_ThrowsOperationCancelled()
    {
        var config = Config(("BEARER_TOKEN", "tok"));
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => Provider(config).GetTokenAsync(Server("s"), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

/// <summary>
/// Tests for <see cref="Utility.IsDevScenario"/>.
/// </summary>
public class IsDevScenarioTests
{
    private static IConfiguration Config(params (string key, string value)[] entries)
    {
        var dict = entries.ToDictionary(e => e.key, e => e.value);
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    [Fact]
    public void IsDevScenario_AspNetCoreEnvironmentDevelopment_ReturnsTrue()
    {
        var config = Config(("ASPNETCORE_ENVIRONMENT", "Development"));
        Utility.IsDevScenario(config).Should().BeTrue();
    }

    [Fact]
    public void IsDevScenario_AspNetCoreEnvironmentProduction_ReturnsFalse()
    {
        var config = Config(("ASPNETCORE_ENVIRONMENT", "Production"));
        Utility.IsDevScenario(config).Should().BeFalse();
    }

    [Fact]
    public void IsDevScenario_AspNetCoreEnvironmentStaging_ReturnsFalse()
    {
        var config = Config(("ASPNETCORE_ENVIRONMENT", "Staging"));
        Utility.IsDevScenario(config).Should().BeFalse();
    }

    [Fact]
    public void IsDevScenario_DotNetEnvironmentProduction_ReturnsFalse()
    {
        // Only DOTNET_ENVIRONMENT set (no ASPNETCORE_ENVIRONMENT)
        var config = Config(("DOTNET_ENVIRONMENT", "Production"));
        Utility.IsDevScenario(config).Should().BeFalse();
    }

    [Fact]
    public void IsDevScenario_DotNetEnvironmentDevelopment_ReturnsTrue()
    {
        var config = Config(("DOTNET_ENVIRONMENT", "Development"));
        Utility.IsDevScenario(config).Should().BeTrue();
    }

    [Fact]
    public void IsDevScenario_AspNetCoreTakesPriorityOverDotNet()
    {
        // ASPNETCORE_ENVIRONMENT wins even when DOTNET_ENVIRONMENT says Production
        var config = Config(
            ("ASPNETCORE_ENVIRONMENT", "Development"),
            ("DOTNET_ENVIRONMENT", "Production"));
        Utility.IsDevScenario(config).Should().BeTrue();
    }

    [Fact]
    public void IsDevScenario_NoEnvironmentSet_ReturnsFalse()
    {
        // Unset environment must NOT default to Development so that hosts without an
        // explicit ASPNETCORE_ENVIRONMENT / DOTNET_ENVIRONMENT are not silently treated
        // as dev (which would enable manifest discovery, EnvMcpTokenProvider, and relaxed TLS).
        var config = Config(); // nothing set
        Utility.IsDevScenario(config).Should().BeFalse();
    }

    [Theory]
    [InlineData("development")]
    [InlineData("DEVELOPMENT")]
    [InlineData("Development")]
    public void IsDevScenario_DevelopmentCaseInsensitive_ReturnsTrue(string value)
    {
        var config = Config(("ASPNETCORE_ENVIRONMENT", value));
        Utility.IsDevScenario(config).Should().BeTrue();
    }
}
