// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.A365.Observability.Runtime.Common;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common
{
    [TestClass]
    public class EnvironmentUtilsTests
    {
        private static IConfiguration BuildConfig(IDictionary<string, string?>? values = null)
        {
            var builder = new ConfigurationBuilder();
            if (values != null)
            {
                builder.AddInMemoryCollection(values);
            }
            return builder.Build();
        }

        [TestMethod]
        public void GetObservabilityAuthenticationScope_DefaultsToProdScope()
        {
            // Act
            var scope = EnvironmentUtils.GetObservabilityAuthenticationScope();

            // Assert
            scope.Should().ContainSingle().Which.Should().Be("https://api.powerplatform.com/.default");
        }

        [TestMethod]
        public void GetObservabilityAuthenticationScope_UsesAgent365Scope_WhenCustomDomainEnabled()
        {
            // Arrange
            var configEnabled = BuildConfig(new Dictionary<string, string?>
            {
                { "EnableAgent365CustomDomain", "true" }
            });

            // Act
            var scope = EnvironmentUtils.GetObservabilityAuthenticationScope(configuration: configEnabled);

            // Assert
            scope.Should().ContainSingle().Which.Should().Be("api://9b975845-388f-4429-889e-eab1ef63949c/.default");
        }

        [TestMethod]
        public void GetObservabilityAuthenticationScope_UsesProdScope_WhenCustomDomainDisabled()
        {
            // Arrange
            var configDisabled = BuildConfig(new Dictionary<string, string?>
            {
                { "EnableAgent365CustomDomain", "false" }
            });

            // Act
            var scope = EnvironmentUtils.GetObservabilityAuthenticationScope(configuration: configDisabled);

            // Assert
            scope.Should().ContainSingle().Which.Should().Be("https://api.powerplatform.com/.default");
        }

        [TestMethod]
        public void GetObservabilityAuthenticationScope_UsesProdScope_WhenCustomDomainSettingMissing()
        {
            // Arrange
            var configMissing = BuildConfig();

            // Act
            var scope = EnvironmentUtils.GetObservabilityAuthenticationScope(configuration: configMissing);

            // Assert
            scope.Should().ContainSingle().Which.Should().Be("https://api.powerplatform.com/.default");
        }

        [TestMethod]
        public void IsCustomDomainEnabled_ReturnsTrue_WhenConfigValueTrue()
        {
            // Arrange
            var config = BuildConfig(new Dictionary<string, string?>
            {
                { "EnableAgent365CustomDomain", "true" }
            });

            // Act
            var result = EnvironmentUtils.IsCustomDomainEnabled(configuration: config);

            // Assert
            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsCustomDomainEnabled_ReturnsFalse_WhenConfigValueFalse()
        {
            // Arrange
            var config = BuildConfig(new Dictionary<string, string?>
            {
                { "EnableAgent365CustomDomain", "false" }
            });

            // Act
            var result = EnvironmentUtils.IsCustomDomainEnabled(configuration: config);

            // Assert
            result.Should().BeFalse();
        }

        [TestMethod]
        public void IsCustomDomainEnabled_ReturnsFalse_WhenConfigValueMissing()
        {
            // Arrange
            var config = BuildConfig();

            // Act
            var result = EnvironmentUtils.IsCustomDomainEnabled(configuration: config);

            // Assert
            result.Should().BeFalse();
        }

        [TestMethod]
        public void IsCustomDomainEnabled_DefaultsToFalse()
        {
            // Act
            var result = EnvironmentUtils.IsCustomDomainEnabled(null);

            // Assert
            result.Should().BeFalse();
        }
    }
}
