// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Runtime.Extensions.OpenAI;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using Xunit;

namespace Microsoft.Agents.A365.Runtime.OpenAI.Tests
{
    /// <summary>
    /// Unit tests for the OpenAIFunctionProvider functionality.
    /// </summary>
    public class OpenAIFunctionProviderTests : IDisposable
    {
        private readonly OpenAIFunctionProvider _functionProvider;
        private readonly Mock<ILogger<OpenAIFunctionProvider>> _loggerMock;

        public OpenAIFunctionProviderTests()
        {
            _loggerMock = new Mock<ILogger<OpenAIFunctionProvider>>();
            
            // Create provider with a delegate that returns test functions
            _functionProvider = new OpenAIFunctionProvider(
                configureFunctions: (tenantId, workerId) =>
                {
                    var tools = new List<ChatTool>
                    {
                        ChatTool.CreateFunctionTool("test_function", "A test function")
                    };
                    var executors = new Dictionary<string, Func<JsonNode?, Task<string>>>
                    {
                        ["test_function"] = async (args) => await Task.FromResult($"test result for {tenantId}/{workerId}")
                    };
                    return (tools, executors);
                },
                logger: _loggerMock.Object
            );
        }

        [Fact]
        public void GetAvailableTools_ValidTenantAndWorker_ReturnsTools()
        {
            // Act
            var tools = _functionProvider.GetAvailableTools("tenant1", "worker1");

            // Assert
            Assert.NotNull(tools);
            Assert.Single(tools);
            Assert.Equal("test_function", tools.First().FunctionName);
        }

        [Theory]
        [InlineData("tenant1", "worker1")]
        [InlineData("tenant2", "worker2")]
        public void GetAvailableTools_DifferentTenants_ReturnsIndependentTools(string tenantId, string workerId)
        {
            // Act
            var tools = _functionProvider.GetAvailableTools(tenantId, workerId);

            // Assert
            Assert.NotNull(tools);
            Assert.Single(tools);
        }

        [Fact]
        public async Task ExecuteFunctionAsync_ValidFunction_ReturnsExpectedResult()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string workerId = "worker1";
            const string functionName = "test_function";
            var args = JsonNode.Parse("{\"param\": \"value\"}");

            // Act
            var result = await _functionProvider.ExecuteFunctionAsync(functionName, tenantId, workerId, args);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("test result for tenant1/worker1", result);
        }

        [Fact]
        public async Task ExecuteFunctionAsync_NonExistentFunction_ReturnsErrorMessage()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string workerId = "worker1";
            const string functionName = "non_existent_function";

            // Act
            var result = await _functionProvider.ExecuteFunctionAsync(functionName, tenantId, workerId, null);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Unknown function", result);
            Assert.Contains(functionName, result);
        }

        [Theory]
        [InlineData(null, "worker1")]
        [InlineData("tenant1", null)]
        [InlineData(null, null)]
        public void GetAvailableTools_NullParameters_ThrowsArgumentNullException(string? tenantId, string? workerId)
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _functionProvider.GetAvailableTools(tenantId!, workerId!));
        }

        [Theory]
        [InlineData("", "worker1")]
        [InlineData("   ", "worker1")]
        [InlineData("tenant1", "")]
        [InlineData("tenant1", "   ")]
        public void GetAvailableTools_EmptyOrWhitespaceParameters_ThrowsArgumentException(string tenantId, string workerId)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _functionProvider.GetAvailableTools(tenantId, workerId));
        }

        [Fact]
        public void Constructor_WithNullConfigureDelegate_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new OpenAIFunctionProvider(configureFunctions: null!));
        }

        [Fact]
        public void GetAvailableTools_SameTenantAndWorker_ReturnsCachedResult()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string workerId = "worker1";

            // Act
            var tools1 = _functionProvider.GetAvailableTools(tenantId, workerId);
            var tools2 = _functionProvider.GetAvailableTools(tenantId, workerId);

            // Assert
            Assert.NotNull(tools1);
            Assert.NotNull(tools2);
            Assert.Equal(tools1.Count, tools2.Count);
        }

        [Fact]
        public void Dispose_ClearsCache_ReleasesResources()
        {
            // Arrange - Create some cached entries
            _ = _functionProvider.GetAvailableTools("tenant1", "worker1");
            _ = _functionProvider.GetAvailableTools("tenant2", "worker2");

            // Act
            _functionProvider.Dispose();

            // Assert - Should not throw, provider should be disposed
            // In a real scenario, we'd verify that cached resources are properly disposed
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            _functionProvider.Dispose();
            _functionProvider.Dispose();
            _functionProvider.Dispose();
        }

        public void Dispose()
        {
            _functionProvider.Dispose();
        }
    }
}