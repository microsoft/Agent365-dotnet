// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Runtime;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for Agent365SdkUserAgentConfiguration class.
    /// Tests the default configuration implementation for User-Agent headers.
    /// </summary>
    public class Agent365SdkUserAgentConfigurationTests
    {
        [Fact]
        public void Agent365SdkUserAgentConfiguration_HasCorrectProductName()
        {
            // Arrange & Act
            var config = new Agent365SdkUserAgentConfiguration();

            // Assert
            config.ProductName.Should().Be("Agent365SDK");
        }

        [Fact]
        public void Agent365SdkUserAgentConfiguration_HasValidVersion()
        {
            // Arrange & Act
            var config = new Agent365SdkUserAgentConfiguration();

            // Assert
            config.Version.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Agent365SdkUserAgentConfiguration_StoresOrchestratorName()
        {
            // Arrange & Act
            var config = new Agent365SdkUserAgentConfiguration("MyOrchestrator");

            // Assert
            config.OrchestratorName.Should().Be("MyOrchestrator");
        }

        [Fact]
        public void Agent365SdkUserAgentConfiguration_OrchestratorNameIsNullByDefault()
        {
            // Arrange & Act
            var config = new Agent365SdkUserAgentConfiguration();

            // Assert
            config.OrchestratorName.Should().BeNull();
        }

        [Fact]
        public void Agent365SdkUserAgentConfiguration_AcceptsEmptyOrchestratorName()
        {
            // Arrange & Act
            var config = new Agent365SdkUserAgentConfiguration("");

            // Assert
            config.OrchestratorName.Should().BeEmpty();
        }

        [Fact]
        public void Agent365SdkUserAgentConfiguration_AcceptsNullOrchestratorName()
        {
            // Arrange & Act
            var config = new Agent365SdkUserAgentConfiguration(null);

            // Assert
            config.OrchestratorName.Should().BeNull();
        }

        [Fact]
        public void Agent365SdkUserAgentConfiguration_ImplementsInterface()
        {
            // Arrange & Act
            var config = new Agent365SdkUserAgentConfiguration();

            // Assert
            config.Should().BeAssignableTo<IUserAgentConfiguration>();
        }
    }
}
