// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Hosting.Caching;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Caching;

[TestClass]
public sealed class AgenticTokenCacheTests
{
    private const string TestAgentId = "test-agent";
    private const string TestTenantId = "test-tenant";
    private const string ScopeOverrideEnvVar = "A365_OBSERVABILITY_SCOPE_OVERRIDE";
    private const string DefaultObsScope = "api://9b975845-388f-4429-889e-eab1ef63949c/.default";
    private static readonly string[] TestScopes = new[] { "api://9b975845-388f-4429-889e-eab1ef63949c/.default" };

    [TestCleanup]
    public void TestCleanup()
    {
        Environment.SetEnvironmentVariable(ScopeOverrideEnvVar, null);
    }

    [TestMethod]
    public async Task RegisterObservability_WithAppOnlyResolver_CachesTokenPerAgentTenant()
    {
        var resolverCalls = 0;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);
        ObservabilityTokenResolver resolver = (agentId, tenantId, scopes) =>
        {
            Interlocked.Increment(ref resolverCalls);
            agentId.Should().Be(TestAgentId);
            tenantId.Should().Be(TestTenantId);
            scopes.Should().Equal(TestScopes);
            return Task.FromResult<string?>("app-only-token");
        };

        cache.RegisterObservability(TestAgentId, TestTenantId, resolver, TestScopes);

        var firstToken = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        var secondToken = await cache.GetObservabilityToken(TestAgentId, TestTenantId);

