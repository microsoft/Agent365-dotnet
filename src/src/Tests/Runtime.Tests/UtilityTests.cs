// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Runtime.Utils;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for Utility class.
    /// Tests configuration retrieval, environment detection, and token handling utilities.
    /// </summary>
    public class UtilityTests
    {
        #region GetMcpPlatformAuthenticationScope Tests

        [Fact]
        public void GetMcpPlatformAuthenticationScope_WithConfigurationValue_ReturnsConfigurationValue()
        {
            // Arrange
            const string expectedScope = "custom-scope-from-config";
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns(expectedScope);

            // Act
            var result = Utility.GetMcpPlatformAuthenticationScope(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedScope, result);
        }

        [Fact]
        public void GetMcpPlatformAuthenticationScope_WithNullConfigurationValue_ReturnsDefaultScope()
        {
            // Arrange
            const string expectedDefaultScope = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default";
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns((string?)null);

            // Act
            var result = Utility.GetMcpPlatformAuthenticationScope(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedDefaultScope, result);
        }

        [Fact]
        public void GetMcpPlatformAuthenticationScope_WithEmptyConfigurationValue_ReturnsDefaultScope()
        {
            // Arrange
            const string expectedDefaultScope = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default";
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns(string.Empty);

            // Act
            var result = Utility.GetMcpPlatformAuthenticationScope(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedDefaultScope, result);
        }

        #endregion

        #region GetCurrentEnvironment Tests

        [Fact]
        public void GetCurrentEnvironment_WithAspNetCoreEnvironment_ReturnsAspNetCoreValue()
        {
            // Arrange
            const string expectedEnvironment = "Production";
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["ASPNETCORE_ENVIRONMENT"])
                .Returns(expectedEnvironment);
            mockConfiguration.Setup(c => c["DOTNET_ENVIRONMENT"])
                .Returns("SomeOtherValue");

            // Act
            var result = Utility.GetCurrentEnvironment(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedEnvironment, result);
        }

        [Fact]
        public void GetCurrentEnvironment_WithDotNetEnvironmentOnly_ReturnsDotNetValue()
        {
            // Arrange
            const string expectedEnvironment = "Staging";
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["ASPNETCORE_ENVIRONMENT"])
                .Returns((string?)null);
            mockConfiguration.Setup(c => c["DOTNET_ENVIRONMENT"])
                .Returns(expectedEnvironment);

            // Act
            var result = Utility.GetCurrentEnvironment(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedEnvironment, result);
        }

        [Fact]
        public void GetCurrentEnvironment_WithNoEnvironmentVariables_ReturnsDefaultDevelopment()
        {
            // Arrange
            const string expectedDefaultEnvironment = "Development";
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["ASPNETCORE_ENVIRONMENT"])
                .Returns((string?)null);
            mockConfiguration.Setup(c => c["DOTNET_ENVIRONMENT"])
                .Returns((string?)null);

            // Act
            var result = Utility.GetCurrentEnvironment(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedDefaultEnvironment, result);
        }

        [Fact]
        public void GetCurrentEnvironment_WithEmptyEnvironmentVariables_ReturnsDefaultDevelopment()
        {
            // Arrange
            const string expectedDefaultEnvironment = "Development";
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["ASPNETCORE_ENVIRONMENT"])
                .Returns(string.Empty);
            mockConfiguration.Setup(c => c["DOTNET_ENVIRONMENT"])
                .Returns(string.Empty);

            // Act
            var result = Utility.GetCurrentEnvironment(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedDefaultEnvironment, result);
        }

        #endregion

        #region GetAppIdFromToken Tests

        [Fact]
        public void GetAppIdFromToken_WithValidTokenContainingAppId_ReturnsAppId()
        {
            // Arrange
            const string expectedAppId = "12345678-1234-1234-1234-123456789abc";
            var token = CreateJwtToken(new Dictionary<string, object>
            {
                { "appid", expectedAppId },
                { "aud", "test-audience" }
            });

            // Act
            var result = Utility.GetAppIdFromToken(token);

            // Assert
            Assert.Equal(expectedAppId, result);
        }

        [Fact]
        public void GetAppIdFromToken_WithValidTokenContainingAzp_ReturnsAzp()
        {
            // Arrange
            const string expectedAppId = "87654321-4321-4321-4321-cba987654321";
            var token = CreateJwtToken(new Dictionary<string, object>
            {
                { "azp", expectedAppId },
                { "aud", "test-audience" }
            });

            // Act
            var result = Utility.GetAppIdFromToken(token);

            // Assert
            Assert.Equal(expectedAppId, result);
        }

        [Fact]
        public void GetAppIdFromToken_WithBothAppIdAndAzp_PrioritizesAppId()
        {
            // Arrange
            const string appIdValue = "appid-value";
            const string azpValue = "azp-value";
            var token = CreateJwtToken(new Dictionary<string, object>
            {
                { "appid", appIdValue },
                { "azp", azpValue },
                { "aud", "test-audience" }
            });

            // Act
            var result = Utility.GetAppIdFromToken(token);

            // Assert
            Assert.Equal(appIdValue, result);
        }

        [Fact]
        public void GetAppIdFromToken_WithNoAppIdOrAzp_ReturnsEmptyString()
        {
            // Arrange
            var token = CreateJwtToken(new Dictionary<string, object>
            {
                { "aud", "test-audience" },
                { "sub", "test-subject" }
            });

            // Act
            var result = Utility.GetAppIdFromToken(token);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetAppIdFromToken_WithNullToken_ReturnsEmptyGuid()
        {
            // Act
            var result = Utility.GetAppIdFromToken(null);

            // Assert
            Assert.Equal(Guid.Empty.ToString(), result);
        }

        [Fact]
        public void GetAppIdFromToken_WithEmptyToken_ReturnsEmptyGuid()
        {
            // Act
            var result = Utility.GetAppIdFromToken(string.Empty);

            // Assert
            Assert.Equal(Guid.Empty.ToString(), result);
        }

        [Fact]
        public void GetAppIdFromToken_WithWhitespaceToken_ReturnsEmptyGuid()
        {
            // Act
            var result = Utility.GetAppIdFromToken("   ");

            // Assert
            Assert.Equal(Guid.Empty.ToString(), result);
        }

        [Theory]
        [InlineData("invalid-token")]
        [InlineData("not.a.jwt")]
        [InlineData("header.payload")]
        public void GetAppIdFromToken_WithInvalidToken_ThrowsException(string invalidToken)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => Utility.GetAppIdFromToken(invalidToken));
        }

        #endregion

        #region ResolveAgentIdentity Tests

        [Fact]
        public void ResolveAgentIdentity_WithAgenticRequest_ReturnsAgenticInstanceId()
        {
            // Arrange
            const string expectedAgenticInstanceId = "agentic-instance-123";
            const string authToken = "dummy-token";
            
            var mockTurnContext = new Mock<ITurnContext>();
            var mockActivity = new Mock<IActivity>();
            
            mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);
            mockActivity.Setup(a => a.IsAgenticRequest()).Returns(true);
            mockActivity.Setup(a => a.GetAgenticInstanceId()).Returns(expectedAgenticInstanceId);

            // Act
            var result = Utility.ResolveAgentIdentity(mockTurnContext.Object, authToken);

            // Assert
            Assert.Equal(expectedAgenticInstanceId, result);
            mockActivity.Verify(a => a.IsAgenticRequest(), Times.Once);
            mockActivity.Verify(a => a.GetAgenticInstanceId(), Times.Once);
        }

        [Fact]
        public void ResolveAgentIdentity_WithNonAgenticRequest_ReturnsAppIdFromToken()
        {
            // Arrange
            const string expectedAppId = "token-app-id-456";
            var authToken = CreateJwtToken(new Dictionary<string, object>
            {
                { "appid", expectedAppId }
            });
            
            var mockTurnContext = new Mock<ITurnContext>();
            var mockActivity = new Mock<IActivity>();
            
            mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);
            mockActivity.Setup(a => a.IsAgenticRequest()).Returns(false);

            // Act
            var result = Utility.ResolveAgentIdentity(mockTurnContext.Object, authToken);

            // Assert
            Assert.Equal(expectedAppId, result);
            mockActivity.Verify(a => a.IsAgenticRequest(), Times.Once);
            mockActivity.Verify(a => a.GetAgenticInstanceId(), Times.Never);
        }

        [Fact]
        public void ResolveAgentIdentity_WithNonAgenticRequestAndInvalidToken_ReturnsEmptyString()
        {
            // Arrange
            const string authToken = "invalid-token";
            
            var mockTurnContext = new Mock<ITurnContext>();
            var mockActivity = new Mock<IActivity>();
            
            mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);
            mockActivity.Setup(a => a.IsAgenticRequest()).Returns(false);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Utility.ResolveAgentIdentity(mockTurnContext.Object, authToken));
        }

        [Fact]
        public void ResolveAgentIdentity_WithNonAgenticRequestAndNullToken_ReturnsEmptyGuid()
        {
            // Arrange
            string? authToken = null;
            
            var mockTurnContext = new Mock<ITurnContext>();
            var mockActivity = new Mock<IActivity>();
            
            mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);
            mockActivity.Setup(a => a.IsAgenticRequest()).Returns(false);

            // Act
            var result = Utility.ResolveAgentIdentity(mockTurnContext.Object, authToken);

            // Assert
            Assert.Equal(Guid.Empty.ToString(), result);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a JWT token with the specified claims for testing purposes.
        /// </summary>
        /// <param name="claims">Dictionary of claims to include in the token.</param>
        /// <returns>A JWT token string.</returns>
        private static string CreateJwtToken(Dictionary<string, object> claims)
        {
            var handler = new JwtSecurityTokenHandler();
            var claimsList = claims.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!)).ToList();
            
            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claimsList),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("test-key-that-is-long-enough-for-hmac-sha256")),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
            };

            var token = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }

        #endregion
    }
}