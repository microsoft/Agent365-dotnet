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
            url.Should().Be("https://agent365.svc.cloud.microsoft/agents/v2/agent-123/mcpServers");
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
            url.Should().Be("https://test.endpoint.local/agents/v2/agent-456/mcpServers");
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

        #region ResolveTokenScopeForServer Tests

        [Fact]
        public void ResolveTokenScopeForServer_NullAudience_ReturnsAtgScope()
        {
            // Arrange
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert — should be the V1 ATG fallback
            scope.Should().Be("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_EmptyAudience_ReturnsAtgScope()
        {
            // Arrange
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1", audience = ""
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert
            scope.Should().Be("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_AtgAppIdAsAudience_ReturnsAtgScope()
        {
            // Arrange — V1 server that explicitly carries the ATG app ID
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert
            scope.Should().Be("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_AtgAppIdCaseInsensitive_ReturnsAtgScope()
        {
            // Arrange — ATG app ID in uppercase should still route to V1
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = "EA9FFC3E-8A23-4A7D-836D-234D7C7565C1"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert
            scope.Should().Be("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_V2Audience_ReturnsPerAudienceScope()
        {
            // Arrange — V2 server with its own audience GUID
            var config = new ConfigurationBuilder().Build();
            var v2AudienceId = "11111111-2222-3333-4444-555555555555";
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = v2AudienceId
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert
            scope.Should().Be($"{v2AudienceId}/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_ApiPrefixedAtgAudience_ReturnsAtgScope()
        {
            // Regression: if the gateway returns the ATG App ID with an "api://" prefix the server
            // must still be routed down the V1 path, not treated as a V2 server with a wrong scope.
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = $"api://ea9ffc3e-8a23-4a7d-836d-234d7c7565c1"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert — "api://<AtgAppId>" is equivalent to the bare GUID; V1 fallback applies
            scope.Should().Be("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_ApiPrefixedAtgAudience_CaseInsensitive_ReturnsAtgScope()
        {
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = "API://EA9FFC3E-8A23-4A7D-836D-234D7C7565C1"
            };
            Utility.ResolveTokenScopeForServer(server, config)
                .Should().Be("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_ApiPrefixedAudience_ReturnsPerAudienceScope()
        {
            // Arrange — V2 servers may send audience already in "api://<guid>" form.
            // /.default is appended directly, producing "api://<guid>/.default" (no double-prefix).
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = "api://11111111-2222-3333-4444-555555555555"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert
            scope.Should().Be("api://11111111-2222-3333-4444-555555555555/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_V2AudienceAndScope_ReturnsCombinedScope()
        {
            // Arrange — V2 server supplies audience (app ID) + scope (permission name).
            // The full OAuth scope is constructed as "{audience}/{scope}".
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = "11111111-2222-3333-4444-555555555555",
                scope = "Tools.ListInvoke.All"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert
            scope.Should().Be("11111111-2222-3333-4444-555555555555/Tools.ListInvoke.All");
        }

        [Fact]
        public void ResolveTokenScopeForServer_ScopeWithNoAudience_ReturnsAtgScope()
        {
            // Arrange — scope field alone (no audience) is treated as V1; scope is ignored.
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                scope = "McpServers.Calendar.All"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert — falls back to ATG scope; V1 scope field is ignored
            scope.Should().Be("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_V1AudienceWithScope_ReturnsAtgScope()
        {
            // Arrange — V1 server with ATG audience + scope; scope field is ignored for V1.
            var config = new ConfigurationBuilder().Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1",
                scope = "McpServers.Calendar.All"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert
            scope.Should().Be("ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_ConfigOverridesV1Scope()
        {
            // Arrange — environment override applies to V1 path
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MCP_PLATFORM_AUTHENTICATION_SCOPE", "api://custom-atg/.default" }
                })
                .Build();
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1"
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert
            scope.Should().Be("api://custom-atg/.default");
        }

        [Fact]
        public void ResolveTokenScopeForServer_ConfigOverrideDoesNotAffectV2Audience()
        {
            // Arrange — V2 per-audience scope is never overridden by config
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MCP_PLATFORM_AUTHENTICATION_SCOPE", "api://custom-atg/.default" }
                })
                .Build();
            var v2AudienceId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
            var server = new Microsoft.Agents.A365.Tooling.Models.MCPServerConfig
            {
                mcpServerName = "server1", id = "id1", url = "http://s1",
                audience = v2AudienceId
            };

            // Act
            var scope = Utility.ResolveTokenScopeForServer(server, config);

            // Assert — V2 derives scope from audience, not config
            scope.Should().Be($"{v2AudienceId}/.default");
        }

        #endregion
    }
}
