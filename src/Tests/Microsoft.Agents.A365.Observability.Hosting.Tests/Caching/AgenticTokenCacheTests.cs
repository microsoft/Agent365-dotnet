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
    private static readonly string[] TestScopes = new[] { "api://9b975845-388f-4429-889e-eab1ef63949c/.default" };

    [TestMethod]
    public async Task RegisterObservability_WithAppOnlyResolver_CachesTokenPerAgentTenant()
    {
        var resolverCalls = 0;
        var cache = new AgenticTokenCache(cleanupInterval: TimeSpan.Zero);
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
    public async Task RegisterObservability_MultipleAgentTenantKeys_AreIndependent()
    {
        var cache = new AgenticTokenCache(cleanupInterval: TimeSpan.Zero);

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
        var cache = new AgenticTokenCache(cleanupInterval: TimeSpan.Zero);
        var firstToken = CreateJwt(DateTimeOffset.UtcNow.AddMinutes(10));
        var secondToken = CreateJwt(DateTimeOffset.UtcNow.AddMinutes(20));

        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(firstToken), TestScopes);
        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(secondToken), TestScopes);

        var token = await cache.GetObservabilityToken(TestAgentId, TestTenantId);

        token.Should().Be(secondToken);
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_OpaqueTokenClearsStaleExpiryMetadata()
    {
        var cache = new AgenticTokenCache(cleanupInterval: TimeSpan.Zero);
        var expiredJwt = CreateJwt(DateTimeOffset.UtcNow.AddMinutes(-10));

        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>(expiredJwt), TestScopes);
        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>("opaque-token"), TestScopes);

        var removed = cache.RemoveExpiredTokens();

        removed.Should().Be(0, "refreshing with an opaque token should clear the prior JWT expiry metadata");
        cache.Count.Should().Be(1);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be("opaque-token");
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_ResolverFailure_PropagatesAndLeavesCachedToken()
    {
        var cache = new AgenticTokenCache(cleanupInterval: TimeSpan.Zero);
        await cache.RefreshObservabilityToken(TestAgentId, TestTenantId, (_, _, _) => Task.FromResult<string?>("cached-token"), TestScopes);

        Func<Task> act = () => cache.RefreshObservabilityToken(
            TestAgentId,
            TestTenantId,
            (_, _, _) => Task.FromException<string?>(new InvalidOperationException("acquisition failed")),
            TestScopes);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("acquisition failed");
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be("cached-token");
    }

    [TestMethod]
    public async Task RefreshObservabilityToken_EmptyToken_PropagatesFailure()
    {
        var cache = new AgenticTokenCache(cleanupInterval: TimeSpan.Zero);

        Func<Task> act = () => cache.RefreshObservabilityToken(
            TestAgentId,
            TestTenantId,
            (_, _, _) => Task.FromResult<string?>(string.Empty),
            TestScopes);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The observability token resolver returned an empty token.");
    }

    [TestMethod]
    public async Task RemovedDelegatedRegisterObservabilityShape_DoesNotRegisterOrThrowWhenInvokedDynamically()
    {
        var cache = new AgenticTokenCache(cleanupInterval: TimeSpan.Zero);
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
