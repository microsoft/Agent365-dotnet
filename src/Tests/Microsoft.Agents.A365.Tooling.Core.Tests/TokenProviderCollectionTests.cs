// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// Tests for <see cref="TokenProviderCollection"/> which chains multiple token providers
/// and returns the first successful token.
/// </summary>
public class TokenProviderCollectionTests
{
    private static MCPServerConfig TestServer(string name = "test-server") =>
        new() { mcpServerName = name, id = $"id-{name}", url = "http://test" };

    private static Mock<ILogger> CreateMockLogger() => new Mock<ILogger>();

    // ─── Constructor validation ──────────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidProviders_Succeeds()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        // Act
        var act = () => new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNoProviders_Succeeds()
    {
        // Arrange
        var logger = CreateMockLogger();

        // Act
        var act = () => new TokenProviderCollection(logger.Object);

        // Assert - Constructor succeeds; validation happens on GetTokenAsync
        act.Should().NotThrow();
    }

    // ─── Provider ordering and priority ──────────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_FirstProviderReturnsToken_ReturnsFirstToken()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-from-provider1");
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-from-provider2");

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act
        var token = await collection.GetTokenAsync(TestServer());

        // Assert
        token.Should().Be("token-from-provider1");
        provider1.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        provider2.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTokenAsync_FirstProviderFails_TriesSecondProvider()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider 1 failed"));
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-from-provider2");

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act
        var token = await collection.GetTokenAsync(TestServer());

        // Assert
        token.Should().Be("token-from-provider2");
        provider1.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        provider2.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_FirstProviderReturnsEmpty_TriesSecondProvider()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-from-provider2");

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act
        var token = await collection.GetTokenAsync(TestServer());

        // Assert
        token.Should().Be("token-from-provider2");
        provider1.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        provider2.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_FirstProviderReturnsWhitespace_TriesSecondProvider()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("   ");
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-from-provider2");

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act
        var token = await collection.GetTokenAsync(TestServer());

