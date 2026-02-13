// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services;

/// <summary>
/// Validates that the agent blueprint has admin consent for local MCP server scopes.
/// This is the equivalent of token-based scope validation for remote MCP servers,
/// but for local MCP servers that are invoked via WNS (no token is sent to the local server).
/// </summary>
public interface ILocalMcpScopeValidator
{
    /// <summary>
    /// Validates that the agent has admin consent for the specified local MCP server scope.
    /// </summary>
    /// <param name="localServerName">The name of the local MCP server being invoked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating whether the scope is valid and any error message.</returns>
    Task<LocalMcpScopeValidationResult> ValidateScopeAsync(
        string localServerName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the required scope for a local MCP server from the ToolingManifest.
    /// </summary>
    /// <param name="localServerName">The name of the local MCP server.</param>
    /// <returns>The required scope, or null if not found in manifest.</returns>
    string? GetRequiredScope(string localServerName);

    /// <summary>
    /// Loads local MCP server configurations from the ToolingManifest.json.
    /// </summary>
    /// <returns>List of local MCP server configurations.</returns>
    Task<List<LocalMcpServerManifestConfig>> LoadLocalMcpServersFromManifestAsync();
}

/// <summary>
/// Result of local MCP scope validation.
/// </summary>
public class LocalMcpScopeValidationResult
{
    /// <summary>
    /// Whether the scope validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The scope that was validated (or required but missing).
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// The resource app ID that exposes the scope.
    /// </summary>
    public string? ResourceAppId { get; set; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static LocalMcpScopeValidationResult Success(string scope, string resourceAppId)
        => new() { IsValid = true, Scope = scope, ResourceAppId = resourceAppId };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static LocalMcpScopeValidationResult Failed(string errorMessage, string? scope = null, string? resourceAppId = null)
        => new() { IsValid = false, ErrorMessage = errorMessage, Scope = scope, ResourceAppId = resourceAppId };

    /// <summary>
    /// Creates a result indicating the server is not in manifest (no scope required).
    /// </summary>
    public static LocalMcpScopeValidationResult NotInManifest(string serverName)
        => new() { IsValid = true, ErrorMessage = $"Server '{serverName}' not found in localMcpServers manifest - no scope validation required" };
}

/// <summary>
/// Configuration for a local MCP server from ToolingManifest.json.
/// </summary>
public class LocalMcpServerManifestConfig
{
    /// <summary>
    /// Name of the local MCP server.
    /// </summary>
    public string McpServerName { get; set; } = string.Empty;

    /// <summary>
    /// Required scope for this server (e.g., "Local.FileMcpServer").
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// The app ID that exposes this scope (the resource app).
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Transport type (should be "wns" for local servers).
    /// </summary>
    public string TransportType { get; set; } = "wns";

    /// <summary>
    /// Description of the local MCP server.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional pattern to match ODR-discovered server IDs.
    /// For example, "file" would match "file-mcp-server" or 
    /// "MicrosoftWindows.Client.Core_cw5n1h2txyewy/file-mcp-server".
    /// </summary>
    public string? ServerIdPattern { get; set; }
}
