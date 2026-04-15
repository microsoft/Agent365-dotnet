using FluentAssertions;
using Microsoft.Agents.A365.Observability.Hosting.Caching;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Caching;

[TestClass]
public sealed class ServiceTokenCacheTests
{
    private const string TestAgentId = "test-agent";
    private const string TestTenantId = "test-tenant";
    private const string TestToken = "test-token-12345";
    private readonly string[] TestScopes = new[] { "https://example.com/.default" };

    [TestMethod]
    public void Constructor_WithDefaultExpiration_ShouldSucceed()
    {
        var cache = new ServiceTokenCache();
        cache.Should().NotBeNull();
    }

    [TestMethod]
    public void Constructor_WithCustomExpiration_ShouldSucceed()
    {
        var cache = new ServiceTokenCache(TimeSpan.FromMinutes(30));
        cache.Should().NotBeNull();
    }

    [TestMethod]
    public void Constructor_WithZeroExpiration_ShouldThrow()
    {
        Action act = () => new ServiceTokenCache(TimeSpan.Zero);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Default expiration must be greater than zero*");
    }

    [TestMethod]
    public void Constructor_WithNegativeExpiration_ShouldThrow()
    {
        Action act = () => new ServiceTokenCache(TimeSpan.FromMinutes(-1));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Default expiration must be greater than zero*");
    }

    [TestMethod]
    public async Task RegisterObservability_WithValidParameters_ShouldSucceed()
    {
        var cache = new ServiceTokenCache();
        
        cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes);
        
