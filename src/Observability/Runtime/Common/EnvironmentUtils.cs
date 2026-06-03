// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Utility logic for environment-related operations.
    /// </summary>
    public class EnvironmentUtils
    {
        private const string ProdObservabilityScope = "api://9b975845-388f-4429-889e-eab1ef63949c/Agent365.Observability.OtelWrite";
        private const string ProdObservabilityClusterCategory = "prod";
        private const string DevelopmentEnvironmentName = "development";

        /// <summary>
        /// Returns the scope for authenticating to the observability service based on the current environment.
        /// </summary>
        /// <returns>The authentication scope.</returns>
        public static string[] GetObservabilityAuthenticationScope()
        {
            var overrideScope = Environment.GetEnvironmentVariable("A365_OBSERVABILITY_SCOPE_OVERRIDE");
            return new[] { !string.IsNullOrEmpty(overrideScope) ? overrideScope : ProdObservabilityScope };
        }

        /// <summary>
        /// [Deprecated] Returns the scope for authenticating to the observability service based on the cluster category.
        /// </summary>
        /// <param name="clusterCategory">Cluster category (deprecated, defaults to production).</param>
        /// <returns>The authentication scope.</returns>
        [Obsolete("Cluster category argument is deprecated and will be removed in future versions. Defaults to production.")]
        public static string[] GetObservabilityAuthenticationScope(string clusterCategory = ProdObservabilityClusterCategory)
        {
            // clusterCategory is ignored; always returns production scope
            return new[] { ProdObservabilityScope };
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


