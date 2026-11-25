// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Tooling.Handlers
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Custom HTTP handler for logging HTTP requests and responses
    /// </summary>
    /// <remarks>
    /// Logs the response status code and headers. Response bodies aren't read, so streaming responses
    /// such as the MCP <c>text/event-stream</c> GET stream are returned to the caller as soon as their headers arrive.
    /// </remarks>
    public class HttpLoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpLoggingHandler"/> class.
        /// </summary>
        /// <param name="logger">The logger to use for logging HTTP requests and responses.</param>
        public HttpLoggingHandler(ILogger logger)
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                // Buffer the request body so it keeps being sent with a Content-Length header rather than chunked.
                await request.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
            }

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            this._logger.LogInformation($"HTTP Response: {response.StatusCode}");
            this._logger.LogInformation($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"))}");

            return response;
        }
    }
}
