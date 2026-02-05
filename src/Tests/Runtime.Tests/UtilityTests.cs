// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Agents.A365.Runtime.Utils;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for Utility class.
    /// Tests utility methods including authentication scopes, environment detection, and User-Agent header generation.
    /// </summary>
    public class UtilityTests
    {
        [Fact]
        public void GetMcpPlatformAuthenticationScope_ReturnsDefault_WhenConfigMissing()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"]).Returns((string?)null);

            // Act
            var result = Utility.GetMcpPlatformAuthenticationScope(configMock.Object);

            // Assert
            Assert.Equal("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default", result);
        }

        [Fact]
        public void GetMcpPlatformAuthenticationScope_ReturnsConfigValue_WhenPresent()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"]).Returns("custom_scope");

            // Act
            var result = Utility.GetMcpPlatformAuthenticationScope(configMock.Object);

            // Assert
            Assert.Equal("custom_scope", result);
        }

        [Fact]
        public void GetCurrentEnvironment_ReturnsAspNetCoreEnv()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Production");

            // Act
            var result = Utility.GetCurrentEnvironment(configMock.Object);

            // Assert
            Assert.Equal("Production", result);
        }

        [Fact]
        public void GetCurrentEnvironment_ReturnsDotNetEnv_WhenAspNetCoreMissing()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns((string?)null);
            configMock.Setup(c => c["DOTNET_ENVIRONMENT"]).Returns("Staging");

            // Act
            var result = Utility.GetCurrentEnvironment(configMock.Object);

            // Assert
            Assert.Equal("Staging", result);
        }

        [Fact]
        public void GetCurrentEnvironment_ReturnsDevelopment_WhenConfigMissing()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns((string?)null);
            configMock.Setup(c => c["DOTNET_ENVIRONMENT"]).Returns((string?)null);

            // Act
            var result = Utility.GetCurrentEnvironment(configMock.Object);

            // Assert
            Assert.Equal("Development", result);
        }

        #region GetAgentIdFromToken Tests

        [Fact]
        public void GetAgentIdFromToken_ReturnsEmptyString_WhenTokenIsEmpty()
        {
            // Act
            var result = Utility.GetAgentIdFromToken("");

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetAgentIdFromToken_ReturnsEmptyString_WhenTokenIsWhitespace()
        {
            // Act
            var result = Utility.GetAgentIdFromToken("   ");

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetAgentIdFromToken_ReturnsEmptyString_WhenTokenIsNull()
        {
            // Act
            var result = Utility.GetAgentIdFromToken(null!);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetAgentIdFromToken_ReturnsBlueprintId_WhenXmsParAppAzpPresent()
        {
            // Arrange
            var token = CreateTestJwtToken(new Claim("xms_par_app_azp", "blueprint-id-123"),
                                           new Claim("appid", "app-id-456"),
                                           new Claim("azp", "azp-id-789"));

            // Act
            var result = Utility.GetAgentIdFromToken(token);

            // Assert
            Assert.Equal("blueprint-id-123", result);
        }

        [Fact]
        public void GetAgentIdFromToken_ReturnsAppId_WhenXmsParAppAzpNotPresent()
        {
            // Arrange
            var token = CreateTestJwtToken(new Claim("appid", "app-id-456"),
                                           new Claim("azp", "azp-id-789"));

            // Act
            var result = Utility.GetAgentIdFromToken(token);

            // Assert
            Assert.Equal("app-id-456", result);
        }

        [Fact]
        public void GetAgentIdFromToken_ReturnsAzp_WhenOnlyAzpPresent()
        {
            // Arrange
            var token = CreateTestJwtToken(new Claim("azp", "azp-id-789"));

            // Act
            var result = Utility.GetAgentIdFromToken(token);

            // Assert
            Assert.Equal("azp-id-789", result);
        }

        [Fact]
        public void GetAgentIdFromToken_ReturnsEmptyString_WhenNoRelevantClaimsPresent()
        {
            // Arrange
            var token = CreateTestJwtToken(new Claim("sub", "some-subject"),
                                           new Claim("iss", "some-issuer"));

            // Act
            var result = Utility.GetAgentIdFromToken(token);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetAgentIdFromToken_ReturnsEmptyString_WhenTokenIsMalformed()
        {
            // Act
            var result = Utility.GetAgentIdFromToken("not-a-valid-jwt-token");

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetAgentIdFromToken_PrefersXmsParAppAzp_OverAppId()
        {
            // Arrange - both present, xms_par_app_azp should win
            var token = CreateTestJwtToken(new Claim("appid", "app-id-first"),
                                           new Claim("xms_par_app_azp", "blueprint-id-second"));

            // Act
            var result = Utility.GetAgentIdFromToken(token);

            // Assert
            Assert.Equal("blueprint-id-second", result);
        }

        [Fact]
        public void GetAgentIdFromToken_FallsBackToAppId_WhenXmsParAppAzpIsEmpty()
        {
            // Arrange
            var token = CreateTestJwtToken(new Claim("xms_par_app_azp", ""),
                                           new Claim("appid", "app-id-456"));

            // Act
            var result = Utility.GetAgentIdFromToken(token);

            // Assert
            Assert.Equal("app-id-456", result);
        }

        [Fact]
        public void GetAgentIdFromToken_FallsBackToAzp_WhenBothXmsParAppAzpAndAppIdAreEmpty()
        {
            // Arrange
            var token = CreateTestJwtToken(new Claim("xms_par_app_azp", ""),
                                           new Claim("appid", ""),
                                           new Claim("azp", "azp-id-789"));

            // Act
            var result = Utility.GetAgentIdFromToken(token);

            // Assert
            Assert.Equal("azp-id-789", result);
        }

        #endregion

        #region GetApplicationName Tests

        [Fact]
        public void GetApplicationName_ReturnsAssemblyName()
        {
            // Act
            var result = Utility.GetApplicationName();

            // Assert
            // In a test context, the entry assembly should exist and have a name
            // The exact name depends on the test runner, so we just verify it's not null
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a test JWT token with the specified claims.
        /// Note: This creates an unsigned token suitable for testing claim extraction only.
        /// </summary>
        private static string CreateTestJwtToken(params Claim[] claims)
        {
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var payloadDict = claims
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.First().Value);

            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadDict);
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            return $"{header}.{payload}.";
        }

        #endregion
    }
}
