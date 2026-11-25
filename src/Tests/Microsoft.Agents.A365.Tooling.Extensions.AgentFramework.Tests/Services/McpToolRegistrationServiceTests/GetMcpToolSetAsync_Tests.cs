// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using ModelContextProtocol.Client;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests;

/// <summary>
/// Unit tests for McpToolRegistrationService.GetMcpToolSetAsync method.
/// </summary>
public class GetMcpToolSetAsync_Tests : McpToolRegistrationServiceTestBase
{
    [Fact]
    public async Task ReturnsToolSetFromConfigurationServiceWithoutDisposingIt()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        using var cancellation = new CancellationTokenSource();
        var clients = new DisposalProbe();
        var toolSet = new McpToolSet(
            Array.Empty<MCPServerConfig>(),
            new Dictionary<string, IReadOnlyList<McpClientTool>>(),
            new[] { clients });
        McpServerConfigurationServiceMock
            .Setup(x => x.GetMcpToolSetAsync(
                TestAgentUserId,
                TestAuthToken,
                It.IsAny<IMcpTokenProvider>(),
                mockTurnContext.Object,
                It.Is<ToolOptions>(o => o.UserAgentConfiguration == Agent365AgentFrameworkSdkUserAgentConfiguration.Instance),
                cancellation.Token))
            .ReturnsAsync(toolSet);
        var service = CreateService();

        // Act
        var result = await service.GetMcpToolSetAsync(
            agentUserId: TestAgentUserId,
            userAuthorization: null!,
            authHandlerName: "handler",
            turnContext: mockTurnContext.Object,
            authToken: TestAuthToken,
            cancellationToken: cancellation.Token);

        // Assert: the caller owns the tool set and disposes it when the turn ends.
        result.Should().BeSameAs(toolSet);
        clients.DisposeCount.Should().Be(0);
    }

    private sealed class DisposalProbe : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
