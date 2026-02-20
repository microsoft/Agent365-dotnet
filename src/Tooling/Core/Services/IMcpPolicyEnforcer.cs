// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    /// <summary>
    /// Result of SDK-level MCP policy enforcement.
    /// </summary>
    public record McpPolicyEnforcementResult
    {
        /// <summary>
        /// Gets whether the tool call is allowed to proceed.
        /// </summary>
        public bool IsAllowed { get; init; }

        /// <summary>
        /// Gets the error message to return when access is denied.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Gets the name of the policy rule that blocked the request.
        /// </summary>
        public string? PolicyRuleId { get; init; }

        /// <summary>
        /// Creates a result indicating the call is allowed.
        /// </summary>
        public static McpPolicyEnforcementResult Allowed() =>
            new() { IsAllowed = true };

        /// <summary>
        /// Creates a result indicating device-path routing is required.
        /// </summary>
        /// <param name="serverName">The MCP server name.</param>
        /// <param name="policyRuleId">The policy rule ID.</param>
        public static McpPolicyEnforcementResult DevicePathRequired(string serverName, string? policyRuleId = null) =>
            new()
            {
                IsAllowed = false,
                ErrorMessage = $"Access to {serverName} requires routing through an Intune-managed device. " +
                               "Your IT admin policy requires this action to be executed via a managed device.",
                PolicyRuleId = policyRuleId
            };

        /// <summary>
        /// Creates a result indicating access is denied.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="policyRuleId">The policy rule ID.</param>
        public static McpPolicyEnforcementResult Denied(string errorMessage, string? policyRuleId = null) =>
            new()
            {
                IsAllowed = false,
                ErrorMessage = errorMessage,
                PolicyRuleId = policyRuleId
            };
    }

    /// <summary>
    /// Enforces MCP access policies at the SDK level before tool invocation.
    /// This is used to block direct cloud access to MCP servers that require device-path routing.
    /// </summary>
    public interface IMcpPolicyEnforcer
    {
        /// <summary>
        /// Checks if a tool call should be allowed at the SDK level.
        /// </summary>
        /// <param name="serverName">Name of the MCP server.</param>
        /// <param name="toolName">Name of the tool being called.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Policy enforcement result indicating whether the call is allowed.</returns>
        Task<McpPolicyEnforcementResult> EnforceAsync(
            string serverName,
            string toolName,
            CancellationToken cancellationToken = default);
    }
}