        var token = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        token.Should().Be(TestToken);
    }

    [TestMethod]
    public void RegisterObservability_WithNullAgentId_ShouldThrow()
    {
        var cache = new ServiceTokenCache();
        
        Action act = () => cache.RegisterObservability(null!, TestTenantId, TestToken, TestScopes);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be null or whitespace*")
            .WithParameterName("agentId");
    }

    [TestMethod]
    public void RegisterObservability_WithEmptyAgentId_ShouldThrow()
    {
        var cache = new ServiceTokenCache();
        
        Action act = () => cache.RegisterObservability("", TestTenantId, TestToken, TestScopes);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be null or whitespace*")
            .WithParameterName("agentId");
    }

    [TestMethod]
    public void RegisterObservability_WithNullTenantId_ShouldThrow()
    {
        var cache = new ServiceTokenCache();
        
        Action act = () => cache.RegisterObservability(TestAgentId, null!, TestToken, TestScopes);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be null or whitespace*")
            .WithParameterName("tenantId");
    }

    [TestMethod]
    public void RegisterObservability_WithEmptyToken_ShouldThrow()
    {
        var cache = new ServiceTokenCache();
        
        Action act = () => cache.RegisterObservability(TestAgentId, TestTenantId, "", TestScopes);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be null or whitespace*")
            .WithParameterName("token");
    }

    [TestMethod]
    public void RegisterObservability_WithNullScopes_ShouldThrow()
    {
        var cache = new ServiceTokenCache();
        
        Action act = () => cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, null!);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Observability scopes cannot be null or empty*")
            .WithParameterName("observabilityScopes");
    }

    [TestMethod]
    public void RegisterObservability_WithEmptyScopes_ShouldThrow()
    {
        var cache = new ServiceTokenCache();
        
        Action act = () => cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, Array.Empty<string>());
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Observability scopes cannot be null or empty*")
            .WithParameterName("observabilityScopes");
    }

    [TestMethod]
    public async Task RegisterObservability_WithCustomExpiration_ShouldRespectExpiration()
    {
        var cache = new ServiceTokenCache();
        var customExpiration = TimeSpan.FromSeconds(1);
        
        cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes, customExpiration);

        var tokenBefore = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        tokenBefore.Should().Be(TestToken);
        
        Thread.Sleep(1100);

        var tokenAfter = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        tokenAfter.Should().BeNull();
    }

    [TestMethod]
    public void RegisterObservability_WithZeroCustomExpiration_ShouldThrow()
    {
        var cache = new ServiceTokenCache();
        
        Action act = () => cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes, TimeSpan.Zero);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Expiration time must be greater than zero*")
            .WithParameterName("expiresIn");
    }

    [TestMethod]
    public async Task RegisterObservability_Twice_ShouldUpdateToken()
    {
        var cache = new ServiceTokenCache();
        const string firstToken = "first-token";
        const string secondToken = "second-token";
        
        cache.RegisterObservability(TestAgentId, TestTenantId, firstToken, TestScopes);
        cache.RegisterObservability(TestAgentId, TestTenantId, secondToken, TestScopes);

        var token = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        token.Should().Be(secondToken);
    }

    [TestMethod]
    public async Task GetObservabilityToken_WithNonExistentKey_ShouldReturnNull()
    {
        var cache = new ServiceTokenCache();

        var token = await cache.GetObservabilityToken("non-existent-agent", "non-existent-tenant");

        token.Should().BeNull();
    }

    [TestMethod]
    public async Task GetObservabilityToken_WithNullAgentId_ShouldReturnNull()
    {
        var cache = new ServiceTokenCache();
        
        var token = await cache.GetObservabilityToken(null!, TestTenantId);
        
        token.Should().BeNull();
    }

    [TestMethod]
    public async Task GetObservabilityToken_WithEmptyTenantId_ShouldReturnNull()
    {
        var cache = new ServiceTokenCache();
        
        var token = await cache.GetObservabilityToken(TestAgentId, "");
        
        token.Should().BeNull();
    }

    [TestMethod]
    public async Task GetObservabilityToken_AfterExpiration_ShouldReturnNull()
    {
        var cache = new ServiceTokenCache(TimeSpan.FromMilliseconds(500));
        
        cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes);
        
        Thread.Sleep(600);
        
        var token = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        token.Should().BeNull();
    }

    [TestMethod]
    public async Task GetObservabilityToken_BeforeExpiration_ShouldReturnToken()
    {
        var cache = new ServiceTokenCache(TimeSpan.FromSeconds(10));
        
        cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes);
        
        var token = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        token.Should().Be(TestToken);
    }

    [TestMethod]
    public async Task InvalidateToken_WithExistingToken_ShouldReturnTrueAndRemoveToken()
    {
        var cache = new ServiceTokenCache();
        cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes);
        
        var result = cache.InvalidateToken(TestAgentId, TestTenantId);
        
        result.Should().BeTrue();
        
        var token = await cache.GetObservabilityToken(TestAgentId, TestTenantId);
        token.Should().BeNull();
    }

    [TestMethod]
    public void InvalidateToken_WithNonExistentToken_ShouldReturnFalse()
    {
        var cache = new ServiceTokenCache();
        
        var result = cache.InvalidateToken("non-existent-agent", "non-existent-tenant");
        
        result.Should().BeFalse();
    }

    [TestMethod]
    public void InvalidateToken_WithNullAgentId_ShouldReturnFalse()
    {
        var cache = new ServiceTokenCache();
        
        var result = cache.InvalidateToken(null!, TestTenantId);
        
        result.Should().BeFalse();
    }

    [TestMethod]
    public void InvalidateToken_WithEmptyTenantId_ShouldReturnFalse()
    {
        var cache = new ServiceTokenCache();
        
        var result = cache.InvalidateToken(TestAgentId, "");
        
        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task InvalidateAll_ShouldRemoveAllTokens()
    {
        var cache = new ServiceTokenCache();
        cache.RegisterObservability("agent1", "tenant1", "token1", TestScopes);
        cache.RegisterObservability("agent2", "tenant2", "token2", TestScopes);
        cache.RegisterObservability("agent3", "tenant3", "token3", TestScopes);
        
        cache.InvalidateAll();
        
        (await cache.GetObservabilityToken("agent1", "tenant1")).Should().BeNull();
        (await cache.GetObservabilityToken("agent2", "tenant2")).Should().BeNull();
        (await cache.GetObservabilityToken("agent3", "tenant3")).Should().BeNull();
    }

    [TestMethod]
    public async Task RemoveExpiredTokens_WithNoExpiredTokens_ShouldReturnZero()
    {
        var cache = new ServiceTokenCache(TimeSpan.FromHours(1));
        cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes);
        
        var removedCount = cache.RemoveExpiredTokens();
        
        removedCount.Should().Be(0);
        (await cache.GetObservabilityToken(TestAgentId, TestTenantId)).Should().Be(TestToken);
    }

    [TestMethod]
    public async Task RemoveExpiredTokens_WithExpiredTokens_ShouldRemoveThemAndReturnCount()
    {
        var cache = new ServiceTokenCache(TimeSpan.FromMilliseconds(500));
        cache.RegisterObservability("agent1", "tenant1", "token1", TestScopes);
        cache.RegisterObservability("agent2", "tenant2", "token2", TestScopes);
        
        Thread.Sleep(600);
        
        cache.RegisterObservability("agent3", "tenant3", "token3", TestScopes, TimeSpan.FromHours(1));
        
        var removedCount = cache.RemoveExpiredTokens();
        
        removedCount.Should().Be(2);
        (await cache.GetObservabilityToken("agent1", "tenant1")).Should().BeNull();
        (await cache.GetObservabilityToken("agent2", "tenant2")).Should().BeNull();
        (await cache.GetObservabilityToken("agent3", "tenant3")).Should().Be("token3");
    }

    [TestMethod]
    public async Task RemoveExpiredTokens_WithMixedExpirations_ShouldOnlyRemoveExpired()
    {
        var cache = new ServiceTokenCache();
        cache.RegisterObservability("agent1", "tenant1", "token1", TestScopes, TimeSpan.FromMilliseconds(500));
        cache.RegisterObservability("agent2", "tenant2", "token2", TestScopes, TimeSpan.FromHours(1));
        cache.RegisterObservability("agent3", "tenant3", "token3", TestScopes, TimeSpan.FromMilliseconds(500));
        
        Thread.Sleep(600);
        
        var removedCount = cache.RemoveExpiredTokens();
        
        removedCount.Should().Be(2);
        (await cache.GetObservabilityToken("agent1", "tenant1")).Should().BeNull();
        (await cache.GetObservabilityToken("agent2", "tenant2")).Should().Be("token2");
        (await cache.GetObservabilityToken("agent3", "tenant3")).Should().BeNull();
    }

    [TestMethod]
    public async Task MultipleAgentsTenants_ShouldBeIndependent()
    {
        var cache = new ServiceTokenCache();
        cache.RegisterObservability("agent1", "tenant1", "token1", TestScopes);
        cache.RegisterObservability("agent2", "tenant1", "token2", TestScopes);
        cache.RegisterObservability("agent1", "tenant2", "token3", TestScopes);
        
        (await cache.GetObservabilityToken("agent1", "tenant1")).Should().Be("token1");
        (await cache.GetObservabilityToken("agent2", "tenant1")).Should().Be("token2");
        (await cache.GetObservabilityToken("agent1", "tenant2")).Should().Be("token3");
    }

    [TestMethod]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        var cache = new ServiceTokenCache();
        var tasks = new List<Task>();
        var tokenCount = 100;

        for (int i = 0; i < tokenCount; i++)
        {
            var agentId = $"agent-{i}";
            var tenantId = $"tenant-{i}";
            var token = $"token-{i}";
            
            tasks.Add(Task.Run(() => cache.RegisterObservability(agentId, tenantId, token, TestScopes)));
        }

        Task.WaitAll(tasks.ToArray());

        for (int i = 0; i < tokenCount; i++)
        {
            var agentId = $"agent-{i}";
            var tenantId = $"tenant-{i}";
            var expectedToken = $"token-{i}";
            
            (await cache.GetObservabilityToken(agentId, tenantId)).Should().Be(expectedToken);
        }
    }

    [TestMethod]
    public void Constructor_WithCustomCleanupInterval_ShouldSucceed()
    {
        using var cache = new ServiceTokenCache(TimeSpan.FromHours(1), TimeSpan.FromMinutes(10));
        cache.Should().NotBeNull();
    }

    [TestMethod]
    public void Constructor_WithDisabledCleanupInterval_ShouldSucceed()
    {
        using var cache = new ServiceTokenCache(TimeSpan.FromHours(1), TimeSpan.Zero);
        cache.Should().NotBeNull();
    }

    [TestMethod]
    public void Count_ShouldReturnCorrectNumber()
    {
        using var cache = new ServiceTokenCache();
        cache.RegisterObservability("agent1", "tenant1", "token1", TestScopes);
        cache.RegisterObservability("agent2", "tenant2", "token2", TestScopes);
        
        cache.Count.Should().Be(2);
    }

    [TestMethod]
    public void Dispose_ShouldClearAllTokens()
    {
        var cache = new ServiceTokenCache();
        cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes);
        
        cache.Dispose();
        
        cache.Count.Should().Be(0);
    }

    [TestMethod]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        var cache = new ServiceTokenCache();
        cache.RegisterObservability(TestAgentId, TestTenantId, TestToken, TestScopes);
        
        cache.Dispose();
        cache.Dispose(); // Second dispose should not throw
        
        cache.Count.Should().Be(0);
    }

    [TestMethod]
    public void DefaultCleanupInterval_ShouldBeFiveMinutes()
    {
        ServiceTokenCache.DefaultCleanupInterval.Should().Be(TimeSpan.FromMinutes(5));
    }
}
