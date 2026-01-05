// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Represents the supported Agent365 exporter types.
    /// </summary>
    public enum Agent365ExporterType
    {
        /// <summary>
        /// Agent365 synchronous exporter type.
        /// </summary>
        Agent365Exporter = 0,

        /// <summary>
        /// Agent365 asynchronous exporter type.
        /// </summary>
        Agent365ExporterAsync = 1
    }
}
