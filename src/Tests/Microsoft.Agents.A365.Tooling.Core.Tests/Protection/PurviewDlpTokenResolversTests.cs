// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Protection.Purview;
using Microsoft.Agents.Authentication;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests.Protection;

public class PurviewDlpTokenResolversTests
{
    private const string AgentId = "aaaaaaaa-0000-4000-8000-000000000001";
    private const string TenantId = "bbbbbbbb-0000-4000-8000-000000000002";
    private const string AgenticUserId = "dddddddd-0000-4000-8000-000000000004";

    [Fact]
    public async Task AsksTheConnectionForTheAgenticUsersTokenAndEvaluatesAsMe()
    {
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticUserTokenAsync(TenantId, AgentId, AgenticUserId, It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("agentic-user-token");
        var resolver = PurviewDlpTokenResolvers.FromAgenticUser(connection.Object);
        using var cancellation = new CancellationTokenSource();

        var token = await resolver(Agent(), PurviewDlpOptions.DefaultAuthenticationScope, cancellation.Token);

        token!.AccessToken.Should().Be("agentic-user-token");
        token.UserId.Should().BeNull("the agentic user's delegated token evaluates as /me");
        connection.Verify(
            provider => provider.GetAgenticUserTokenAsync(
                TenantId,
                AgentId,
                AgenticUserId,
                It.Is<IList<string>>(scopes => scopes.SequenceEqual(new[] { "https://graph.microsoft.com/.default" })),
                cancellation.Token),
            Times.Once);
    }

    [Theory]
    [InlineData(nameof(PurviewDlpAgentContext.TenantId))]
    [InlineData(nameof(PurviewDlpAgentContext.AgentId))]
    [InlineData(nameof(PurviewDlpAgentContext.AgenticUserId))]
    public async Task RequiresTheAgentTenantAndAgenticUser(string missing)
    {
        var connection = new Mock<IAgenticTokenProvider>();
        var agent = Agent();
        typeof(PurviewDlpAgentContext).GetProperty(missing)!.SetValue(agent, " ");
        var resolver = PurviewDlpTokenResolvers.FromAgenticUser(connection.Object);

        var act = () => resolver(agent, PurviewDlpOptions.DefaultAuthenticationScope, CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be(missing);
        connection.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ResolvesNoTokenWhenTheConnectionReturnsNone(string? issued)
    {
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issued!);

        var token = await PurviewDlpTokenResolvers.FromAgenticUser(connection.Object)(Agent(), "scope", CancellationToken.None);

        token.Should().BeNull();
    }

    [Fact]
    public async Task EvaluatesTheAgenticUserEndToEnd()
    {
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticUserTokenAsync(TenantId, AgentId, AgenticUserId, It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("agentic-user-token");
        var handler = new GraphHandler(_ => PurviewDlpClientTests.Graph(PurviewDlpClientTests.Clean));
        var client = new PurviewDlpClient(
            new PurviewDlpOptions { Enabled = true, GraphBaseUrl = new Uri("https://graph.example.test/v1.0") },
            new HttpClient(handler));

        var result = await client.EvaluateAsync(
            PurviewDlpActivity.UploadText,
            "hello",
            PurviewDlpClientTests.Agent(),
            PurviewDlpTokenResolvers.FromAgenticUser(connection.Object));

        result!.Evaluated.Should().BeTrue();
        var call = handler.Calls.Single();
        call.Uri.Should().Be(new Uri("https://graph.example.test/v1.0/me/dataSecurityAndGovernance/processContent"));
        call.Authorization.Should().Be("Bearer agentic-user-token");
    }

    [Fact]
    public async Task PassesTheScopeToTheHostsProviderAndEvaluatesForTheGivenUser()
    {
        var scopes = new List<string>();
        var resolver = PurviewDlpTokenResolvers.FromAccessTokenProvider(
            (scope, _) =>
            {
                scopes.Add(scope);
                return Task.FromResult("host-token");
            },
            "user@contoso.example");

        var token = await resolver(Agent(), "https://graph.microsoft.com/.default", CancellationToken.None);

        scopes.Should().Equal("https://graph.microsoft.com/.default");
        token!.AccessToken.Should().Be("host-token");
        token.UserId.Should().Be("user@contoso.example");
    }

    [Fact]
    public async Task EvaluatesAsMeWithoutAUser()
    {
        var resolver = PurviewDlpTokenResolvers.FromAccessTokenProvider((_, _) => Task.FromResult("obo-token"));

        var token = await resolver(Agent(), "scope", CancellationToken.None);

        token!.UserId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RejectsAnEmptyUser(string userId)
    {
        var create = () => PurviewDlpTokenResolvers.FromAccessTokenProvider((_, _) => Task.FromResult("token"), userId);
        var token = () => new PurviewDlpToken("token", userId);

        create.Should().Throw<ArgumentException>().WithParameterName("userId");
        token.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ResolvesNoTokenWhenTheProviderReturnsNone(string? issued)
    {
        var resolver = PurviewDlpTokenResolvers.FromAccessTokenProvider((_, _) => Task.FromResult(issued!));

        var token = await resolver(Agent(), "scope", CancellationToken.None);

        token.Should().BeNull();
    }

    [Fact]
    public void RequiresAnAccessToken()
    {
        var create = () => new PurviewDlpToken(" ");

        create.Should().Throw<ArgumentException>().WithParameterName("accessToken");
    }

    [Fact]
    public void CopiesTheContextWithASessionWithoutChangingIt()
    {
        var agent = Agent();
        agent.SessionId = "turn-session";
        agent.SequenceNumber = 4;

        var copy = agent.WithSession("hook-session", 9);
        var kept = agent.WithSession(null, null);

        copy.Should().BeEquivalentTo(new { AgentId, TenantId, AgenticUserId, SessionId = "hook-session", SequenceNumber = 9L });
        kept.Should().BeEquivalentTo(new { SessionId = "turn-session", SequenceNumber = 4L });
        agent.SessionId.Should().Be("turn-session");
        agent.SequenceNumber.Should().Be(4);
    }

    private static PurviewDlpAgentContext Agent() => new()
    {
        AgentId = AgentId,
        TenantId = TenantId,
        AgenticUserId = AgenticUserId,
    };
}
