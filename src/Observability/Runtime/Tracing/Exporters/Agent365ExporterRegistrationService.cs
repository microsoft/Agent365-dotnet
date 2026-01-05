// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Collections.Concurrent;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Default implementation of the Agent365 exporter registration service.
    /// </summary>
    internal sealed class Agent365ExporterRegistrationService : IAgent365ExporterRegistrationService
    {
        private readonly ConcurrentDictionary<Agent365ExporterType, bool> _registeredExporters = new ConcurrentDictionary<Agent365ExporterType, bool>();

        /// <inheritdoc />
        public bool TryRegisterExporter(Agent365ExporterType exporterType)
        {
            return _registeredExporters.TryAdd(exporterType, true);
        }

        /// <inheritdoc />
        public void ClearRegisteredExporters()
        {
            _registeredExporters.Clear();
        }

        /// <inheritdoc />
        public bool IsExporterRegistered(Agent365ExporterType exporterType)
        {
            return _registeredExporters.ContainsKey(exporterType);
        }
    }
}