// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Handlers;

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
    /// <param name="logger">The logger instance.</param>
    public HttpLoggingHandler(ILogger logger)
    {
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        return response;
    }
}