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
        [InlineData("custom-scope-1", "custom-scope-1")]
        [InlineData("custom-scope-2", "custom-scope-2")]
        [InlineData("https://api.example.com/.default", "https://api.example.com/.default")]
        [InlineData("SKIP", "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default")]
        [InlineData("", "")] // Empty string is returned as-is (not null)
        [InlineData("   ", "   ")] // Whitespace is returned as-is (not null)
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
        [InlineData("Production", "SKIP", "Production")]
        [InlineData("Staging", "Development", "Staging")]
        [InlineData("Testing", "Staging", "Testing")]
        [InlineData("SKIP", "Production", "Production")]
        [InlineData("SKIP", "Development", "Development")]
        [InlineData("SKIP", "SKIP", "Development")]
        [InlineData("", "", "")] // Empty strings are returned as-is (not replaced with default)
        [InlineData("   ", "   ", "   ")] // Whitespace is returned as-is (not replaced with default)
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
        [InlineData("12345678-1234-1234-1234-123456789abc", "SKIP", "12345678-1234-1234-1234-123456789abc")]
        [InlineData("SKIP", "87654321-4321-4321-4321-cba987654321", "87654321-4321-4321-4321-cba987654321")]
        [InlineData("appid-value", "azp-value", "appid-value")] // appid takes precedence
        [InlineData("SKIP", "SKIP", "")]
        public void GetAppIdFromToken_WithValidTokensContainingVariousClaims_ReturnsExpectedAppId(
            string? appIdValue,
            string? azpValue,
            string expectedAppId)
        {
            // Arrange
            var claims = new Dictionary<string, object>
            {
                { "aud", "test-audience" }
            };

            if (appIdValue != null && appIdValue != "SKIP")
                claims.Add("appid", appIdValue);
            if (azpValue != null && azpValue != "SKIP")
                claims.Add("azp", azpValue);

            var token = CreateJwtToken(claims);

            // Act
            var result = Utility.GetAppIdFromToken(token);

            // Assert
            Assert.Equal(expectedAppId, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void GetAppIdFromToken_WithWhitespaceToken_ReturnsEmptyGuid(string token)
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
        [InlineData("invalid-token")]
        [InlineData("not.a.jwt")]
        [InlineData("header.payload")]
        [InlineData("just-text")]
        [InlineData("123456")]
        public void GetAppIdFromToken_WithInvalidTokenFormats_ThrowsException(string invalidToken)
        {
            // Act & Assert - Expects SecurityTokenMalformedException or ArgumentException
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