        // Assert
        token.Should().Be("token-from-provider2");
    }

    [Fact]
    public async Task GetTokenAsync_MultipleProviders_TriesInOrder()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();
        var provider3 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Failed 1"));
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        provider3.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-from-provider3");

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object, provider3.Object);

        // Act
        var token = await collection.GetTokenAsync(TestServer());

        // Assert
        token.Should().Be("token-from-provider3");
        provider1.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        provider2.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        provider3.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Error handling and AggregateException ───────────────────────────────

    [Fact]
    public async Task GetTokenAsync_AllProvidersFail_ThrowsAggregateException()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider 1 failed"));
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Provider 2 failed"));

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act
        var act = () => collection.GetTokenAsync(TestServer());

        // Assert
        await act.Should().ThrowAsync<AggregateException>()
            .WithMessage("*No valid token could be obtained from any provider*");
    }

    [Fact]
    public async Task GetTokenAsync_AllProvidersFail_AggregateExceptionContainsAllInnerExceptions()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        var exception1 = new InvalidOperationException("Provider 1 error");
        var exception2 = new UnauthorizedAccessException("Provider 2 error");

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception1);
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception2);

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act & Assert
        var aggregateEx = await Assert.ThrowsAsync<AggregateException>(
            () => collection.GetTokenAsync(TestServer()));

        aggregateEx.InnerExceptions.Should().HaveCount(2);
        aggregateEx.InnerExceptions[0].InnerException.Should().BeSameAs(exception1);
        aggregateEx.InnerExceptions[1].InnerException.Should().BeSameAs(exception2);
    }

    [Fact]
    public async Task GetTokenAsync_AllProvidersReturnEmpty_ThrowsAggregateException()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("   ");

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act
        var act = () => collection.GetTokenAsync(TestServer());

        // Assert - When all providers return empty, no exceptions are collected, but still throws AggregateException
        await act.Should().ThrowAsync<AggregateException>()
            .WithMessage("*No valid token could be obtained from any provider*");
    }

    // ─── Validation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_NoProviders_ThrowsInvalidOperationException()
    {
        // Arrange
        var logger = CreateMockLogger();
        var collection = new TokenProviderCollection(logger.Object);

        // Act
        var act = () => collection.GetTokenAsync(TestServer());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No token providers are configured*");
    }

    [Fact]
    public async Task GetTokenAsync_NullProviderInArray_ThrowsInvalidOperationException()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        IMcpTokenProvider? nullProvider = null;

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, nullProvider!);

        // Act
        var act = () => collection.GetTokenAsync(TestServer());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*One or more token providers are null*");
    }

    // ─── Cancellation ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_CancelledBeforeCall_ThrowsOperationCancelledException()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider = new Mock<IMcpTokenProvider>();
        var collection = new TokenProviderCollection(logger.Object, provider.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => collection.GetTokenAsync(TestServer(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        provider.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTokenAsync_ProviderRespectsCancellation_PropagatesException()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider = new Mock<IMcpTokenProvider>();
        var cts = new CancellationTokenSource();

        provider.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var collection = new TokenProviderCollection(logger.Object, provider.Object);

        // Act
        var act = () => collection.GetTokenAsync(TestServer(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<AggregateException>();
    }

    // ─── Logging behavior ────────────────────────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_ProviderReturnsEmpty_LogsDebugMessage()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid-token");

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act
        await collection.GetTokenAsync(TestServer());

        // Assert - Verify that LogDebug was called (checking the log level)
        logger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("returned an empty token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_ProviderFails_LogsDebugMessage()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider1 = new Mock<IMcpTokenProvider>();
        var provider2 = new Mock<IMcpTokenProvider>();

        provider1.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test failure"));
        provider2.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid-token");

        var collection = new TokenProviderCollection(logger.Object, provider1.Object, provider2.Object);

        // Act
        await collection.GetTokenAsync(TestServer());

        // Assert - Verify that LogDebug was called for the failure
        logger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed to obtain a token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ─── Integration scenarios ───────────────────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_RealWorldScenario_EnvThenAgentic()
    {
        // Simulate the common pattern: EnvMcpTokenProvider (dev) fallback to AgenticMcpTokenProvider (prod)
        // Arrange
        var logger = CreateMockLogger();
        var envProvider = new Mock<IMcpTokenProvider>();
        var agenticProvider = new Mock<IMcpTokenProvider>();

        // EnvProvider returns empty (not in dev scenario or no env var set)
        envProvider.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // AgenticProvider performs OBO and returns token
        agenticProvider.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("agentic-obo-token");

        var collection = new TokenProviderCollection(logger.Object, envProvider.Object, agenticProvider.Object);

        // Act
        var token = await collection.GetTokenAsync(TestServer());

        // Assert
        token.Should().Be("agentic-obo-token");
        envProvider.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        agenticProvider.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_RealWorldScenario_EnvProviderSucceedsInDev()
    {
        // Simulate dev scenario where EnvProvider finds token
        // Arrange
        var logger = CreateMockLogger();
        var envProvider = new Mock<IMcpTokenProvider>();
        var agenticProvider = new Mock<IMcpTokenProvider>();

        // EnvProvider finds token from environment variable
        envProvider.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("dev-env-token");

        // AgenticProvider should never be called
        agenticProvider.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("should-not-be-used");

        var collection = new TokenProviderCollection(logger.Object, envProvider.Object, agenticProvider.Object);

        // Act
        var token = await collection.GetTokenAsync(TestServer());

        // Assert
        token.Should().Be("dev-env-token");
        envProvider.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        agenticProvider.Verify(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTokenAsync_SingleProvider_ReturnsToken()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider = new Mock<IMcpTokenProvider>();

        provider.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("single-provider-token");

        var collection = new TokenProviderCollection(logger.Object, provider.Object);

        // Act
        var token = await collection.GetTokenAsync(TestServer());

        // Assert
        token.Should().Be("single-provider-token");
    }

    [Fact]
    public async Task GetTokenAsync_SingleProviderFails_ThrowsAggregateException()
    {
        // Arrange
        var logger = CreateMockLogger();
        var provider = new Mock<IMcpTokenProvider>();

        provider.Setup(p => p.GetTokenAsync(It.IsAny<MCPServerConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Single provider failed"));

        var collection = new TokenProviderCollection(logger.Object, provider.Object);

        // Act
        var act = () => collection.GetTokenAsync(TestServer());

        // Assert
        var aggregateEx = await act.Should().ThrowAsync<AggregateException>();
        aggregateEx.Which.InnerExceptions.Should().HaveCount(1);
    }
}
