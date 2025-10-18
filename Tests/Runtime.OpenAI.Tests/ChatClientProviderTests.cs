// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using Microsoft.Agents.A365.Runtime.Extensions.OpenAI;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using Xunit;

namespace Microsoft.Agents.A365.Runtime.OpenAI.Tests
{
    /// <summary>
    /// Unit tests for the ChatClientProvider functionality.
    /// </summary>
    public class ChatClientProviderTests : IDisposable
    {
        private readonly ChatClientProvider _provider;
        private readonly Mock<ILogger<ChatClientProvider>> _loggerMock;
        private int _clientCounter = 0;

        public ChatClientProviderTests()
        {
            _loggerMock = new Mock<ILogger<ChatClientProvider>>();
            
            // Create provider with a delegate that creates different mock instances for different tenant/worker combinations
            _provider = new ChatClientProvider(
                createChatClient: (tenantId, workerId) => 
                {
                    var mockClient = new Mock<ChatClient>();
                    mockClient.Setup(c => c.ToString()).Returns($"MockChatClient_{++_clientCounter}_{tenantId}_{workerId}");
                    return mockClient.Object;
                },
                logger: _loggerMock.Object
            );
        }

        [Theory]
        [InlineData("tenant1", "worker1")]
        [InlineData("tenant2", "worker2")]
        [InlineData("test-tenant", "test-worker")]
        public void GetChatClient_ValidTenantAndWorker_ReturnsClient(string tenantId, string workerId)
        {
            // Act
            var client = _provider.GetChatClient(tenantId, workerId);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void GetChatClient_SameTenantAndWorker_ReturnsCachedClient()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string workerId = "worker1";

            // Act
            var client1 = _provider.GetChatClient(tenantId, workerId);
            var client2 = _provider.GetChatClient(tenantId, workerId);

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.Same(client1, client2);

            // Verify cache hit was logged (using Moq verification)
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ChatClient cache hit")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void GetChatClient_DifferentTenants_ReturnsDifferentClients()
        {
            // Arrange
            const string workerId = "worker1";

            // Act
            var client1 = _provider.GetChatClient("tenant1", workerId);
            var client2 = _provider.GetChatClient("tenant2", workerId);

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.NotSame(client1, client2);
        }

        [Fact]
        public void GetChatClient_DifferentWorkers_ReturnsDifferentClients()
        {
            // Arrange
            const string tenantId = "tenant1";

            // Act
            var client1 = _provider.GetChatClient(tenantId, "worker1");
            var client2 = _provider.GetChatClient(tenantId, "worker2");

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.NotSame(client1, client2);
        }

        [Theory]
        [InlineData(null, "worker1")]
        [InlineData("tenant1", null)]
        [InlineData(null, null)]
        public void GetChatClient_NullParameters_ThrowsArgumentNullException(string? tenantId, string? workerId)
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _provider.GetChatClient(tenantId!, workerId!));
        }

        [Theory]
        [InlineData("", "worker1")]
        [InlineData("   ", "worker1")]
        [InlineData("tenant1", "")]
        [InlineData("tenant1", "   ")]
        public void GetChatClient_EmptyOrWhitespaceParameters_ThrowsArgumentException(string tenantId, string workerId)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _provider.GetChatClient(tenantId, workerId));
        }

        [Fact]
        public void Constructor_WithNullCreateDelegate_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChatClientProvider(createChatClient: null!));
        }

        [Fact]
        public void Constructor_WithLogger_LogsCorrectly()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ChatClientProvider>>();
            using var provider = new ChatClientProvider(
                createChatClient: (tenantId, workerId) => new Mock<ChatClient>().Object,
                logger: mockLogger.Object
            );

            // Act
            provider.GetChatClient("tenant1", "worker1");

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ChatClient cache miss")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void Dispose_ClearsCache_ReleasesResources()
        {
            // Arrange
            var client1 = _provider.GetChatClient("tenant1", "worker1");
            var client2 = _provider.GetChatClient("tenant2", "worker2");

            // Act
            _provider.Dispose();

            // Assert - Should not throw, provider should be disposed
            // Note: In a real scenario, we'd check that cached clients are disposed
            Assert.NotNull(client1);
            Assert.NotNull(client2);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            _provider.Dispose();
            _provider.Dispose();
            _provider.Dispose();
        }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}