// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Factory for creating HttpClient instances with configured timeouts for Agent365 exporters.
    /// </summary>
    /// <remarks>
    /// This factory creates HttpClient instances without custom handlers or IHttpClientFactory integration.
    /// In high-load scenarios, callers should ensure HttpClient instances are reused (e.g., passed via constructor)
    /// to avoid socket exhaustion. This factory is intended for creating a single HttpClient per exporter instance
    /// that is reused for the exporter's lifetime.
    /// </remarks>
    internal static class HttpClientFactory
    {
        /// <summary>
        /// Creates a new HttpClient with the specified timeout.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The timeout in milliseconds. Must be a positive value, or -1 for infinite timeout.
        /// </param>
        /// <returns>A new HttpClient instance with the configured timeout.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="timeoutMilliseconds"/> is 0 or less than -1.
        /// </exception>
        public static HttpClient CreateWithTimeout(int timeoutMilliseconds)
        {
            if (timeoutMilliseconds == 0 || timeoutMilliseconds < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds),
                    timeoutMilliseconds,
                    "Timeout must be a positive value, or -1 for infinite timeout.");
            }

            var timeout = timeoutMilliseconds == -1
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromMilliseconds(timeoutMilliseconds);

            return new HttpClient
            {
                Timeout = timeout
            };
        }
    }
}
