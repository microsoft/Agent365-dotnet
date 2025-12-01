// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Provides discovery for Agent365 endpoints.
    /// </summary>
    internal sealed class Agent365EndpointDiscovery
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
        public string GetBaseHost()
        {
            switch (this.clusterCategory?.ToLowerInvariant())
            {
                case "preprod":
                case "firstrelease":
                    return "preprod.agent365.svc.cloud.dev.microsoft";
                case "production":
                case "prod":
                    return "agent365.svc.cloud.microsoft";
                default:
                    return "agent365.svc.cloud.microsoft";
            }
        }
    }
}
