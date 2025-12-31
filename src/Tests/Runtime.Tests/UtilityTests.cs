// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Agents.A365.Runtime.Utils;
using Moq;
using FluentAssertions;

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

        [Fact]
        public void GetDefaultHttpClient_WithoutFactory_CreatesNewHttpClient()
        {
            // Act
            using var httpClient = Utility.GetDefaultHttpClient();

            // Assert
            httpClient.Should().NotBeNull();
            httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(30));
            httpClient.DefaultRequestHeaders.UserAgent.Should().NotBeEmpty();
            httpClient.DefaultRequestHeaders.UserAgent.ToString().Should().Contain("Agent365SDK/");
        }

        [Fact]
        public void GetDefaultHttpClient_WithFactory_UsesFactory()
        {
            // Arrange
            var mockFactory = new Mock<IHttpClientFactory>();
            using var expectedClient = new HttpClient();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(expectedClient);

            // Act
            var httpClient = Utility.GetDefaultHttpClient(mockFactory.Object);

            // Assert
            httpClient.Should().BeSameAs(expectedClient);
            mockFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void GetDefaultHttpClient_DefaultTimeout_Is30Seconds()
        {
            // Act
            using var httpClient = Utility.GetDefaultHttpClient();

            // Assert
            httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        }

        [Fact]
        public void GetDefaultHttpClient_CustomTimeout_IsApplied()
        {
            // Arrange
            const int customTimeout = 60;

            // Act
            using var httpClient = Utility.GetDefaultHttpClient(timeoutSeconds: customTimeout);

            // Assert
            httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(customTimeout));
        }

        [Fact]
        public void GetDefaultHttpClient_DefaultUserAgentConfiguration_IsApplied()
        {
            // Act
            using var httpClient = Utility.GetDefaultHttpClient();

            // Assert
            var userAgent = httpClient.DefaultRequestHeaders.UserAgent.ToString();
            userAgent.Should().Contain("Agent365SDK/");
            userAgent.Should().MatchRegex(@"^Agent365SDK/.+ \(.+; .NET \d+(\.\d+)*\)$");
        }

        [Fact]
        public void GetDefaultHttpClient_CustomUserAgentConfiguration_IsApplied()
        {
            // Arrange
            var mockConfig = new Mock<IUserAgentConfiguration>();
            mockConfig.Setup(c => c.ProductName).Returns("CustomProduct");
            mockConfig.Setup(c => c.Version).Returns("1.2.3");
            mockConfig.Setup(c => c.OrchestratorName).Returns("TestOrchestrator");

            // Act
            using var httpClient = Utility.GetDefaultHttpClient(userAgentConfiguration: mockConfig.Object);

            // Assert
            var userAgent = httpClient.DefaultRequestHeaders.UserAgent.ToString();
            userAgent.Should().Contain("CustomProduct/1.2.3");
            userAgent.Should().Contain("TestOrchestrator");
        }

        [Fact]
        public void GetDefaultHttpClient_WithAllParameters_ConfiguresCorrectly()
        {
            // Arrange
            var mockFactory = new Mock<IHttpClientFactory>();
            using var expectedClient = new HttpClient();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(expectedClient);

            var mockConfig = new Mock<IUserAgentConfiguration>();
            mockConfig.Setup(c => c.ProductName).Returns("TestProduct");
            mockConfig.Setup(c => c.Version).Returns("2.0.0");
            mockConfig.Setup(c => c.OrchestratorName).Returns((string?)null);

            const int customTimeout = 45;

            // Act
            var httpClient = Utility.GetDefaultHttpClient(
                mockFactory.Object, 
                mockConfig.Object, 
                customTimeout);

            // Assert
            httpClient.Should().BeSameAs(expectedClient);
            httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(customTimeout));
            var userAgent = httpClient.DefaultRequestHeaders.UserAgent.ToString();
            userAgent.Should().Contain("TestProduct/2.0.0");
        }
    }
}
