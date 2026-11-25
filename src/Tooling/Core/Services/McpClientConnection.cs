// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ModelContextProtocol.Client;

    /// <summary>
    /// An MCP client together with the <see cref="System.Net.Http.HttpClient"/> and handler chain created for it.
    /// Disposing it disposes the client, ends its Streamable HTTP session and disposes the <see cref="System.Net.Http.HttpClient"/>,
    /// which closes its connections.
    /// </summary>
    internal sealed class McpClientConnection : IAsyncDisposable
    {
        private const string SessionIdHeader = "Mcp-Session-Id";
        private static readonly TimeSpan s_endSessionTimeout = TimeSpan.FromSeconds(5);

        private readonly Uri _endpoint;
        private readonly SessionIdHandler _sessionIdHandler;
        private readonly ILogger _logger;
        private IMcpClient? _client;
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpClientConnection"/> class.
        /// </summary>
        /// <param name="endpoint">The MCP server endpoint.</param>
        /// <param name="handler">The handler chain for the MCP server requests. It is disposed with the connection.</param>
        /// <param name="logger">The logger.</param>
        public McpClientConnection(Uri endpoint, HttpMessageHandler handler, ILogger logger)
        {
            _endpoint = endpoint;
            _logger = logger;
            _sessionIdHandler = new SessionIdHandler { InnerHandler = handler };
            HttpClient = new HttpClient(_sessionIdHandler);
        }

        /// <summary>
        /// Gets the HTTP client that sends the MCP server requests.
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// Gets the connected MCP client.
        /// </summary>
        public IMcpClient Client => _client ?? throw new InvalidOperationException("The MCP client isn't connected.");

        /// <summary>
        /// Connects the MCP client to the server.
        /// </summary>
        public async Task ConnectAsync(McpClientOptions? clientOptions, ILoggerFactory? loggerFactory, CancellationToken cancellationToken)
        {
            var transportOptions = new SseClientTransportOptions
            {
                Endpoint = _endpoint,
                TransportMode = HttpTransportMode.AutoDetect,
            };

            var transport = new SseClientTransport(transportOptions, HttpClient);
            _client = await McpClientFactory.CreateAsync(transport, clientOptions, loggerFactory, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes the MCP client, ends its session and disposes the HTTP client. Doesn't throw.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                if (_client is not null)
                {
                    await _client.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose the MCP client for {Endpoint}", _endpoint);
            }

            try
            {
                if (_sessionIdHandler.SessionId is { } sessionId)
                {
                    await EndSessionAsync(sessionId).ConfigureAwait(false);
                }
            }
            finally
            {
                HttpClient.Dispose();
            }
        }

        private async Task EndSessionAsync(string sessionId)
        {
            // ModelContextProtocol.Core 0.2.0-preview.3 doesn't send the DELETE that ends a Streamable HTTP session
            // when a client is disposed, so send it here. Remove this once the SDK sends it.
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, _endpoint);
                request.Headers.TryAddWithoutValidation(SessionIdHeader, sessionId);
                using var timeout = new CancellationTokenSource(s_endSessionTimeout);

                // Servers that don't let clients end sessions answer 405, so the status code isn't checked.
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to end the MCP session at {Endpoint}", _endpoint);
            }
        }

        /// <summary>
        /// Records the MCP session ID that the server returns.
        /// </summary>
        private sealed class SessionIdHandler : DelegatingHandler
        {
            private string? _sessionId;

            public string? SessionId => Volatile.Read(ref _sessionId);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode &&
                    response.Headers.TryGetValues(SessionIdHeader, out var values) &&
                    values.FirstOrDefault() is { Length: > 0 } sessionId)
                {
                    Volatile.Write(ref _sessionId, sessionId);
                }

                return response;
            }
        }
    }
}
