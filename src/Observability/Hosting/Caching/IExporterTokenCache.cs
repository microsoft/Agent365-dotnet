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
        /// Registers a credential or resolver used for app-only observability token acquisition.
        /// Whether a repeated registration for the same agent and tenant replaces the existing one is
        /// implementation-specific: <see cref="AgenticTokenCache"/> keeps the first registration and
        /// <see cref="ServiceTokenCache"/> replaces it.
        /// </summary>
        void RegisterObservability(string agentId, string tenantId, T tokenGenerator, string[] observabilityScopes);

        /// <summary>
        /// Returns an observability token or <c>null</c> when not registered.
        /// Implementations that acquire a new token may propagate resolver failures to the caller.
        /// </summary>
        Task<string?> GetObservabilityToken(string agentId, string tenantId);
    }
}