using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Utility logic for environment-related operations.
    /// </summary>
    public class EnvironmentUtils
    {
        private const string TestObservabilityScope = "https://api.test.powerplatform.com/.default";
        private const string PreprodObservabilityScope = "https://api.preprod.powerplatform.com/.default";
        private const string ProdObservabilityScope = "https://api.powerplatform.com/.default";

        private const string TestObservabilityClusterCategory = "test";
        private const string PreprodObservabilityClusterCategory = "preprod";
        private const string ProdObservabilityClusterCategory = "prod";

        private const string DevelopmentEnvironmentName = "Development";

        /// <summary>
        /// Returns the scope for authenticating to the observability service based on the current environment.
        /// </summary>
        /// <param name="environment"> Optional environment name. Supported values "development", "test", "preprod", "production".</param>
        /// <returns></returns>
        public static string[] GetObservabilityAuthenticationScope(string? environment = null)
        {
            environment = environment ?? GetCurrentEnvironment();

            return environment.ToLowerInvariant() switch
            {
                // For now, map "development" to preprod scope for local testing
                "development" => new[] { PreprodObservabilityScope },
                "test" => new[] { TestObservabilityScope },
                "preprod" => new[] { PreprodObservabilityScope },
                "production" => new[] { ProdObservabilityScope },
                _ => new[] { ProdObservabilityScope },
            };
        }
        /// <summary>
        /// Returns the cluster category for the observability service based on the current environment.
        /// </summary>
        /// <param name="environment"> Optional environment name. Supported values "development", "test", "preprod", "production".</param>
        /// <returns></returns>
        public static string GetObservabilityClusterCategory(string? environment = null)
        {
            environment = environment ?? GetCurrentEnvironment();

            return environment.ToLowerInvariant() switch
            {
                // For now, map "development" to preprod scope for local testing
                "development" => PreprodObservabilityClusterCategory,
                "test" => TestObservabilityClusterCategory,
                "preprod" => PreprodObservabilityClusterCategory,
                "production" => ProdObservabilityClusterCategory,
                _ => ProdObservabilityClusterCategory,
            };
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


