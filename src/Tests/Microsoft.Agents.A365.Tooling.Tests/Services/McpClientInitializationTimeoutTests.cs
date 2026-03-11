// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Tests.Services
{
    /// <summary>
    /// Unit tests for McpClientInitializationTimeoutSeconds in ToolOptions
    /// and its application in McpToolServerConfigurationService.
    /// </summary>
    public class McpClientInitializationTimeoutTests
    {
        private readonly Mock<ILogger<IMcpToolServerConfigurationService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

        public McpClientInitializationTimeoutTests()
        {
            _loggerMock = new Mock<ILogger<IMcpToolServerConfigurationService>>();
            _configurationMock = new Mock<IConfiguration>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();

            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());
        }

        [Fact]
        public void ToolOptions_McpClientInitializationTimeoutSeconds_DefaultsToNull()
        {
            // Arrange & Act
            var options = new ToolOptions();

            // Assert
            options.McpClientInitializationTimeoutSeconds.Should().BeNull();
        }

        [Fact]
        public void ToolOptions_McpClientInitializationTimeoutSeconds_CanBeSetToValidValue()
        {
            // Arrange & Act
            var options = new ToolOptions
            {
                McpClientInitializationTimeoutSeconds = 180
            };

            // Assert
            options.McpClientInitializationTimeoutSeconds.Should().Be(180);
        }

        [Fact]
        public void ToolOptions_McpClientInitializationTimeoutSeconds_CanBeSetToNull()
        {
            // Arrange
            var options = new ToolOptions
            {
                McpClientInitializationTimeoutSeconds = 120
            };

            // Act
            options.McpClientInitializationTimeoutSeconds = null;

            // Assert
            options.McpClientInitializationTimeoutSeconds.Should().BeNull();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(601)]
        [InlineData(int.MaxValue)]
        public async Task GetMcpClientToolsAsync_InvalidTimeoutValue_ThrowsArgumentOutOfRangeException(int invalidTimeout)
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Development");

            var service = new McpToolServerConfigurationService(
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                _httpClientFactoryMock.Object);

            var serverConfig = new MCPServerConfig
            {
                mcpServerName = "test_server",
                url = "https://localhost:52856/agents/servers/test_server",
                id = "test-id",
                scope = "test-scope",
                audience = "test-audience",
                publisher = "test-publisher",
            };

            var toolOptions = new ToolOptions
            {
                McpClientInitializationTimeoutSeconds = invalidTimeout
            };

            var turnContextMock = new Mock<ITurnContext>();

            // Act
            Func<Task> act = async () => await service.GetMcpClientToolsAsync(
                turnContextMock.Object, serverConfig, "test-token", toolOptions);

            // Assert - ArgumentOutOfRangeException is wrapped in InvalidOperationException by the catch block
            var ex = await act.Should().ThrowAsync<InvalidOperationException>();
            ex.WithInnerException<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(60)]
        [InlineData(120)]
        [InlineData(300)]
        [InlineData(600)]
        public void ToolOptions_McpClientInitializationTimeoutSeconds_AcceptsValidValues(int validTimeout)
        {
            // Arrange & Act
            var options = new ToolOptions
            {
                McpClientInitializationTimeoutSeconds = validTimeout
            };

            // Assert
            options.McpClientInitializationTimeoutSeconds.Should().Be(validTimeout);
        }
    }
}
