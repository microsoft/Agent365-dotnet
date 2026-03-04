// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Tooling.Models
{
    /// <summary>
    /// Represents the root response from `odr mcp list` command.
    /// </summary>
    public class OdrMcpListResponse
    {
        /// <summary>
        /// Gets or sets the list of MCP servers.
        /// </summary>
        [JsonPropertyName("servers")]
        public List<LocalMcpServerWrapper>? Servers { get; set; }
    }

    /// <summary>
    /// Wrapper for an MCP server entry in the `odr mcp list` response.
    /// Each element in the servers array has a nested "server" property
    /// containing the actual server info.
    /// </summary>
    public class LocalMcpServerWrapper
    {
        /// <summary>
        /// Gets or sets the actual server info nested inside the wrapper.
        /// </summary>
        [JsonPropertyName("server")]
        public LocalMcpServerInfo? Server { get; set; }
    }

    /// <summary>
    /// Represents information about a local MCP server discovered via Windows ODR (On-Device Registry).
    /// This matches the JSON structure returned by `odr mcp list`.
    /// </summary>
    public class LocalMcpServerInfo
    {
        /// <summary>
        /// Gets or sets the name of the MCP server.
        /// Example: "MicrosoftWindows.Client.Core_cw5n1h2txyewy/file-mcp-server"
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the MCP server.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the version of the MCP server.
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the packages that provide this MCP server.
        /// </summary>
        [JsonPropertyName("packages")]
        public List<LocalMcpPackageInfo>? Packages { get; set; }

        /// <summary>
        /// Gets or sets additional metadata for this MCP server.
        /// This contains the static_responses with pre-cached tools/list response.
        /// </summary>
        [JsonPropertyName("_meta")]
        public JsonElement? Meta { get; set; }

        /// <summary>
        /// Gets the server identifier from the first package.
        /// This is the ID used to start the server via ODR.
        /// </summary>
        [JsonIgnore]
        public string? ServerId => Packages?.FirstOrDefault()?.Identifier;

        /// <summary>
        /// Attempts to extract the static tools/list response from the _meta property.
        /// The structure is: _meta["io.modelcontextprotocol.registry/publisher-provided"]["com.microsoft.windows"]["manifest"]["_meta"]["com.microsoft.windows"]["static_responses"]["tools/list"]["tools"]
        /// </summary>
        /// <returns>The tools array as a JsonElement, or null if not found.</returns>
        public JsonElement? GetStaticToolsList()
        {
            if (Meta == null || Meta.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            try
            {
                // Navigate: _meta -> io.modelcontextprotocol.registry/publisher-provided -> com.microsoft.windows -> manifest -> _meta -> com.microsoft.windows -> static_responses -> tools/list -> tools
                if (Meta.Value.TryGetProperty("io.modelcontextprotocol.registry/publisher-provided", out var publisherProvided) &&
                    publisherProvided.TryGetProperty("com.microsoft.windows", out var msWindows) &&
                    msWindows.TryGetProperty("manifest", out var manifest) &&
                    manifest.TryGetProperty("_meta", out var manifestMeta) &&
                    manifestMeta.TryGetProperty("com.microsoft.windows", out var msWindowsInner) &&
                    msWindowsInner.TryGetProperty("static_responses", out var staticResponses) &&
                    staticResponses.TryGetProperty("tools/list", out var toolsList) &&
                    toolsList.TryGetProperty("tools", out var tools))
                {
                    return tools;
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return null;
        }

        /// <summary>
        /// Checks if this server has pre-cached static tool definitions.
        /// </summary>
        [JsonIgnore]
        public bool HasStaticTools => GetStaticToolsList() != null;
    }

    /// <summary>
    /// Represents package information for a local MCP server.
    /// </summary>
    public class LocalMcpPackageInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the package.
        /// Example: "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server"
        /// </summary>
        [JsonPropertyName("identifier")]
        public string? Identifier { get; set; }

        /// <summary>
        /// Gets or sets the registry type (e.g., "on_device").
        /// </summary>
        [JsonPropertyName("registryType")]
        public string? RegistryType { get; set; }

        /// <summary>
        /// Gets or sets the runtime hint (e.g., "odr.exe").
        /// </summary>
        [JsonPropertyName("runtimeHint")]
        public string? RuntimeHint { get; set; }

        /// <summary>
        /// Gets or sets the transport configuration.
        /// </summary>
        [JsonPropertyName("transport")]
        public LocalMcpTransportInfo? Transport { get; set; }
    }

    /// <summary>
    /// Represents transport information for a local MCP server.
    /// </summary>
    public class LocalMcpTransportInfo
    {
        /// <summary>
        /// Gets or sets the transport type (e.g., "stdio").
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    /// <summary>
    /// Represents a request to list local MCP servers from a desktop client.
    /// </summary>
    public class ListLocalMcpServersRequest
    {
        /// <summary>
        /// Gets or sets the type of request. For listing servers, this should be "list_servers".
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "list_servers";

        /// <summary>
        /// Gets or sets the request ID for correlation.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }
    }

    /// <summary>
    /// Represents a response containing the list of local MCP servers.
    /// </summary>
    public class ListLocalMcpServersResponse
    {
        /// <summary>
        /// Gets or sets whether the request was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the list of local MCP servers.
        /// </summary>
        [JsonPropertyName("servers")]
        public List<LocalMcpServerInfo>? Servers { get; set; }

        /// <summary>
        /// Gets or sets an error message if the request failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
