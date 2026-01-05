// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Agents.A365.Runtime.Utils;
using Moq;

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
    }
}
