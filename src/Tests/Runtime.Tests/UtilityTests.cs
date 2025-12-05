// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Runtime.Utils;
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

        [Theory]
        [InlineData("custom-scope", "custom-scope")] // Custom value returned as-is
        [InlineData("SKIP", "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default")] // Null returns default
        [InlineData("", "")] // Empty string returned as-is
        public void GetMcpPlatformAuthenticationScope_WithVariousConfigurationValues_ReturnsExpectedScope(
            string? configValue, 
            string expectedScope)
        {
            // Arrange
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns(configValue == "SKIP" ? null : configValue);

            // Act
            var result = Utility.GetMcpPlatformAuthenticationScope(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedScope, result);
        }

        #endregion

        #region GetCurrentEnvironment Tests

        [Theory]
        [InlineData("Production", "SKIP", "Production")] // ASPNETCORE_ENVIRONMENT takes precedence
        [InlineData("SKIP", "Development", "Development")] // Falls back to DOTNET_ENVIRONMENT
        [InlineData("SKIP", "SKIP", "Development")] // Both null returns default
        [InlineData("", "", "")] // Empty strings returned as-is
        public void GetCurrentEnvironment_WithVariousEnvironmentVariables_ReturnsExpectedEnvironment(
            string? aspNetCoreEnv, 
            string? dotNetEnv, 
            string expectedEnvironment)
        {
            // Arrange
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["ASPNETCORE_ENVIRONMENT"])
                .Returns(aspNetCoreEnv == "SKIP" ? null : aspNetCoreEnv);
            mockConfiguration.Setup(c => c["DOTNET_ENVIRONMENT"])
                .Returns(dotNetEnv == "SKIP" ? null : dotNetEnv);

            // Act
            var result = Utility.GetCurrentEnvironment(mockConfiguration.Object);

            // Assert
            Assert.Equal(expectedEnvironment, result);
        }

        #endregion

        #region GetAppIdFromToken Tests

        [Theory]
        [InlineData("12345678-1234-1234-1234-123456789abc", "SKIP", "12345678-1234-1234-1234-123456789abc")] // appid claim only
        [InlineData("SKIP", "87654321-4321-4321-4321-cba987654321", "87654321-4321-4321-4321-cba987654321")] // azp claim only
        [InlineData("appid-value", "azp-value", "appid-value")] // appid takes precedence over azp
        [InlineData("SKIP", "SKIP", "")] // No claims returns empty string
        public void GetAppIdFromToken_WithValidTokensContainingVariousClaims_ReturnsExpectedAppId(
            string? appIdValue,
            string? azpValue,
            string expectedAppId)
        {
            // Arrange
            var claims = new Dictionary<string, object> { { "aud", "test-audience" } };

            if (appIdValue != "SKIP" && appIdValue != null)
                claims.Add("appid", appIdValue);
            if (azpValue != "SKIP" && azpValue != null)
                claims.Add("azp", azpValue);

            var token = CreateJwtToken(claims);

            // Act
            var result = Utility.GetAppIdFromToken(token);

            // Assert
            Assert.Equal(expectedAppId, result);
        }

        [Theory]
        [InlineData("")] // Empty string
        [InlineData("   ")] // Whitespace
        public void GetAppIdFromToken_WithNullOrWhitespaceToken_ReturnsEmptyGuid(string token)
        {
            // Act
            var result = Utility.GetAppIdFromToken(token);

            // Assert
            Assert.Equal(Guid.Empty.ToString(), result);
        }

        [Fact]
        public void GetAppIdFromToken_WithNullToken_ReturnsEmptyGuid()
        {
            // Act
            var result = Utility.GetAppIdFromToken(null!);

            // Assert
            Assert.Equal(Guid.Empty.ToString(), result);
        }

        [Theory]
        [InlineData("invalid-token")] // Invalid format
        [InlineData("not.a.jwt")] // Wrong number of parts
        public void GetAppIdFromToken_WithInvalidTokenFormats_ThrowsException(string invalidToken)
        {
            // Act & Assert
            Assert.ThrowsAny<Exception>(() => Utility.GetAppIdFromToken(invalidToken));
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
