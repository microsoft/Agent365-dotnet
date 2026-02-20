// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Extensions
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Agents.A365.Tooling.Services;
    using Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services;

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
            // Register HttpClient with default configuration
            services.AddHttpClient();
            
            services.AddScoped<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();
            services.AddScoped<IMcpToolRegistrationService, McpToolRegistrationService>();

            // Register local MCP scope validator for WNS-based local MCP server access control
            // This validates that admin has granted consent for local MCP server scopes
            // before allowing invocation (similar to how remote MCP servers validate via token)
            services.AddScoped<ILocalMcpScopeValidator, LocalMcpScopeValidator>();

            // Register policy enforcement service for Scenario 2 (device path routing)
            // This enforces that certain MCP servers must be routed through a registered desktop
            services.AddSingleton<IMcpPolicyEnforcementService, McpPolicyEnforcementService>();
            
            // Register the policy enforcement filter
            services.AddScoped<PolicyEnforcingFunctionInvocationFilter>();
            
            return services;
        }
    }
}
