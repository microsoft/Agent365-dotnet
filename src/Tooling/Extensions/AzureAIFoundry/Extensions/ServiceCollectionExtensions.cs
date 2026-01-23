// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Extensions
{
    using Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;
    using Microsoft.Agents.A365.Tooling.Services;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for service collection to register MCP services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds MCP services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMcpServices(this IServiceCollection services)
        {
            services.AddScoped<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();
            services.AddScoped<IMcpToolRegistrationService, McpToolRegistrationService>();

            return services;
        }
    }
}
