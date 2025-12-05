// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Runtime.Utils;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for AgenticAuthenticationService class.
    /// Tests the authentication token retrieval and utility methods.
    /// </summary>
    public class AgenticAuthenticationServiceTests
    {
        [Theory]
        [InlineData("custom-scope", "custom-scope")]
        [InlineData("SKIP", "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default")]
        [InlineData("", "")] // Empty string is returned as-is (not replaced with default)
        [InlineData("   ", "   ")] // Whitespace is returned as-is (not replaced with default)
        [InlineData("https://custom.scope/.default", "https://custom.scope/.default")]
        public void GetMcpPlatformAuthenticationScope_WithVariousConfigurations_ReturnsExpectedScope(
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

        [Theory]
        [InlineData("Production", "SKIP", "Production")]
        [InlineData("Staging", "Development", "Staging")]
        [InlineData("SKIP", "Production", "Production")]
        [InlineData("SKIP", "SKIP", "Development")]
        public void GetCurrentEnvironment_WithVariousConfigurations_ReturnsExpectedEnvironment(
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

        [Fact]
        public void GetMcpPlatformAuthenticationScope_ConfigurationReturnsNull_ReturnsDefaultScope()
        {
            // Arrange
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns((string?)null);

            // Act
            var result = Utility.GetMcpPlatformAuthenticationScope(mockConfiguration.Object);

            // Assert
            Assert.Equal("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default", result);
        }

        [Fact]
        public void GetCurrentEnvironment_AllEnvironmentVariablesNull_ReturnsDefaultDevelopment()
        {
            // Arrange
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns((string?)null);
            mockConfiguration.Setup(c => c["DOTNET_ENVIRONMENT"]).Returns((string?)null);

            // Act
            var result = Utility.GetCurrentEnvironment(mockConfiguration.Object);

            // Assert
            Assert.Equal("Development", result);
        }

        [Theory]
        [InlineData("scope1", 1)]
        [InlineData("scope2", 2)]
        [InlineData("scope3", 3)]
        public void GetMcpPlatformAuthenticationScope_CalledMultipleTimes_AccessesConfigurationCorrectly(
            string testScope,
            int callCount)
        {
            // Arrange
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"])
                .Returns(testScope);

            // Act
            for (int i = 0; i < callCount; i++)
            {
                Utility.GetMcpPlatformAuthenticationScope(mockConfiguration.Object);
            }

            // Assert
            mockConfiguration.Verify(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"], Times.Exactly(callCount));
        }
    }
}