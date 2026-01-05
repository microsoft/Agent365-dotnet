// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Hosting.Caching
{
    /// <summary>
    /// Cache only for observability (exporter) scoped tokens per (agentId, tenantId).
    /// </summary>
    public interface IExporterTokenCache<T> where T : class
    {
        /// <summary>
        /// Registers (idempotent) a credential to be used for observability token acquisition.
        /// </summary>
        void RegisterObservability(string agentId, string tenantId, T tokenGenerator, string[] observabilityScopes);

        /// <summary>
        /// Returns an observability token (cached inside the credential) or null on failure/not registered.
        /// </summary>
        Task<string?> GetObservabilityToken(string agentId, string tenantId);
    }
}