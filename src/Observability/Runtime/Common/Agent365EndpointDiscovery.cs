// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Provides discovery for Agent365 endpoints.
    /// </summary>
    public sealed class Agent365EndpointDiscovery
    {
        private readonly string clusterCategory;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365EndpointDiscovery"/> class.
        /// </summary>
        /// <param name="clusterCategory">The cluster category.</param>
        public Agent365EndpointDiscovery(string clusterCategory)
        {
            this.clusterCategory = clusterCategory ?? throw new ArgumentNullException(nameof(clusterCategory));
        }

        /// <summary>
        /// Gets the base host for the specified cluster category.
        /// </summary>
        public string GetHost()
        {
            switch (this.clusterCategory?.ToLowerInvariant())
            {
                case "firstrelease":
                case "production":
                case "prod":
                    return "agent365.svc.cloud.microsoft";
                default:
                    throw new ArgumentException($"Invalid ClusterCategory value: {clusterCategory}");
            }
        }
    }
}
