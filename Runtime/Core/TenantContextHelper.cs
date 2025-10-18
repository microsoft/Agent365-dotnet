using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.A365.Runtime
{
    /// <summary>
    /// Helper class for extracting tenant and worker context from ASP.NET Core HttpContext.
    /// Provides standardized ways to retrieve tenant and worker IDs from various sources
    /// including claims, headers, and request items.
    /// </summary>
    public static class TenantContextHelper
    {
        /// <summary>
        /// Claim name used to store tenant identifier in ClaimsPrincipal.
        /// </summary>
        public const string TenantClaimName = "tenant_id";

        /// <summary>
        /// Claim name used to store worker identifier in ClaimsPrincipal.
        /// </summary>
        public const string WorkerClaimName = "worker_id";

        /// <summary>
        /// Header name expected to contain tenant identifier.
        /// </summary>
        public const string TenantHeaderName = "X-Tenant-Id";

        /// <summary>
        /// Header name expected to contain worker identifier.
        /// </summary>
        public const string WorkerHeaderName = "X-Worker-Id";

        /// <summary>
        /// HttpContext.Items key expected to contain tenant identifier.
        /// </summary>
        public const string TenantItemKey = "TenantId";

        /// <summary>
        /// HttpContext.Items key expected to contain worker identifier.
        /// </summary>
        public const string WorkerItemKey = "WorkerId";

        /// <summary>
        /// Extracts the tenant ID from the HttpContext.
        /// Checks in order: user claims, request headers, and request items.
        /// </summary>
        /// <param name="context">The HttpContext to extract tenant ID from.</param>
        /// <returns>The tenant ID if found, otherwise null.</returns>
        public static string? GetTenantId(HttpContext? context)
        {
            if (context == null) return null;

            // Try claims first (most secure)
            var tenantFromClaims = context.User?.FindFirst(TenantClaimName)?.Value;
            if (!string.IsNullOrWhiteSpace(tenantFromClaims))
                return tenantFromClaims;

            // Try headers (fallback for API scenarios)
            if (context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValue))
            {
                var tenantFromHeaders = headerValue.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(tenantFromHeaders))
                    return tenantFromHeaders;
            }

            // Try items (middleware-set values)
            if (context.Items.TryGetValue(TenantItemKey, out var itemValue))
            {
                var tenantFromItems = itemValue?.ToString();
                if (!string.IsNullOrWhiteSpace(tenantFromItems))
                    return tenantFromItems;
            }

            return null;
        }

        /// <summary>
        /// Extracts the worker ID from the HttpContext.
        /// Checks in order: user claims, request headers, and request items.
        /// </summary>
        /// <param name="context">The HttpContext to extract worker ID from.</param>
        /// <returns>The worker ID if found, otherwise null.</returns>
        public static string? GetWorkerId(HttpContext? context)
        {
            if (context == null) return null;

            // Try claims first (most secure)
            var workerFromClaims = context.User?.FindFirst(WorkerClaimName)?.Value;
            if (!string.IsNullOrWhiteSpace(workerFromClaims))
                return workerFromClaims;

            // Try headers (fallback for API scenarios)
            if (context.Request.Headers.TryGetValue(WorkerHeaderName, out var headerValue))
            {
                var workerFromHeaders = headerValue.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(workerFromHeaders))
                    return workerFromHeaders;
            }

            // Try items (middleware-set values)
            if (context.Items.TryGetValue(WorkerItemKey, out var itemValue))
            {
                var workerFromItems = itemValue?.ToString();
                if (!string.IsNullOrWhiteSpace(workerFromItems))
                    return workerFromItems;
            }

            return null;
        }
    }
}