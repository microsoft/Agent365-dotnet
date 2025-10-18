// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Handlers
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Custom HTTP handler for logging HTTP requests and responses
    /// </summary>
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
                var content = await request.Content.ReadAsStringAsync();
            }

            var response = await base.SendAsync(request, cancellationToken);

            this._logger.LogInformation($"HTTP Response: {response.StatusCode}");
            this._logger.LogInformation($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"))}");

            if (response.Content != null)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
            }

            return response;
        }
    }
}