        firstToken.Should().Be("app-only-token");
        secondToken.Should().Be("app-only-token");
        resolverCalls.Should().Be(1, "the cache should reuse a usable token for the same agent/tenant key");
    }

    [TestMethod]
    public async Task RegisterObservability_RepeatedRegistration_DoesNotReplaceResolverOrClearToken()
    {
        var firstResolverCalls = 0;
        var secondResolverCalls = 0;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);

        cache.RegisterObservability(TestAgentId, TestTenantId, (_, _, _) =>
        {
            Interlocked.Increment(ref firstResolverCalls);
            return Task.FromResult<string?>("first-token");
        }, TestScopes);

        cache.RegisterObservability(TestAgentId, TestTenantId, (_, _, _) =>
        {
            Interlocked.Increment(ref secondResolverCalls);
            return Task.FromResult<string?>("second-token");
        }, TestScopes);

        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be("first-token");

        cache.RegisterObservability(TestAgentId, TestTenantId, (_, _, _) =>
        {
            Interlocked.Increment(ref secondResolverCalls);
            return Task.FromResult<string?>("second-token");
        }, TestScopes);

        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be("first-token");
        firstResolverCalls.Should().Be(1);
        secondResolverCalls.Should().Be(0, "RegisterObservability is idempotent and first registration wins");
    }

    [TestMethod]
    public async Task RegisterObservability_MultipleAgentTenantKeys_AreIndependent()
    {
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);

        cache.RegisterObservability("agent-1", "tenant-1", (_, _, _) => Task.FromResult<string?>("token-11"), TestScopes);
        cache.RegisterObservability("agent-2", "tenant-1", (_, _, _) => Task.FromResult<string?>("token-21"), TestScopes);
        cache.RegisterObservability("agent-1", "tenant-2", (_, _, _) => Task.FromResult<string?>("token-12"), TestScopes);

        (await cache.GetObservabilityToken("agent-1", "tenant-1")).Should().Be("token-11");
        (await cache.GetObservabilityToken("agent-2", "tenant-1")).Should().Be("token-21");
        (await cache.GetObservabilityToken("agent-1", "tenant-2")).Should().Be("token-12");
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_UpdatesCachedTokenAndExpiry()
    {
        var now = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        var firstToken = CreateJwt(now.AddMinutes(4));
        var secondToken = CreateJwt(now.AddMinutes(20));

        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(firstToken), TestScopes);
        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(secondToken), TestScopes);

        var token = await cache.GetObservabilityToken(TestAgentId, TestTenantId);

        token.Should().Be(secondToken);
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_ReturnsCachedTokenUntilNearExpiry_AndReplacesFutureResolver()
    {
        var now = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var firstToken = CreateJwt(now.AddMinutes(10));
        var secondToken = CreateJwt(now.AddMinutes(20));
        var firstResolverCalls = 0;
        var secondResolverCalls = 0;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);

        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) =>
        {
            Interlocked.Increment(ref firstResolverCalls);
            return Task.FromResult<string?>(firstToken);
        }, TestScopes);

        var cachedToken = await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) =>
        {
            Interlocked.Increment(ref secondResolverCalls);
            return Task.FromResult<string?>(secondToken);
        }, TestScopes);

        cachedToken.Should().Be(firstToken);
        firstResolverCalls.Should().Be(1);
        secondResolverCalls.Should().Be(0, "RefreshObservabilityToken should not call the resolver while a token is usable");

        now = now.AddMinutes(6);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be(secondToken);
        secondResolverCalls.Should().Be(1, "the resolver passed to RefreshObservabilityToken replaces the resolver for future refreshes");
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_ThreeArgumentOverload_UsesDefaultAppOnlyScope()
    {
        Environment.SetEnvironmentVariable(ScopeOverrideEnvVar, null);
        string[]? capturedScopes = null;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);

        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, scopes) =>
        {
            capturedScopes = scopes;
            return Task.FromResult<string?>("app-only-token");
        });

        capturedScopes.Should().Equal(DefaultObsScope);
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_ThreeArgumentOverload_HonorsScopeOverride()
    {
        const string overrideScope = "api://override-resource/.default";
        Environment.SetEnvironmentVariable(ScopeOverrideEnvVar, overrideScope);
        string[]? capturedScopes = null;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);

        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, scopes) =>
        {
            capturedScopes = scopes;
            return Task.FromResult<string?>("app-only-token");
        });

        capturedScopes.Should().Equal(overrideScope);
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_OpaqueTokenClearsStaleExpiryMetadata()
    {
        var now = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        var expiredJwt = CreateJwt(now.AddMinutes(-10));

        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(expiredJwt), TestScopes);
        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>("opaque-token"), TestScopes);

        var removed = cache.RemoveExpiredTokens();

        removed.Should().Be(0, "refreshing with an opaque token should clear the prior JWT expiry metadata");
        cache.Count.Should().Be(1);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be("opaque-token");
    }

    [TestMethod]
    public async Task OpaqueToken_IsUsableBeforeFallbackMaxAge_AndRefreshesAtBoundary()
    {
        var acquiredAt = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var now = acquiredAt;
        var resolverCalls = 0;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        ObservabilityTokenResolver resolver = (_, _, _) =>
        {
            var call = Interlocked.Increment(ref resolverCalls);
            return Task.FromResult<string?>(call == 1 ? "opaque-token-1" : "opaque-token-2");
        };

        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, resolver, TestScopes);

        now = acquiredAt.AddHours(1).AddTicks(-1);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be("opaque-token-1");
        resolverCalls.Should().Be(1);

        now = acquiredAt.AddHours(1);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be("opaque-token-2");
        resolverCalls.Should().Be(2);
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_ResolverFailure_PropagatesAndClearsCachedToken()
    {
        var now = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(CreateJwt(now.AddMinutes(4))), TestScopes);

        Func<Task> act = () => cache.RefreshObservabilityToken(
            TestAgentId,
            TestTenantId,
            (_, _, _) => Task.FromException<string?>(new InvalidOperationException("acquisition failed")),
            TestScopes);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("acquisition failed");

        cache.RemoveExpiredTokens().Should().Be(0, "a refresh failure must clear stale token and expiry metadata");
        Func<Task> get = async () => await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        await get.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("acquisition failed");
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_EmptyToken_PropagatesFailureAndClearsCachedToken()
    {
        var now = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(CreateJwt(now.AddMinutes(4))), TestScopes);

        Func<Task> act = () => cache.RefreshObservabilityToken(
            TestAgentId,
            TestTenantId,
            (_, _, _) => Task.FromResult<string?>(string.Empty),
            TestScopes);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The observability token resolver returned an empty token.");

        cache.RemoveExpiredTokens().Should().Be(0, "an empty resolver result must clear stale token and expiry metadata");
        Func<Task> get = async () => await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        await get.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The observability token resolver returned an empty token.");
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_ConcurrentCallers_ShareOneAcquisition()
    {
        var resolverCalls = 0;
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);
        ObservabilityTokenResolver resolver = (_, _, _) =>
        {
            Interlocked.Increment(ref resolverCalls);
            return release.Task;
        };

        var first = cache.RefreshObservabilityToken(TestAgentId, TestTenantId, resolver, TestScopes);
        var second = cache.RefreshObservabilityToken(TestAgentId, TestTenantId, resolver, TestScopes);
        release.SetResult("app-only-token");

        (await Task.WhenAll(first, second)).Should().Equal("app-only-token", "app-only-token");
        resolverCalls.Should().Be(1, "concurrent refreshes for one agent and tenant should share one acquisition");
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_ConcurrentFailure_DoesNotClearTokenCachedByAnotherCaller()
    {
        var failingResolverCalls = 0;
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);

        var succeeding = cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => release.Task, TestScopes);
        var failing = cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) =>
        {
            Interlocked.Increment(ref failingResolverCalls);
            return Task.FromException<string?>(new InvalidOperationException("acquisition failed"));
        }, TestScopes);
        release.SetResult("app-only-token");

        (await succeeding).Should().Be("app-only-token");
        (await failing).Should().Be("app-only-token", "a waiting caller should reuse the token cached by the refresh ahead of it");
        failingResolverCalls.Should().Be(0);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be("app-only-token");
    }

    [TestMethod]
    public async Task RemoveExpiredTokens_ClearsExpiredTokenButKeepsResolverRegistration()
    {
        var now = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var resolverCalls = 0;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        ObservabilityTokenResolver resolver = (_, _, _) =>
        {
            Interlocked.Increment(ref resolverCalls);
            return Task.FromResult<string?>(CreateJwt(now.AddHours(1)));
        };

        cache.RegisterObservability(TestAgentId, TestTenantId, resolver, TestScopes);
        var firstToken = await cache.GetObservabilityToken(TestAgentId, TestTenantId);

        now = now.AddHours(2);
        cache.RemoveExpiredTokens().Should().Be(1);
        cache.Count.Should().Be(1, "cleanup must keep the app-only resolver registration");

        var secondToken = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        secondToken.Should().NotBeNull().And.NotBe(firstToken);
        resolverCalls.Should().Be(2);
    }

    [TestMethod]
    public async Task RemoveExpiredTokens_SkipsEntryWhileRefreshIsInFlight()
    {
        var now = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(CreateJwt(now.AddMinutes(10))), TestScopes);

        now = now.AddMinutes(30);
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => release.Task, TestScopes);

        cache.RemoveExpiredTokens().Should().Be(0, "cleanup must not race an in-flight refresh");
        release.SetResult(CreateJwt(now.AddHours(1)));

        var refreshed = await refresh;
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be(refreshed);
    }

    [TestMethod]
    public async Task GetObservabilityToken_QueuedBehindExplicitRefresh_DoesNotRestoreReplacedResolver()
    {
        var now = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var registeredResolverCalls = 0;
        var replacementResolverCalls = 0;
        var firstToken = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        cache.RegisterObservability(TestAgentId, TestTenantId, (_, _, _) =>
            Interlocked.Increment(ref registeredResolverCalls) == 1
                ? firstToken.Task
                : Task.FromResult<string?>(CreateJwt(now.AddHours(1))), TestScopes);
        ObservabilityTokenResolver replacementResolver = (_, _, _) =>
        {
            Interlocked.Increment(ref replacementResolverCalls);
            return Task.FromResult<string?>(CreateJwt(now.AddHours(1)));
        };

        var inFlightGet = cache.GetObservabilityToken(TestAgentId, TestTenantId);
        var explicitRefresh = cache.RefreshObservabilityToken(TestAgentId, TestTenantId, replacementResolver, TestScopes);
        var queuedGet = cache.GetObservabilityToken(TestAgentId, TestTenantId);
        firstToken.SetResult(CreateJwt(now.AddHours(1)));
        await Task.WhenAll(new Task[] { inFlightGet, explicitRefresh, queuedGet });

        now = now.AddHours(2);
        await cache.GetObservabilityToken(TestAgentId, TestTenantId);

        replacementResolverCalls.Should().Be(1, "the resolver installed by an explicit refresh is used for the next refresh");
        registeredResolverCalls.Should().Be(1, "a cache-driven refresh must not restore the resolver an explicit refresh replaced");
    }

    [TestMethod]
    [DataRow("opaque.token")]
    [DataRow("not.a.jwt")]
    [DataRow("header.@@@.signature")]
    public async Task RefreshObservabilityToken_UnparseableTokenWithPeriods_UsesOpaqueFallback(string unparseableToken)
    {
        var acquiredAt = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var now = acquiredAt;
        var resolverCalls = 0;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);
        ObservabilityTokenResolver resolver = (_, _, _) =>
        {
            Interlocked.Increment(ref resolverCalls);
            return Task.FromResult<string?>(unparseableToken);
        };

        (await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, resolver, TestScopes)).Should().Be(unparseableToken);

        now = acquiredAt.AddMinutes(59);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be(unparseableToken);
        resolverCalls.Should().Be(1, "tokens the JWT handler cannot parse use the opaque one-hour fallback");
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_JwtWithNonNumericExp_UsesOpaqueFallback()
    {
        var acquiredAt = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        var now = acquiredAt;
        var token = $"{Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{Base64Url("{\"exp\":\"soon\"}")}.";
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => now);

        (await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(token), TestScopes)).Should().Be(token);

        now = acquiredAt.AddMinutes(59);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be(token);
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_SeparatorBearingIds_DoNotShareCacheEntry()
    {
        var secondResolverCalls = 0;
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);

        (await cache.RefreshObservabilityToken("a:b", "c", (_, _, _) => Task.FromResult<string?>("token-for-first"), TestScopes))
            .Should().Be("token-for-first");
        (await cache.RefreshObservabilityToken("a", "b:c", (_, _, _) =>
        {
            Interlocked.Increment(ref secondResolverCalls);
            return Task.FromResult<string?>("token-for-second");
        }, TestScopes)).Should().Be("token-for-second");

        secondResolverCalls.Should().Be(1, "a different agent/tenant pair must not reuse another pair's token");
        (await cache.GetObservabilityToken("a:b", "c")).Should().Be("token-for-first");
        (await cache.GetObservabilityToken("a", "b:c")).Should().Be("token-for-second");
        cache.Count.Should().Be(2);
    }

    [TestMethod]
    public async Task RemovedDelegatedRegisterObservabilityShape_DoesNotRegisterOrThrowWhenInvokedDynamically()
    {
        var cache = new AgenticTokenCache(TimeSpan.Zero, () => DateTimeOffset.UtcNow);
        var removedOverload = typeof(AgenticTokenCache).GetMethod(
            nameof(AgenticTokenCache.RegisterObservability),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(AgenticTokenStruct), typeof(string[]) },
            modifiers: null);

        removedOverload.Should().NotBeNull();
        var act = () => removedOverload!.Invoke(cache, new object?[] { TestAgentId, TestTenantId, null, TestScopes });

        act.Should().NotThrow("untyped callers should get no delegated exchange and no crash");
        cache.Count.Should().Be(0);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().BeNull();
    }

    private static string CreateJwt(DateTimeOffset expiresAt)
    {
        var header = Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64Url($"{{\"exp\":{expiresAt.ToUnixTimeSeconds()}}}");
        return $"{header}.{payload}.";
    }

    private static string Base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
