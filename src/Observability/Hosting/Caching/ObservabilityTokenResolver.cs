// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Hosting.Caching
{
    /// <summary>
    /// Resolves an app-only OBS token for the exporting agent and tenant.
    /// The resolver must not perform delegated, OBO, or user_fic authentication for OBS export.
    /// </summary>
    /// <param name="agentId">The exporting agent identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="observabilityScopes">The OBS token scopes to request.</param>
    /// <returns>The final OBS access token for the exporting agent.</returns>
    public delegate Task<string?> ObservabilityTokenResolver(string agentId, string tenantId, string[] observabilityScopes);
}
