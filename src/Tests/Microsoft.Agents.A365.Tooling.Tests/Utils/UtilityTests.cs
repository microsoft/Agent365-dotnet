// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Utils;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Tests.Utils
{
    /// <summary>
    /// Unit tests for Utility class.
    /// Tests URL building and endpoint generation methods.
    /// </summary>
    public class UtilityTests
    {
        [Fact]
        public void GetChatHistoryEndpoint_UsesDefaultProdUrl()
        {
            // Arrange
            var configuration = new ConfigurationBuilder().Build();

            // Act
            var endpoint = Utility.GetChatHistoryEndpoint(configuration);

            // Assert
            endpoint.Should().Be("https://agent365.svc.cloud.microsoft/agents/real-time-threat-protection/chat-message");
        }

        [Fact]
        public void GetChatHistoryEndpoint_UsesConfiguredEndpoint()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MCP_PLATFORM_ENDPOINT", "https://custom.endpoint.test" }
                })
                .Build();

            // Act
            var endpoint = Utility.GetChatHistoryEndpoint(configuration);

            // Assert
            endpoint.Should().Be("https://custom.endpoint.test/agents/real-time-threat-protection/chat-message");
        }

        [Fact]
        public void GetToolingGatewayForDigitalWorker_UsesDefaultProdUrl()
        {
            // Arrange
            var configuration = new ConfigurationBuilder().Build();
            var agentInstanceId = "agent-123";

            // Act
            var url = Utility.GetToolingGatewayForDigitalWorker(agentInstanceId, configuration);

            // Assert
            url.Should().Be("https://agent365.svc.cloud.microsoft/agents/agent-123/mcpServers");
        }

        [Fact]
        public void GetToolingGatewayForDigitalWorker_UsesConfiguredEndpoint()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MCP_PLATFORM_ENDPOINT", "https://test.endpoint.local" }
                })
                .Build();
            var agentInstanceId = "agent-456";

            // Act
            var url = Utility.GetToolingGatewayForDigitalWorker(agentInstanceId, configuration);

            // Assert
            url.Should().Be("https://test.endpoint.local/agents/agent-456/mcpServers");
        }

        [Fact]
        public void GetMcpBaseUrl_UsesDefaultProdUrl()
        {
            // Arrange
            var configuration = new ConfigurationBuilder().Build();

            // Act
            var url = Utility.GetMcpBaseUrl(configuration);

            // Assert
            url.Should().Be("https://agent365.svc.cloud.microsoft/agents/servers");
        }

        [Fact]
        public void GetMcpBaseUrl_UsesConfiguredEndpoint()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MCP_PLATFORM_ENDPOINT", "https://dev.platform.test" }
                })
                .Build();

            // Act
            var url = Utility.GetMcpBaseUrl(configuration);

            // Assert
            url.Should().Be("https://dev.platform.test/agents/servers");
        }

        [Fact]
        public void BuildMcpServerUrl_CombinesBaseUrlAndServerName()
        {
            // Arrange
            var configuration = new ConfigurationBuilder().Build();
            var serverName = "my-server";

            // Act
            var url = Utility.BuildMcpServerUrl(serverName, configuration);

            // Assert
            url.Should().Be("https://agent365.svc.cloud.microsoft/agents/servers/my-server");
        }

        [Fact]
        public void BuildMcpServerUrl_UsesConfiguredEndpoint()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MCP_PLATFORM_ENDPOINT", "https://staging.example.com" }
                })
                .Build();
            var serverName = "test-server";

            // Act
            var url = Utility.BuildMcpServerUrl(serverName, configuration);

            // Assert
            url.Should().Be("https://staging.example.com/agents/servers/test-server");
        }

        [Fact]
        public void GetChatHistoryEndpoint_HandlesEmptyConfigValue()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MCP_PLATFORM_ENDPOINT", "" }
                })
                .Build();

            // Act
            var endpoint = Utility.GetChatHistoryEndpoint(configuration);

            // Assert
            endpoint.Should().Be("https://agent365.svc.cloud.microsoft/agents/real-time-threat-protection/chat-message");
        }

        [Fact]
        public void GetChatHistoryEndpoint_HandlesNullConfigValue()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MCP_PLATFORM_ENDPOINT", null }
                })
                .Build();

            // Act
            var endpoint = Utility.GetChatHistoryEndpoint(configuration);

            // Assert
            endpoint.Should().Be("https://agent365.svc.cloud.microsoft/agents/real-time-threat-protection/chat-message");
        }
    }
}
