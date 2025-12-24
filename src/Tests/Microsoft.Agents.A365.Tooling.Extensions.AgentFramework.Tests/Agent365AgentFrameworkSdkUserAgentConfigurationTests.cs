// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests
{
    /// <summary>
    /// Unit tests for Agent365AgentFrameworkSdkUserAgentConfiguration class.
    /// </summary>
    public class Agent365AgentFrameworkSdkUserAgentConfigurationTests
    {
        [Fact]
        public void Instance_ReturnsSameInstance()
        {
            // Arrange & Act
            var instance1 = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance;
            var instance2 = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void Instance_HasCorrectOrchestratorName()
        {
            // Arrange & Act
            var config = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance;

            // Assert
            config.OrchestratorName.Should().Be("AgentFramework");
        }

        [Fact]
        public void Instance_HasCorrectProductName()
        {
            // Arrange & Act
            var config = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance;

            // Assert
            config.ProductName.Should().Be("Agent365SDK");
        }

        [Fact]
        public void Instance_HasValidVersion()
        {
            // Arrange & Act
            var config = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance;

            // Assert
            config.Version.Should().NotBeNullOrEmpty();
        }
    }
}
