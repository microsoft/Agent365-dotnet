// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Provides discovery and endpoint generation for Power Platform APIs across different cluster categories.
    /// </summary>
    public class PowerPlatformApiDiscovery
    {
        private readonly string clusterCategory;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerPlatformApiDiscovery"/> class.
        /// </summary>
        /// <param name="clusterCategory">The cluster category.</param>
        public PowerPlatformApiDiscovery(string clusterCategory)
        {
            this.clusterCategory = clusterCategory ?? throw new ArgumentNullException(nameof(clusterCategory));
        }

        /// <summary>
        /// Gets the token audience URL for authentication.
        /// </summary>
        /// <returns>The token audience URL.</returns>
        public string GetTokenAudience()
        {
            return $"https://{GetEnvironmentApiHostNameSuffix()}";
        }

        /// <summary>
        /// Gets the token endpoint host name.
        /// </summary>
        /// <returns>The token endpoint host name.</returns>
        public string GetTokenEndpointHost()
        {
            return GetEnvironmentApiHostNameSuffix();
        }

        /// <summary>
        /// Gets the tenant-specific API endpoint.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>The tenant API endpoint URL.</returns>
        public string GetTenantEndpoint(string tenantId)
        {
            return GeneratePowerPlatformApiDomain(tenantId);
        }

        /// <summary>
        /// Gets the tenant island cluster API endpoint.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>The tenant island cluster API endpoint URL.</returns>
        public string GetTenantIslandClusterEndpoint(string tenantId)
        {
            return GeneratePowerPlatformApiDomain(tenantId, "il-");
        }

        private string GeneratePowerPlatformApiDomain(string hostNameIdentifier, string hostNamePrefix = "")
        {
            if (!Regex.IsMatch(hostNameIdentifier, "^[a-zA-Z0-9-]+$"))
            {
                throw new ArgumentException($"Cannot generate Power Platform API endpoint because the tenant identifier contains invalid host name characters, only alphanumeric and dash characters are expected: {hostNameIdentifier}");
            }

            const string hostNameInfix = "tenant";
            int hexNameSuffixLength = GetHexApiSuffixLength();
            string hexName = hostNameIdentifier.ToLowerInvariant().Replace("-", "");

            if (hexNameSuffixLength >= hexName.Length)
            {
                throw new ArgumentException($"Cannot generate Power Platform API endpoint because the normalized tenant identifier must be at least {hexNameSuffixLength + 1} characters in length: {hexName}");
            }

            string hexNameSuffix = hexName.Substring(hexName.Length - hexNameSuffixLength);
            string hexNamePrefix = hexName.Substring(0, hexName.Length - hexNameSuffixLength);
            string hostNameSuffix = GetEnvironmentApiHostNameSuffix();

            return $"{hostNamePrefix}{hexNamePrefix}.{hexNameSuffix}.{hostNameInfix}.{hostNameSuffix}";
        }

        private int GetHexApiSuffixLength()
        {
            switch (clusterCategory)
            {
                case "firstrelease":
                case "production":
                case "prod":
                    return 2;
                default:
                    return 1;
            }
        }

        private string GetEnvironmentApiHostNameSuffix()
        {
            switch (clusterCategory)
            {
                case "firstrelease":
                case "production":
                case "prod":
                    return "api.powerplatform.com";
                case "gov":
                    return "api.gov.powerplatform.microsoft.us";
                case "high":
                    return "api.high.powerplatform.microsoft.us";
                case "dod":
                    return "api.appsplatform.us";
                case "mooncake":
                    return "api.powerplatform.partner.microsoftonline.cn";
                case "ex":
                    return "api.powerplatform.eaglex.ic.gov";
                case "rx":
                    return "api.powerplatform.microsoft.scloud";
                default:
                    throw new ArgumentException($"Invalid ClusterCategory value: {clusterCategory}");
            }
        }
    }
}
