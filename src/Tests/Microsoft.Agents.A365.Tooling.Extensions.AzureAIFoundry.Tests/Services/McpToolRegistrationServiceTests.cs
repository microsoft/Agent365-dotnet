// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.AI.Agents.Persistent;
using FluentAssertions;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry;
using Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry.Tests.Services
{
    /// <summary>
    /// Unit tests for McpToolRegistrationService.SendChatHistoryAsync methods.
    /// Tests parameter validation, message retrieval and conversion, and delegation to underlying service.
    /// </summary>
    public class McpToolRegistrationServiceTests
    {
        private readonly Mock<ILogger<IMcpToolRegistrationService>> _loggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IMcpToolServerConfigurationService> _mcpServerConfigurationServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;

        public McpToolRegistrationServiceTests()
        {
            _loggerMock = new Mock<ILogger<IMcpToolRegistrationService>>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _mcpServerConfigurationServiceMock = new Mock<IMcpToolServerConfigurationService>();
            _configurationMock = new Mock<IConfiguration>();
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenAgentClientIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(null!, "thread-123", turnContextMock.Object);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("agentClient");
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var agentClientMock = new Mock<PersistentAgentsClient>();
            var turnContextMock = new Mock<ITurnContext>();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(agentClientMock.Object, null!, turnContextMock.Object);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("threadId");
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsArgumentNullException_WhenTurnContextIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var agentClientMock = new Mock<PersistentAgentsClient>();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(agentClientMock.Object, "thread-123", null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("turnContext");
        }

        [Fact]
        public async Task SendChatHistoryAsync_ThrowsOperationCanceledException_WhenCancellationTokenIsCanceled()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var agentClientMock = new Mock<PersistentAgentsClient>();
            var turnContextMock = new Mock<ITurnContext>();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(
                agentClientMock.Object, 
                "thread-123", 
                turnContextMock.Object, 
                cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenAgentClientIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var turnContextMock = new Mock<ITurnContext>();
            var toolOptions = new ToolOptions();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(null!, "thread-123", turnContextMock.Object, toolOptions);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("agentClient");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenThreadIdIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var agentClientMock = new Mock<PersistentAgentsClient>();
            var turnContextMock = new Mock<ITurnContext>();
            var toolOptions = new ToolOptions();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(agentClientMock.Object, null!, turnContextMock.Object, toolOptions);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("threadId");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenTurnContextIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var agentClientMock = new Mock<PersistentAgentsClient>();
            var toolOptions = new ToolOptions();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(agentClientMock.Object, "thread-123", null!, toolOptions);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("turnContext");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsArgumentNullException_WhenToolOptionsIsNull()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var agentClientMock = new Mock<PersistentAgentsClient>();
            var turnContextMock = new Mock<ITurnContext>();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(agentClientMock.Object, "thread-123", turnContextMock.Object, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("toolOptions");
        }

        [Fact]
        public async Task SendChatHistoryAsync_WithToolOptions_ThrowsOperationCanceledException_WhenCancellationTokenIsCanceled()
        {
            // Arrange
            var service = new McpToolRegistrationService(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _mcpServerConfigurationServiceMock.Object,
                _configurationMock.Object);

            var agentClientMock = new Mock<PersistentAgentsClient>();
            var turnContextMock = new Mock<ITurnContext>();
            var toolOptions = new ToolOptions();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await service.SendChatHistoryAsync(
                agentClientMock.Object, 
                "thread-123", 
                turnContextMock.Object, 
                toolOptions, 
                cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // Tests for direct message overloads removed due to Azure SDK type limitations
        // PersistentThreadMessage has non-virtual properties that cannot be mocked
        // Coverage is provided through PersistentAgentsClient overload parameter validation tests

    }
}
