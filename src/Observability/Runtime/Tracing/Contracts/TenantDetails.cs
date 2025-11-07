// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents the tenant id attached to the span.
    /// </summary>
    public sealed class TenantDetails : IEquatable<TenantDetails>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TenantDetails"/> class.
        /// </summary>
        /// <param name="tenantId">The identifier of the tenant.</param>
        public TenantDetails(Guid tenantId)
        {
            TenantId = tenantId;
        }

        /// <summary>
        /// The unique identifier for the tenant.
        /// </summary>
        public Guid TenantId { get; }

        /// <summary>
        /// Deconstructs this instance into its tenant identifier.
        /// </summary>
        /// <param name="tenantId">Receives the tenant identifier.</param>
        public void Deconstruct(out Guid tenantId)
        {
            tenantId = TenantId;
        }

        /// <inheritdoc/>
        public bool Equals(TenantDetails? other)
        {
            return other != null && TenantId.Equals(other.TenantId);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as TenantDetails);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return TenantId.GetHashCode();
        }
    }
}