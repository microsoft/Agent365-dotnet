// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Tooling.Transports
{
    /// <summary>
    /// MCP client transport that uses WNS (Windows Push Notification Service) for communication
    /// with MCP servers running on local Windows desktops.
    /// </summary>
    public class WnsClientTransport : IClientTransport
    {
        private readonly WnsClientTransportOptions _options;
        private readonly HttpClient _httpClient;
        private readonly ILogger? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WnsClientTransport"/> class.
        /// </summary>
        /// <param name="options">The WNS transport options.</param>
        /// <param name="httpClient">The HTTP client for communicating with the WNS proxy.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public WnsClientTransport(WnsClientTransportOptions options, HttpClient httpClient, ILogger? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger;
        }

        /// <inheritdoc/>
        public string Name => $"WNS:{_options.ClientName}";

        /// <inheritdoc/>
        public async Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("[WNS Transport] Connecting to client: {ClientName}", _options.ClientName);

            // Step 1: Send WNS notification to trigger desktop connection
            // Include localServerId in the payload if specified
            HttpContent? notifyContent = null;
            if (!string.IsNullOrEmpty(_options.LocalServerId))
            {
                var payload = JsonSerializer.Serialize(new { serverId = _options.LocalServerId });
                notifyContent = new StringContent(payload, Encoding.UTF8, "application/json");
            }

            var notifyResponse = await _httpClient.PostAsync(
                $"{_options.ProxyBaseUrl}/api/notify/{_options.ClientName}",
                notifyContent,
                cancellationToken);

            notifyResponse.EnsureSuccessStatusCode();

            var notifyResult = await notifyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var sessionId = notifyResult.GetProperty("sessionId").GetString()
                ?? throw new InvalidOperationException("Failed to get sessionId from WNS notify response");

            _logger?.LogInformation("[WNS Transport] Session created: {SessionId}, waiting for connection...", sessionId);

            // Step 2: Wait for desktop client to connect
            var timeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds);
            var connected = await WaitForConnectionAsync(sessionId, timeout, cancellationToken);

            if (!connected)
            {
                throw new TimeoutException($"Desktop client '{_options.ClientName}' did not connect within {timeout.TotalSeconds}s");
            }

            // Step 3: Wait for WebSocket to be ready
            var wsReady = await WaitForWebSocketReadyAsync(sessionId, TimeSpan.FromSeconds(10), cancellationToken);
            if (!wsReady)
            {
                throw new TimeoutException($"WebSocket for '{_options.ClientName}' did not become ready");
            }

            // Step 4: Grace period to ensure WebSocket is fully ready (matches LocalMcpProxyService behavior)
            _logger?.LogDebug("[WNS Transport] Waiting 1s grace period for WebSocket to be fully ready...");
            await Task.Delay(1000, cancellationToken);

            _logger?.LogInformation("[WNS Transport] Connected to session: {SessionId}", sessionId);

            // Return the transport for duplex messaging
            return new WnsTransport(_options.ProxyBaseUrl, sessionId, _httpClient, _logger);
        }

        private async Task<bool> WaitForConnectionAsync(string sessionId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    var response = await _httpClient.GetAsync(
                        $"{_options.ProxyBaseUrl}/api/status/{sessionId}",
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var status = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                        if (status.GetProperty("connected").GetBoolean())
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "[WNS Transport] Connection check failed");
                }

                await Task.Delay(1000, cancellationToken);
            }

            return false;
        }

        private async Task<bool> WaitForWebSocketReadyAsync(string sessionId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    var response = await _httpClient.GetAsync(
                        $"{_options.ProxyBaseUrl}/api/status/{sessionId}",
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var status = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                        if (status.TryGetProperty("wsReady", out var wsReadyProp) && wsReadyProp.GetBoolean())
                        {
                            return true;
                        }
                        // If wsReady property doesn't exist, assume it's ready if connected
                        if (status.GetProperty("connected").GetBoolean())
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "[WNS Transport] WebSocket ready check failed");
                }

                await Task.Delay(500, cancellationToken);
            }

            return false;
        }
    }

    /// <summary>
    /// Transport implementation for WNS-based MCP communication.
    /// </summary>
    internal sealed class WnsTransport : ITransport
    {
        private readonly string _proxyBaseUrl;
        private readonly string _sessionId;
        private readonly HttpClient _httpClient;
        private readonly ILogger? _logger;
        private readonly Channel<JsonRpcMessage> _messageChannel;
        private bool _disposed;

        public WnsTransport(string proxyBaseUrl, string sessionId, HttpClient httpClient, ILogger? logger)
        {
            _proxyBaseUrl = proxyBaseUrl;
            _sessionId = sessionId;
            _httpClient = httpClient;
            _logger = logger;
            _messageChannel = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });
        }

        /// <inheritdoc/>
        public string? SessionId => _sessionId;

        /// <inheritdoc/>
        public ChannelReader<JsonRpcMessage> MessageReader => _messageChannel.Reader;

        /// <inheritdoc/>
        public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var messageJson = JsonSerializer.Serialize(message);

            _logger?.LogInformation("[WNS Transport] Sending message to session {SessionId}: {MessagePreview}",
                _sessionId, messageJson.Length > 200 ? messageJson.Substring(0, 200) + "..." : messageJson);

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_proxyBaseUrl}/api/mcp/{_sessionId}",
                    new StringContent(messageJson, Encoding.UTF8, "application/json"),
                    cancellationToken);

                _logger?.LogInformation("[WNS Transport] Response status: {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode();

                // Read the response and write to the channel for the MCP client to consume
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger?.LogInformation("[WNS Transport] Response content length: {Length}, preview: {Preview}",
                    responseContent?.Length ?? 0,
                    responseContent?.Length > 200 ? responseContent.Substring(0, 200) + "..." : responseContent);

                if (!string.IsNullOrEmpty(responseContent))
                {
                    try
                    {
                        var responseMessage = JsonSerializer.Deserialize<JsonRpcMessage>(responseContent);
                        if (responseMessage != null)
                        {
                            _logger?.LogInformation("[WNS Transport] Writing response message to channel");
                            await _messageChannel.Writer.WriteAsync(responseMessage, cancellationToken);
                            _logger?.LogInformation("[WNS Transport] Response message written to channel successfully");
                        }
                        else
                        {
                            _logger?.LogWarning("[WNS Transport] Deserialized response was null");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger?.LogWarning(ex, "[WNS Transport] Failed to parse response as JsonRpcMessage: {Content}", responseContent);
                    }
                }
                else
                {
                    _logger?.LogWarning("[WNS Transport] Response content was empty");
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger?.LogError(httpEx, "[WNS Transport] HTTP error sending message to session {SessionId}", _sessionId);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[WNS Transport] Error sending message to session {SessionId}", _sessionId);
                throw;
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _messageChannel.Writer.TryComplete();
            }
            return ValueTask.CompletedTask;
        }
    }
}
