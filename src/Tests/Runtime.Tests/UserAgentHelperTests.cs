// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Runtime;
using Moq;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for UserAgentHelper class.
    /// Tests User-Agent header generation with various configurations.
    /// </summary>
    public class UserAgentHelperTests
    {
        [Fact]
        public void BuildUserAgent_ReturnsExpectedFormat_WithDefaultConfiguration()
        {
            // Arrange
            var config = new Agent365SdkUserAgentConfiguration();

            // Act
            var userAgent = UserAgentHelper.BuildUserAgent(config);

            // Assert
            // Regex: Agent365SDK/{version} ({osType}; .NET {dotnetVersion})
            var pattern = @"^Agent365SDK/.+ \(.+; .NET \d+(\.\d+)*\)$";
            userAgent.Should().MatchRegex(pattern);
            userAgent.Should().Contain("Agent365SDK/");
            userAgent.Should().NotContain(";;"); // No double semicolons when no orchestrator
        }

        [Fact]
        public void BuildUserAgent_ReturnsExpectedFormat_WithOrchestrator()
        {
            // Arrange
            var config = new Agent365SdkUserAgentConfiguration("TestOrchestrator");

            // Act
            var userAgent = UserAgentHelper.BuildUserAgent(config);

            // Assert
            // Regex: Agent365SDK/{version} ({osType}; .NET {dotnetVersion}; TestOrchestrator)
            var pattern = @"^Agent365SDK/.+ \(.+; .NET \d+(\.\d+)*; TestOrchestrator\)$";
            userAgent.Should().MatchRegex(pattern);
            userAgent.Should().Contain("TestOrchestrator");
        }

        [Fact]
        public void BuildUserAgent_HandlesEmptyOrchestratorName()
        {
            // Arrange
            var config = new Agent365SdkUserAgentConfiguration("");

            // Act
            var userAgent = UserAgentHelper.BuildUserAgent(config);

            // Assert
            var pattern = @"^Agent365SDK/.+ \(.+; .NET \d+(\.\d+)*\)$";
            userAgent.Should().MatchRegex(pattern);
            userAgent.Should().NotContain(";;");
        }

        [Fact]
        public void BuildUserAgent_HandlesNullOrchestratorName()
        {
            // Arrange
            var config = new Agent365SdkUserAgentConfiguration(null);

            // Act
            var userAgent = UserAgentHelper.BuildUserAgent(config);

            // Assert
            var pattern = @"^Agent365SDK/.+ \(.+; .NET \d+(\.\d+)*\)$";
            userAgent.Should().MatchRegex(pattern);
            userAgent.Should().NotContain(";;");
        }

        [Fact]
        public void BuildUserAgent_WithCustomConfiguration_ReturnsExpectedFormat()
        {
            // Arrange
            var mockConfig = new Mock<IUserAgentConfiguration>();
            mockConfig.Setup(c => c.ProductName).Returns("CustomProduct");
            mockConfig.Setup(c => c.Version).Returns("2.0.0");
            mockConfig.Setup(c => c.OrchestratorName).Returns((string?)null);

            // Act
            var userAgent = UserAgentHelper.BuildUserAgent(mockConfig.Object);

            // Assert
            userAgent.Should().StartWith("CustomProduct/2.0.0");
            userAgent.Should().Contain("(").And.Contain(")");
        }

        [Fact]
        public void BuildUserAgent_WithCustomConfiguration_IncludesOrchestrator()
        {
            // Arrange
            var mockConfig = new Mock<IUserAgentConfiguration>();
            mockConfig.Setup(c => c.ProductName).Returns("CustomProduct");
            mockConfig.Setup(c => c.Version).Returns("3.1.4");
            mockConfig.Setup(c => c.OrchestratorName).Returns("MyOrchestrator");

            // Act
            var userAgent = UserAgentHelper.BuildUserAgent(mockConfig.Object);

            // Assert
            userAgent.Should().StartWith("CustomProduct/3.1.4");
            userAgent.Should().Contain("MyOrchestrator");
        }

        [Fact]
        public void BuildUserAgent_IncludesOSDescription()
        {
            // Arrange
            var config = new Agent365SdkUserAgentConfiguration();

            // Act
            var userAgent = UserAgentHelper.BuildUserAgent(config);

            // Assert
            // Should contain OS information (Windows, Linux, macOS, etc.)
            userAgent.Should().MatchRegex(@"\([^)]+\)"); // Contains parentheses with content
        }

        [Fact]
        public void BuildUserAgent_IncludesFrameworkDescription()
        {
            // Arrange
            var config = new Agent365SdkUserAgentConfiguration();

            // Act
            var userAgent = UserAgentHelper.BuildUserAgent(config);

            // Assert
            // Should contain .NET framework version
            userAgent.Should().Contain(".NET");
        }
    }
}
