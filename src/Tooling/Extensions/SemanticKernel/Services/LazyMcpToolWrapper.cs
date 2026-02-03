// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Transports;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    /// <summary>
    /// Creates Semantic Kernel functions from static MCP tool definitions.
    /// When a function is invoked, it lazily creates the MCP client connection and caches it for reuse.
    /// This avoids the initialize → tools/list flow when we already have tool definitions from discovery.
    /// </summary>
    public class LazyMcpToolWrapper : IDisposable
    {
        private readonly MCPServerConfig _serverConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger? _logger;
        private readonly string? _cacheKey;
        private IMcpClient? _mcpClient;
        private readonly SemaphoreSlim _clientLock = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// Cache of MCP clients per session for reuse across tool calls.
        /// Key is a combination of clientName and serverId.
        /// </summary>
        private static readonly ConcurrentDictionary<string, LazyMcpToolWrapper> _sessionCache = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyMcpToolWrapper"/> class.
        /// </summary>
        /// <param name="serverConfig">The MCP server configuration with static tools.</param>
        /// <param name="httpClientFactory">HTTP client factory for creating connections.</param>
        /// <param name="loggerFactory">Logger factory for diagnostics.</param>
        /// <param name="cacheKey">Optional cache key for this wrapper instance.</param>
        public LazyMcpToolWrapper(
            MCPServerConfig serverConfig,
            IHttpClientFactory httpClientFactory,
            ILoggerFactory? loggerFactory = null,
            string? cacheKey = null)
        {
            _serverConfig = serverConfig ?? throw new ArgumentNullException(nameof(serverConfig));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<LazyMcpToolWrapper>();
            _cacheKey = cacheKey;
        }

        /// <summary>
        /// Gets or creates a cached wrapper for the given server configuration.
        /// </summary>
        public static LazyMcpToolWrapper GetOrCreate(
            MCPServerConfig serverConfig,
            IHttpClientFactory httpClientFactory,
            ILoggerFactory? loggerFactory = null)
        {
            var cacheKey = $"{serverConfig.wnsConfig?.clientName}:{serverConfig.wnsConfig?.localServerId}";
            return _sessionCache.GetOrAdd(cacheKey, _ => new LazyMcpToolWrapper(serverConfig, httpClientFactory, loggerFactory, cacheKey));
        }

        /// <summary>
        /// Removes a specific entry from the session cache.
        /// </summary>
        public static void RemoveFromCache(string cacheKey)
        {
            if (_sessionCache.TryRemove(cacheKey, out var wrapper))
            {
                wrapper.Dispose();
            }
        }

        /// <summary>
        /// Clears the session cache. Call this when the conversation/session ends.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var wrapper in _sessionCache.Values)
            {
                wrapper.Dispose();
            }

            _sessionCache.Clear();
        }

        /// <summary>
        /// Creates Semantic Kernel functions from the static tool definitions in the server config.
        /// </summary>
        /// <returns>A list of KernelFunctions that will lazily connect to the MCP server when invoked.</returns>
        public IEnumerable<KernelFunction> CreateKernelFunctions()
        {
            if (!_serverConfig.HasStaticTools || _serverConfig.staticToolsList == null)
            {
                _logger?.LogWarning("[LazyMcp] Server '{ServerName}' has no static tools", _serverConfig.mcpServerName);
                yield break;
            }

            var toolsArray = _serverConfig.staticToolsList.Value;
            if (toolsArray.ValueKind != JsonValueKind.Array)
            {
                _logger?.LogWarning("[LazyMcp] Static tools is not an array for '{ServerName}'", _serverConfig.mcpServerName);
                yield break;
            }

            _logger?.LogInformation("[LazyMcp] Creating {ToolCount} kernel functions from static tools for '{ServerName}'",
                toolsArray.GetArrayLength(), _serverConfig.mcpServerName);

            foreach (var toolElement in toolsArray.EnumerateArray())
            {
                var function = CreateKernelFunctionFromToolDefinition(toolElement);
                if (function != null)
                {
                    _logger?.LogDebug("[LazyMcp] Created function '{FunctionName}' for '{ServerName}'", 
                        function.Name, _serverConfig.mcpServerName);
                    yield return function;
                }
            }
        }

        /// <summary>
        /// Creates a KernelFunction from a JSON tool definition.
        /// </summary>
        private KernelFunction? CreateKernelFunctionFromToolDefinition(JsonElement toolElement)
        {
            // Extract tool name early for better error logging
            var name = toolElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            
            try
            {
                // Extract tool metadata
                var description = toolElement.TryGetProperty("description", out var descElement) ? descElement.GetString() : null;

                if (string.IsNullOrEmpty(name))
                {
                    _logger?.LogWarning("[LazyMcp] Tool without name in static definitions");
                    return null;
                }

                _logger?.LogDebug("[LazyMcp] Creating lazy function for tool '{ToolName}'", name);

                // Parse input schema for parameter metadata
                var parameters = new List<KernelParameterMetadata>();
                if (toolElement.TryGetProperty("inputSchema", out var inputSchema) &&
                    inputSchema.TryGetProperty("properties", out var properties) &&
                    properties.ValueKind == JsonValueKind.Object)
                {
                    // Get required properties
                    var requiredProperties = new HashSet<string>();
                    if (inputSchema.TryGetProperty("required", out var requiredElement) &&
                        requiredElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var req in requiredElement.EnumerateArray())
                        {
                            if (req.ValueKind == JsonValueKind.String)
                            {
                                requiredProperties.Add(req.GetString()!);
                            }
                        }
                    }

                    foreach (var prop in properties.EnumerateObject())
                    {
                        var paramName = prop.Name;
                        var paramDesc = prop.Value.TryGetProperty("description", out var pdesc) ? pdesc.GetString() : null;
                        var isRequired = requiredProperties.Contains(paramName);

                        // Determine parameter type from JSON schema
                        Type paramType = typeof(string);
                        if (prop.Value.TryGetProperty("type", out var typeElement))
                        {
                            // Handle both simple types ("string") and union types (["string", "null"])
                            string? typeString = null;
                            if (typeElement.ValueKind == JsonValueKind.String)
                            {
                                typeString = typeElement.GetString();
                            }
                            else if (typeElement.ValueKind == JsonValueKind.Array)
                            {
                                // For union types like ["boolean", "null"], use the first non-null type
                                foreach (var typeItem in typeElement.EnumerateArray())
                                {
                                    if (typeItem.ValueKind == JsonValueKind.String)
                                    {
                                        var t = typeItem.GetString();
                                        if (t != "null")
                                        {
                                            typeString = t;
                                            break;
                                        }
                                    }
                                }
                            }

                            paramType = typeString switch
                            {
                                "integer" => typeof(int),
                                "number" => typeof(double),
                                "boolean" => typeof(bool),
                                "array" => typeof(JsonElement),
                                "object" => typeof(JsonElement),
                                _ => typeof(string)
                            };
                        }

                        parameters.Add(new KernelParameterMetadata(paramName)
                        {
                            Description = paramDesc,
                            IsRequired = isRequired,
                            ParameterType = paramType
                        });
                    }
                }

                // Create a function that will lazily connect and call the tool
                var toolName = name;
                var wrapper = this;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
                return KernelFunctionFactory.CreateFromMethod(
                    async (Kernel kernel, KernelArguments arguments) =>
                    {
                        return await wrapper.InvokeToolAsync(toolName, arguments);
                    },
                    functionName: name,
                    description: description,
                    parameters: parameters,
                    returnParameter: new KernelReturnParameterMetadata { Description = "Tool result" });
#pragma warning restore SKEXP0001
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[LazyMcp] Failed to create function from tool definition for tool '{ToolName}'", name ?? "unknown");
                return null;
            }
        }

        /// <summary>
        /// Invokes a tool on the MCP server, lazily creating the connection if needed.
        /// Implements automatic retry with connection refresh on failure.
        /// </summary>
        private async Task<string> InvokeToolAsync(string toolName, KernelArguments arguments)
        {
            const int maxRetries = 2;
            Exception? lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var client = await GetOrCreateClientAsync();

                    _logger?.LogInformation("[LazyMcp] Invoking tool '{ToolName}' on '{ServerName}' (attempt {Attempt})",
                        toolName, _serverConfig.mcpServerName, attempt + 1);

                    // Convert KernelArguments to dictionary for MCP
                    var mcpArguments = new Dictionary<string, object?>();
                    foreach (var arg in arguments)
                    {
                        mcpArguments[arg.Key] = arg.Value;
                    }

                    var result = await client.CallToolAsync(toolName, mcpArguments);

                    // Convert result to string - serialize the content
                    if (result.Content != null && result.Content.Count > 0)
                    {
                        // Serialize the content to JSON for a consistent response format
                        return JsonSerializer.Serialize(result.Content);
                    }

                    return result.IsError == true ? $"Error: {JsonSerializer.Serialize(result)}" : "Success";
                }
                catch (Exception ex) when (IsConnectionError(ex) && attempt < maxRetries - 1)
                {
                    _logger?.LogWarning(ex, "[LazyMcp] Tool invocation failed for '{ToolName}' with connection error, clearing client and retrying...", toolName);
                    lastException = ex;

                    // Clear the stale client to force reconnection on next attempt
                    await ClearClientAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[LazyMcp] Tool invocation failed for '{ToolName}'", toolName);
                    throw;
                }
            }

            // Should not reach here, but throw the last exception if we do
            throw lastException ?? new InvalidOperationException("Tool invocation failed after retries");
        }

        /// <summary>
        /// Determines if the exception is a connection-related error that warrants a retry.
        /// </summary>
        private static bool IsConnectionError(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TimeoutException ||
                   ex is TaskCanceledException ||
                   ex is InvalidOperationException ||
                   ex.Message.Contains("session", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("websocket", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Clears the cached MCP client, allowing a fresh connection to be created.
        /// </summary>
        private async Task ClearClientAsync()
        {
            await _clientLock.WaitAsync();
            try
            {
                if (_mcpClient != null)
                {
                    _logger?.LogInformation("[LazyMcp] Clearing stale MCP client for '{ServerName}'", _serverConfig.mcpServerName);

                    // Dispose the old client
                    if (_mcpClient is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (_mcpClient is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }

                    _mcpClient = null;
                }
            }
            finally
            {
                _clientLock.Release();
            }
        }

        /// <summary>
        /// Gets the cached MCP client or creates a new one.
        /// </summary>
        private async Task<IMcpClient> GetOrCreateClientAsync()
        {
            if (_mcpClient != null)
            {
                return _mcpClient;
            }

            await _clientLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_mcpClient != null)
                {
                    return _mcpClient;
                }

                _logger?.LogInformation("[LazyMcp] Creating MCP client for '{ServerName}' (deferred connection)",
                    _serverConfig.mcpServerName);

                // Only WNS transport is supported for lazy loading currently
                if (_serverConfig.transportType != McpTransportType.Wns)
                {
                    throw new NotSupportedException($"Lazy tool loading is only supported for WNS transport, got {_serverConfig.transportType}");
                }

                var wnsConfig = _serverConfig.wnsConfig
                    ?? throw new InvalidOperationException("WNS configuration is required for WNS transport");

                var proxyBaseUrl = wnsConfig.proxyBaseUrl ?? _serverConfig.url
                    ?? throw new InvalidOperationException("WNS proxy base URL is required");

                var transportOptions = new WnsClientTransportOptions
                {
                    ClientName = wnsConfig.clientName,
                    ProxyBaseUrl = proxyBaseUrl,
                    ConnectionTimeoutSeconds = wnsConfig.connectionTimeoutSeconds > 0 ? wnsConfig.connectionTimeoutSeconds : 30,
                    LocalServerId = wnsConfig.localServerId
                };

                var httpClient = _httpClientFactory.CreateClient("WnsMcpClient");
                var logger = _loggerFactory?.CreateLogger<WnsClientTransport>();
                var wnsTransport = new WnsClientTransport(transportOptions, httpClient, logger);

                _mcpClient = await McpClientFactory.CreateAsync(wnsTransport, loggerFactory: _loggerFactory);

                _logger?.LogInformation("[LazyMcp] MCP client created successfully for '{ServerName}'",
                    _serverConfig.mcpServerName);

                return _mcpClient;
            }
            finally
            {
                _clientLock.Release();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // IMcpClient may implement IAsyncDisposable, try async first
            if (_mcpClient is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            else if (_mcpClient is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _clientLock.Dispose();
        }
    }
}
