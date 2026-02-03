// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Tooling.Handlers
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
            // Log request details
            var requestUri = request.RequestUri?.ToString() ?? "unknown";
            this._logger.LogInformation("[MCP-HTTP] Request: {Method} {Uri}", request.Method, requestUri);

            if (request.Content != null)
            {
                var content = await request.Content.ReadAsStringAsync(cancellationToken);
                // Log MCP tool call requests (contains the tool name and arguments)
                if (requestUri.Contains("/agents/servers/", StringComparison.OrdinalIgnoreCase))
                {
                    this._logger.LogInformation("[MCP-HTTP] Request Body: {Content}", content);
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            this._logger.LogInformation("[MCP-HTTP] Response: {StatusCode}", response.StatusCode);
            this._logger.LogInformation("[MCP-HTTP] Response Headers: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}")));

            if (response.Content != null)
            {
                // Read the response content
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                // Log MCP tool responses (contains the actual data returned from Calendar, Mail, etc.)
                if (requestUri.Contains("/agents/servers/", StringComparison.OrdinalIgnoreCase))
                {
                    // Truncate very long responses but show enough to debug
                    var truncatedContent = responseContent.Length > 5000
                        ? responseContent.Substring(0, 5000) + "... [TRUNCATED]"
                        : responseContent;
                    this._logger.LogInformation("[MCP-HTTP] Response Body: {Content}", truncatedContent);
                }

                // Re-create the response content since ReadAsStringAsync consumed it
                response.Content = new StringContent(responseContent, System.Text.Encoding.UTF8, response.Content.Headers.ContentType?.MediaType ?? "application/json");

                // Copy over the original content headers
                foreach (var header in response.Content.Headers.ToList())
                {
                    if (header.Key != "Content-Type" && header.Key != "Content-Length")
                    {
                        response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            return response;
        }
    }
}
