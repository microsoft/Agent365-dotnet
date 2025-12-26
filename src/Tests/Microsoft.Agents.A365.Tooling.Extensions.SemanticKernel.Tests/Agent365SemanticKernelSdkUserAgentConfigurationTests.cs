// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Tests
{
    /// <summary>
    /// Unit tests for Agent365SemanticKernelSdkUserAgentConfiguration class.
    /// </summary>
    public class Agent365SemanticKernelSdkUserAgentConfigurationTests
    {
        [Fact]
        public void Instance_ReturnsSameInstance()
        {
            // Arrange & Act
            var instance1 = Agent365SemanticKernelSdkUserAgentConfiguration.Instance;
            var instance2 = Agent365SemanticKernelSdkUserAgentConfiguration.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void Instance_HasCorrectOrchestratorName()
        {
            // Arrange & Act
            var config = Agent365SemanticKernelSdkUserAgentConfiguration.Instance;

            // Assert
            config.OrchestratorName.Should().Be("SemanticKernel");
        }

        [Fact]
        public void Instance_HasCorrectProductName()
        {
            // Arrange & Act
            var config = Agent365SemanticKernelSdkUserAgentConfiguration.Instance;

            // Assert
            config.ProductName.Should().Be("Agent365SDK");
        }

        [Fact]
        public void Instance_HasValidVersion()
        {
            // Arrange & Act
            var config = Agent365SemanticKernelSdkUserAgentConfiguration.Instance;

            // Assert
            config.Version.Should().NotBeNullOrEmpty();
        }
    }
}
