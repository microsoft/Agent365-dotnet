using Microsoft.Extensions.Configuration;
using Microsoft.Agents.A365.Runtime.Utils;
using System.Reflection;
using Xunit;
using Moq;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    public class UtilityTests
    {
        [Fact]
        public void GetMcpPlatformAuthenticationScope_ReturnsDefault_WhenConfigMissing()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"]).Returns((string?)null);
            var result = Utility.GetMcpPlatformAuthenticationScope(configMock.Object);
            Assert.Equal("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default", result);
        }

        [Fact]
        public void GetMcpPlatformAuthenticationScope_ReturnsConfigValue_WhenPresent()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["MCP_PLATFORM_AUTHENTICATION_SCOPE"]).Returns("custom_scope");
            var result = Utility.GetMcpPlatformAuthenticationScope(configMock.Object);
            Assert.Equal("custom_scope", result);
        }

        [Fact]
        public void GetCurrentEnvironment_ReturnsAspNetCoreEnv()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Production");
            var result = Utility.GetCurrentEnvironment(configMock.Object);
            Assert.Equal("Production", result);
        }

        [Fact]
        public void GetCurrentEnvironment_ReturnsDotNetEnv_WhenAspNetCoreMissing()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns((string?)null);
            configMock.Setup(c => c["DOTNET_ENVIRONMENT"]).Returns("Staging");
            var result = Utility.GetCurrentEnvironment(configMock.Object);
            Assert.Equal("Staging", result);
        }

        [Fact]
        public void GetCurrentEnvironment_ReturnsDevelopment_WhenConfigMissing()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns((string?)null);
            configMock.Setup(c => c["DOTNET_ENVIRONMENT"]).Returns((string?)null);
            var result = Utility.GetCurrentEnvironment(configMock.Object);
            Assert.Equal("Development", result);
        }

        [Fact]
        public void GetUserAgentHeader_ReturnsExpectedFormat()
        {
            var userAgent = Utility.GetUserAgentHeader();
            Console.WriteLine(userAgent);

            // Regex: Agent365SDK/{version} ({osType}; Dotnet/{dotnetVersion})
            var pattern = @"^Agent365SDK/.+ \([^)]+; Dotnet/\d+(\.\d+)*\)$";
            Assert.Matches(pattern, userAgent);
        }

        [Fact]
        public void GetUserAgentHeader_ReturnsExpectedFormat_WithOrchestrator()
        {
            var userAgent = Utility.GetUserAgentHeader("TestOrchestrator");
            Console.WriteLine(userAgent);

            // Regex: Agent365SDK/{version} ({osType}; Dotnet/{dotnetVersion}; TestOrchestrator)
            var pattern = @"^Agent365SDK/.+ \([^)]+; Dotnet/\d+(\.\d+)*; TestOrchestrator\)$";
            Assert.Matches(pattern, userAgent);
        }
    }
}
