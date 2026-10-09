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
                It.IsAny<CancellationToken>()),
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

    [Fact]
    public async Task NeverCachesTheHostsTokens()
    {
        var issued = 0;
        var resolver = PurviewDlpTokenResolvers.FromAccessTokenProvider((_, _) => Task.FromResult(Jwt(DateTimeOffset.UtcNow.AddHours(1), ++issued)));

        var first = await resolver(Agent(), "scope", CancellationToken.None);
        var second = await resolver(Agent(), "scope", CancellationToken.None);

        issued.Should().Be(2, "a host token may be for a user the agent context does not identify");
        second!.AccessToken.Should().NotBe(first!.AccessToken);
    }

    [Fact]
    public async Task CachesTheAgenticUsersTokenPerUserUntilItExpires()
    {
        var clock = new ManualClock();
        var issued = new List<string>();
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string user, IList<string> _, CancellationToken _) =>
            {
                issued.Add(user);
                return Jwt(clock.Now.AddHours(1), issued.Count);
            });
        var resolver = PurviewDlpTokenResolvers.CreateAgenticUserResolver(connection.Object, clock, TimeSpan.FromSeconds(30));
        var other = Agent();
        other.AgenticUserId = "eeeeeeee-0000-4000-8000-000000000005";

        var first = await resolver(Agent(), "scope", CancellationToken.None);
        var again = await resolver(Agent(), "scope", CancellationToken.None);
        var otherUser = await resolver(other, "scope", CancellationToken.None);
        var otherScope = await resolver(Agent(), "other-scope", CancellationToken.None);
        clock.Now += TimeSpan.FromHours(2);
        var afterExpiry = await resolver(Agent(), "scope", CancellationToken.None);

        again!.AccessToken.Should().Be(first!.AccessToken);
        otherUser!.AccessToken.Should().NotBe(first.AccessToken, "another agentic user's token is never reused");
        otherScope!.AccessToken.Should().NotBe(first.AccessToken);
        afterExpiry!.AccessToken.Should().NotBe(first.AccessToken);
        issued.Should().Equal(AgenticUserId, "eeeeeeee-0000-4000-8000-000000000005", AgenticUserId, AgenticUserId);
    }

    [Fact]
    public async Task RefreshesEarlyAndKeepsTheCachedTokenWhenTheRefreshFails()
    {
        var clock = new ManualClock();
        var attempts = 0;
        Exception? failure = null;
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return failure is null ? Task.FromResult(Jwt(clock.Now.AddHours(1), attempts)) : Task.FromException<string>(failure);
            });
        var resolver = PurviewDlpTokenResolvers.CreateAgenticUserResolver(connection.Object, clock, TimeSpan.FromSeconds(30));
        var cached = (await resolver(Agent(), "scope", CancellationToken.None))!.AccessToken;
        failure = new InvalidOperationException("Entra is unavailable");

        clock.Now += TimeSpan.FromMinutes(57);
        var withinRefreshWindow = await resolver(Agent(), "scope", CancellationToken.None);
        await WaitUntil(() => attempts == 2);

        withinRefreshWindow!.AccessToken.Should().Be(cached, "the cached token is used while it is valid");
        clock.Now += TimeSpan.FromMinutes(4);
        var afterExpiry = () => resolver(Agent(), "scope", CancellationToken.None);
        await afterExpiry.Should().ThrowAsync<InvalidOperationException>("a failed refresh is never cached, and an expired token is never used");
    }

    [Fact]
    public async Task SharesOneAcquisitionAndLetsItFinishForCallersThatStopWaiting()
    {
        var clock = new ManualClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref attempts);
                await release.Task;
                return Jwt(clock.Now.AddHours(1), 1);
            });
        var resolver = PurviewDlpTokenResolvers.CreateAgenticUserResolver(connection.Object, clock, TimeSpan.FromSeconds(30));
        using var impatient = new CancellationTokenSource();

        var abandoned = resolver(Agent(), "scope", impatient.Token);
        var waiting = resolver(Agent(), "scope", CancellationToken.None);
        impatient.Cancel();
        var stopWaiting = () => abandoned;
        await stopWaiting.Should().ThrowAsync<OperationCanceledException>();
        release.SetResult();
        var token = await waiting;
        var later = await resolver(Agent(), "scope", CancellationToken.None);

        attempts.Should().Be(1, "concurrent callers share one acquisition, which a caller that stops waiting does not cancel");
        later!.AccessToken.Should().Be(token!.AccessToken);
    }

    [Fact]
    public async Task ReleasesAnAcquisitionWhoseConnectionIgnoresCancellation()
    {
        var never = new TaskCompletionSource<string>();
        var attempts = 0;
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++attempts == 1 ? never.Task : Task.FromResult(Jwt(DateTimeOffset.UtcNow.AddHours(1), attempts)));
        var resolver = PurviewDlpTokenResolvers.CreateAgenticUserResolver(connection.Object, TimeProvider.System, TimeSpan.FromMilliseconds(200));

        var first = () => resolver(Agent(), "scope", CancellationToken.None);
        await first.Should().ThrowAsync<OperationCanceledException>();
        var second = await resolver(Agent(), "scope", CancellationToken.None);

        second.Should().NotBeNull("an acquisition that never ends does not hold the key");
        attempts.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoesNotCacheATokenWithoutAReadableExpiryOrUseAnExpiredOne(bool expired)
    {
        var attempts = 0;
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++attempts > 0 && expired ? Jwt(DateTimeOffset.UtcNow.AddMinutes(-1), attempts) : "opaque-token");
        var resolver = PurviewDlpTokenResolvers.FromAgenticUser(connection.Object);

        var act = () => resolver(Agent(), "scope", CancellationToken.None);

        if (expired)
        {
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        else
        {
            (await act())!.AccessToken.Should().Be("opaque-token");
            await act();
            attempts.Should().Be(2, "a token without a readable expiry is used but not cached");
        }
    }

    [Fact]
    public async Task KeepsTheCacheBounded()
    {
        var clock = new ManualClock();
        var cache = new AgenticUserTokenCache((key, _) => Task.FromResult<string?>(Jwt(clock.Now.AddHours(1), key.AgenticUserId.GetHashCode())), clock, TimeSpan.FromSeconds(30));

        await Task.WhenAll(Enumerable.Range(0, 150).Select(index => cache.GetAsync(new AgenticUserTokenKey(TenantId, AgentId, $"user-{index}", "scope"), CancellationToken.None)));

        cache.Count.Should().BeLessThanOrEqualTo(AgenticUserTokenCache.MaxEntries);
    }

    /// <summary>A JWT that expires at the given time; the seed makes each one distinct.</summary>
    private static string Jwt(DateTimeOffset expiresAt, int seed)
    {
        static string Encode(string json) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Encode("""{"alg":"none"}""")}.{Encode($$"""{"exp":{{expiresAt.ToUnixTimeSeconds()}},"n":{{seed}}}""")}.signature";
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && waited.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
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
