// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.Models;

namespace Microsoft.Agents.A365.Tooling.Services
{
    /// <summary>
    /// Result of an Intune compliance check.
    /// </summary>
    public class IntuneComplianceCheckResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the device is Intune managed.
        /// </summary>
        public bool IsIntuneManaged { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the device is Azure AD joined.
        /// </summary>
        public bool IsAzureAdJoined { get; set; }

        /// <summary>
        /// Gets or sets the tenant ID if available.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the device ID if available.
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the machine name if available.
        /// </summary>
        public string? MachineName { get; set; }

        /// <summary>
        /// Gets or sets any error message from the compliance check.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service for discovering MCP servers from various sources (cloud ATG, local Windows desktop, etc.).
    /// </summary>
    public interface IMcpServerDiscoveryService
    {
        /// <summary>
        /// Discovers all available MCP servers from all configured sources.
        /// This includes cloud MCP servers from ATG and local MCP servers from Windows desktops.
        /// For local servers, an Intune compliance check is performed first.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Authentication token for ATG access.</param>
        /// <param name="clientName">The name of the desktop client for local server discovery. If null, only cloud servers are returned.</param>
        /// <param name="toolOptions">Tool options for the discovery request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A combined list of all discovered MCP servers.</returns>
        Task<List<MCPServerConfig>> DiscoverAllServersAsync(
            string agentInstanceId,
            string authToken,
            string? clientName,
            ToolOptions toolOptions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers cloud MCP servers from the Agent Tooling Gateway (ATG).
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Authentication token for ATG access.</param>
        /// <param name="toolOptions">Tool options for the discovery request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of cloud MCP servers.</returns>
        Task<List<MCPServerConfig>> DiscoverCloudServersAsync(
            string agentInstanceId,
            string authToken,
            ToolOptions toolOptions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a Windows desktop client is Intune managed before allowing local server discovery.
        /// This is a security requirement to ensure only managed devices can expose local MCP servers.
        /// </summary>
        /// <param name="clientName">The name of the desktop client to check.</param>
        /// <param name="proxyBaseUrl">The base URL of the WNS proxy service.</param>
        /// <param name="timeoutSeconds">Timeout in seconds for the check. Default is 30 seconds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Intune compliance check result.</returns>
        Task<IntuneComplianceCheckResult> CheckIntuneComplianceAsync(
            string clientName,
            string proxyBaseUrl,
            int timeoutSeconds = 30,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers local MCP servers from a Windows desktop client via WNS.
        /// The desktop client runs `odr mcp list` and returns the results.
        /// </summary>
        /// <param name="clientName">The name of the desktop client to query.</param>
        /// <param name="proxyBaseUrl">The base URL of the WNS proxy service.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of local MCP servers discovered on the desktop.</returns>
        Task<List<MCPServerConfig>> DiscoverLocalServersAsync(
            string clientName,
            string proxyBaseUrl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts a list of local MCP server info (from odr mcp list) to MCPServerConfig objects.
        /// </summary>
        /// <param name="localServers">The local server info from odr mcp list.</param>
        /// <param name="clientName">The client name for WNS transport configuration.</param>
        /// <param name="proxyBaseUrl">The proxy base URL for WNS transport.</param>
        /// <returns>List of MCPServerConfig objects.</returns>
        List<MCPServerConfig> ConvertLocalServersToConfig(
            List<LocalMcpServerInfo> localServers,
            string clientName,
            string proxyBaseUrl);
    }
}
