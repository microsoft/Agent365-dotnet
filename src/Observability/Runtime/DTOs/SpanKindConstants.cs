// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs
{
    /// <summary>
    /// Provides string constants for OpenTelemetry span kind values.
    /// These constants are used in DTOs and Builders to avoid taking a dependency
    /// on <see cref="System.Diagnostics.ActivityKind"/>.
    /// </summary>
    public static class SpanKindConstants
    {
        /// <summary>
        /// Indicates that the span covers server-side handling of a synchronous RPC or other remote request.
        /// </summary>
        public const string Server = "Server";

        /// <summary>
        /// Indicates that the span describes a request to some remote service.
        /// </summary>
        public const string Client = "Client";

        /// <summary>
        /// Indicates that the span describes a producer sending a message to a broker.
        /// </summary>
        public const string Producer = "Producer";

        /// <summary>
        /// Indicates that the span describes a consumer receiving a message from a broker.
        /// </summary>
        public const string Consumer = "Consumer";

        /// <summary>
        /// Default span kind. Indicates that the span represents an internal operation within an application.
        /// </summary>
        public const string Internal = "Internal";
    }
}
