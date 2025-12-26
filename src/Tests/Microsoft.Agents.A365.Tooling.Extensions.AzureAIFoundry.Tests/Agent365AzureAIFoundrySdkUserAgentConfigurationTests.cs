// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry.Tests
{
    /// <summary>
    /// Unit tests for Agent365AzureAIFoundrySdkUserAgentConfiguration class.
    /// </summary>
    public class Agent365AzureAIFoundrySdkUserAgentConfigurationTests
    {
        [Fact]
        public void Instance_ReturnsSameInstance()
        {
            // Arrange & Act
            var instance1 = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance;
            var instance2 = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void Instance_HasCorrectOrchestratorName()
        {
            // Arrange & Act
            var config = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance;

            // Assert
            config.OrchestratorName.Should().Be("AzureAIFoundry");
        }

        [Fact]
        public void Instance_HasCorrectProductName()
        {
            // Arrange & Act
            var config = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance;

            // Assert
            config.ProductName.Should().Be("Agent365SDK");
        }

        [Fact]
        public void Instance_HasValidVersion()
        {
            // Arrange & Act
            var config = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance;

            // Assert
            config.Version.Should().NotBeNullOrEmpty();
        }
    }
}
