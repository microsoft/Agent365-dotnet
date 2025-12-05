// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Utility logic for environment-related operations.
    /// </summary>
    public class EnvironmentUtils
    {
        private const string ProdObservabilityScope = "https://api.powerplatform.com/.default";
        private const string ProdObservabilityClusterCategory = "prod";
        private const string DevelopmentEnvironmentName = "development";
        private const string Agent365EndpointProdObservabilityScope = "api://9b975845-388f-4429-889e-eab1ef63949c/.default";
        private static bool _initialized;
        private static bool _customDomainEnabled;

        /// <summary>
        /// Returns the scope for authenticating to the observability service based on the current environment.
        /// </summary>
        /// <returns>The authentication scope.</returns>
        public static string[] GetObservabilityAuthenticationScope()
        {
            return IsCustomDomainEnabled() ? new[] { Agent365EndpointProdObservabilityScope } : new[] { ProdObservabilityScope };
        }

        /// <summary>
        /// Returns the cluster category for the observability service based on the current environment.
        /// </summary>
        /// <returns></returns>
        public static string GetObservabilityClusterCategory()
        {
            return ProdObservabilityClusterCategory;
        }

        /// <summary>
        /// [Deprecated] Returns the cluster category for the observability service based on the cluster category.
        /// </summary>
        /// <param name="clusterCategory">Cluster category (deprecated, defaults to production).</param>
        /// <returns></returns>
        public static string GetObservabilityClusterCategory(string clusterCategory = ProdObservabilityClusterCategory)
        {
            // clusterCategory is ignored; always returns production category
            return ProdObservabilityClusterCategory;
        }

        /// <summary>
        /// Returns true if the current environment is a development environment.
        /// </summary>
        /// <returns></returns>
        public static bool IsDevelopmentEnvironment()
        {
            var environment = GetCurrentEnvironment();
            return string.Equals(environment, DevelopmentEnvironmentName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initializes the cached configuration values for environment utilities. Should be called once at application startup.
        /// </summary>
        /// <param name="configuration">The configuration instance.</param>
        /// <param name="force">When true, re-initializes even if already initialized.</param>
        public static void Initialize(IConfiguration? configuration, bool force = false)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (_initialized && !force)
            {
                return;
            }

            string enabled = configuration["EnableAgent365CustomDomain"] ?? string.Empty;
            _customDomainEnabled = enabled.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase);
            _initialized = true;
        }

        /// <summary>
        /// Returns true if the custom domain feature is enabled.
        /// </summary>
        public static bool IsCustomDomainEnabled()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("EnvironmentUtils is not initialized. Call Initialize() before using this method.");
            }
            return _customDomainEnabled;
        }

        /// <summary>
        /// Gets the current environment name.
        /// </summary>
        /// <returns>The current environment name.</returns>
        private static string GetCurrentEnvironment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                   Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                   DevelopmentEnvironmentName;
        }
    }
}


