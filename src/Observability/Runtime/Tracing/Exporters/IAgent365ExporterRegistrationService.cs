// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Service to track registered Agent365 exporters and prevent duplicate registrations.
    /// </summary>
    internal interface IAgent365ExporterRegistrationService
    {
        /// <summary>
        /// Attempts to register an exporter type if not already registered.
        /// </summary>
        /// <param name="exporterType">The exporter type to register.</param>
        /// <returns>True if the exporter was newly registered, false if already registered.</returns>
        bool TryRegisterExporter(Agent365ExporterType exporterType);

        /// <summary>
        /// Checks if an exporter type is already registered.
        /// </summary>
        /// <param name="exporterType">The exporter type to check.</param>
        /// <returns>True if the exporter is registered, false otherwise.</returns>
        bool IsExporterRegistered(Agent365ExporterType exporterType);

        /// <summary>
        /// Clears all registered exporters. Used primarily for testing scenarios.
        /// </summary>
        void ClearRegisteredExporters();
    }
}