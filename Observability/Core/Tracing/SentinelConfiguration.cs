namespace Microsoft.Agents.A365.Observability.Tracing
{

    /// <summary>
    /// Configuration settings for Microsoft Sentinel integration.
    /// </summary>
    public sealed class SentinelConfiguration
    {
        /// <summary>
        /// Gets or sets the Azure tenant ID for authentication.
        /// </summary>
        public required string TenantId { get; set; }

        /// <summary>
        /// Gets or sets the client ID for authentication.
        /// </summary>
        public required string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client secret for authentication.
        /// </summary>
        public required string ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the Sentinel endpoint URL.
        /// </summary>
        public required string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the Sentinel rule ID.
        /// </summary>
        public required string RuleId { get; set; }

        /// <summary>
        /// Gets or sets the Sentinel stream name.
        /// </summary>
        public required string StreamName { get; set; }

        /// <summary>
        /// Validates that all required properties are set.
        /// </summary>
        /// <returns>True if all properties are configured; otherwise, false.</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(TenantId) &&
                   !string.IsNullOrEmpty(ClientId) &&
                   !string.IsNullOrEmpty(ClientSecret) &&
                   !string.IsNullOrEmpty(Endpoint) &&
                   !string.IsNullOrEmpty(RuleId) &&
                   !string.IsNullOrEmpty(StreamName);
        }

        /// <summary>
        /// Loads settings from environment variables to create a valid SentinelConfiguration instance.
        /// </summary>
        /// <returns>A fully populated and validated SentinelConfiguration object.</returns>
        /// <exception cref="InvalidOperationException">Thrown if any required environment variable is not set.</exception>
        public static SentinelConfiguration? Load()
        {
            // Helper function to read an environment variable or throw a detailed error.
            static string GetRequiredVariable(string name)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException($"Required configuration environment variable '{name}' is not set or is empty.");
                }
                return value;
            }

            try
            {
                // Create and populate the configuration object
                var config = new SentinelConfiguration
                {
                    // Load credentials and settings from environment
                    TenantId = GetRequiredVariable("AZURE_TENANT_ID"),
                    ClientId = GetRequiredVariable("AZURE_CLIENT_ID"),
                    ClientSecret = GetRequiredVariable("AZURE_CLIENT_SECRET"),
                    Endpoint = GetRequiredVariable("AZURE_MONITOR_ENDPOINT"),
                    RuleId = GetRequiredVariable("AZURE_MONITOR_DCR_RULE_ID"),
                    StreamName = GetRequiredVariable("AZURE_MONITOR_STREAM_NAME")
                };

                return config;
            }
            catch (Exception ex)
            {
                // Log the error and return null for graceful degradation
                Console.WriteLine($"❌ Error loading Sentinel configuration: {ex.Message}");
                return null;
            }
        }
    }
}
