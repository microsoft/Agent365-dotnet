// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Runtime.Authentication;
using Microsoft.Agents.A365.Runtime.Utils;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for AgenticAuthenticationService class.
    /// Tests the authentication token retrieval functionality for agentic users.
    /// </summary>
    public class AgenticAuthenticationServiceTests
    {
        [Fact]
        public async Task GetAgenticUserTokenAsync_WithValidParameters_ReturnsToken()
        {
            // Arrange
            const string expectedToken = "test-token-123";
            const string authHandlerName = "test-handler";
            const string testScope = "test-scope";
            
            var mockUserAuthorization = new Mock<UserAuthorization>();
            var mockTurnContext = new Mock<ITurnContext>();
            var mockConfiguration = new Mock<IConfiguration>();

            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns(testScope);

            mockUserAuthorization
                .Setup(ua => ua.ExchangeTurnTokenAsync(
                    It.IsAny<ITurnContext>(),
                    It.Is<string>(s => s == authHandlerName),
                    It.Is<List<string>>(scopes => scopes.Count == 1 && scopes[0] == testScope)))
                .ReturnsAsync(expectedToken);

            // Act
            var result = await AgenticAuthenticationService.GetAgenticUserTokenAsync(
                mockUserAuthorization.Object,
                authHandlerName,
                mockTurnContext.Object,
                mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedToken, result);
            mockUserAuthorization.Verify(ua => ua.ExchangeTurnTokenAsync(
                mockTurnContext.Object,
                authHandlerName,
                It.Is<List<string>>(scopes => scopes.Count == 1 && scopes[0] == testScope)), 
                Times.Once);
        }

        [Fact]
        public async Task GetAgenticUserTokenAsync_WithNullConfiguration_UsesDefaultScope()
        {
            // Arrange
            const string expectedToken = "test-token-456";
            const string authHandlerName = "test-handler";
            const string defaultScope = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default";
            
            var mockUserAuthorization = new Mock<UserAuthorization>();
            var mockTurnContext = new Mock<ITurnContext>();
            var mockConfiguration = new Mock<IConfiguration>();

            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns((string?)null);

            mockUserAuthorization
                .Setup(ua => ua.ExchangeTurnTokenAsync(
                    It.IsAny<ITurnContext>(),
                    It.Is<string>(s => s == authHandlerName),
                    It.Is<List<string>>(scopes => scopes.Count == 1 && scopes[0] == defaultScope)))
                .ReturnsAsync(expectedToken);

            // Act
            var result = await AgenticAuthenticationService.GetAgenticUserTokenAsync(
                mockUserAuthorization.Object,
                authHandlerName,
                mockTurnContext.Object,
                mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedToken, result);
            mockUserAuthorization.Verify(ua => ua.ExchangeTurnTokenAsync(
                mockTurnContext.Object,
                authHandlerName,
                It.Is<List<string>>(scopes => scopes.Count == 1 && scopes[0] == defaultScope)), 
                Times.Once);
        }

        [Fact]
        public async Task GetAgenticUserTokenAsync_WithEmptyAuthHandlerName_PassesEmptyString()
        {
            // Arrange
            const string expectedToken = "test-token-789";
            const string authHandlerName = "";
            const string testScope = "test-scope";
            
            var mockUserAuthorization = new Mock<UserAuthorization>();
            var mockTurnContext = new Mock<ITurnContext>();
            var mockConfiguration = new Mock<IConfiguration>();

            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns(testScope);

            mockUserAuthorization
                .Setup(ua => ua.ExchangeTurnTokenAsync(
                    It.IsAny<ITurnContext>(),
                    It.Is<string>(s => s == authHandlerName),
                    It.Is<List<string>>(scopes => scopes.Count == 1 && scopes[0] == testScope)))
                .ReturnsAsync(expectedToken);

            // Act
            var result = await AgenticAuthenticationService.GetAgenticUserTokenAsync(
                mockUserAuthorization.Object,
                authHandlerName,
                mockTurnContext.Object,
                mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedToken, result);
        }

        [Fact]
        public async Task GetAgenticUserTokenAsync_WhenUserAuthorizationThrows_PropagatesException()
        {
            // Arrange
            const string authHandlerName = "test-handler";
            var expectedException = new InvalidOperationException("Test exception");
            
            var mockUserAuthorization = new Mock<UserAuthorization>();
            var mockTurnContext = new Mock<ITurnContext>();
            var mockConfiguration = new Mock<IConfiguration>();

            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns("test-scope");

            mockUserAuthorization
                .Setup(ua => ua.ExchangeTurnTokenAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<string>(),
                    It.IsAny<List<string>>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AgenticAuthenticationService.GetAgenticUserTokenAsync(
                    mockUserAuthorization.Object,
                    authHandlerName,
                    mockTurnContext.Object,
                    mockConfiguration.Object));
            
            Assert.Same(expectedException, actualException);
        }

        [Fact]
        public async Task GetAgenticUserTokenAsync_CallsUtilityGetMcpPlatformAuthenticationScope()
        {
            // Arrange
            const string expectedToken = "test-token-utility";
            const string authHandlerName = "test-handler";
            const string testScope = "custom-scope-from-utility";
            
            var mockUserAuthorization = new Mock<UserAuthorization>();
            var mockTurnContext = new Mock<ITurnContext>();
            var mockConfiguration = new Mock<IConfiguration>();

            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns(testScope);

            mockUserAuthorization
                .Setup(ua => ua.ExchangeTurnTokenAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<string>(),
                    It.Is<List<string>>(scopes => scopes.Count == 1 && scopes[0] == testScope)))
                .ReturnsAsync(expectedToken);

            // Act
            var result = await AgenticAuthenticationService.GetAgenticUserTokenAsync(
                mockUserAuthorization.Object,
                authHandlerName,
                mockTurnContext.Object,
                mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedToken, result);
            
            // Verify that the scope passed matches what Utility.GetMcpPlatformAuthenticationScope would return
            mockConfiguration.Verify(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"], Times.Once);
        }
    }
}