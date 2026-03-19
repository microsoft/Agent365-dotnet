// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Exceptions
{
    /// <summary>
    /// Error codes for MCP policy violations.
    /// </summary>
    public enum McpPolicyErrorCode
    {
        /// <summary>
        /// General access denied by policy.
        /// </summary>
        AccessDenied,

        /// <summary>
        /// The MCP server requires routing through an Intune-managed device.
        /// The agent should retry via the local MCP path (WNS → locaproto).
        /// </summary>
        DevicePathRequired,

        /// <summary>
        /// The action requires user elevation/approval on the device.
        /// </summary>
        ElevationRequired,

        /// <summary>
        /// The target resource is protected by organization policy.
        /// </summary>
        ProtectedResource
    }

    /// <summary>
    /// Exception thrown when an MCP tool invocation is blocked by organization policy.
    /// This can occur when:
    /// - A remote MCP server requires device-path routing (DevicePathRequired)
    /// - Access to a protected resource is blocked (ProtectedResource)
    /// - User elevation is required but not provided (ElevationRequired)
    /// </summary>
    public class McpPolicyException : Exception
    {
        /// <summary>
        /// Gets the specific policy error code indicating why the request was blocked.
        /// </summary>
        public McpPolicyErrorCode ErrorCode { get; }

        /// <summary>
        /// Gets the name of the MCP server that was blocked.
        /// </summary>
        public string? ServerName { get; }

        /// <summary>
        /// Gets the name of the tool that was blocked.
        /// </summary>
        public string? ToolName { get; }

        /// <summary>
        /// Gets additional data about the policy decision.
        /// </summary>
        public IDictionary<string, object>? AdditionalData { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPolicyException"/> class with a simple message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public McpPolicyException(string message)
            : base(message)
        {
            ErrorCode = McpPolicyErrorCode.AccessDenied;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPolicyException"/> class.
        /// </summary>
        /// <param name="errorCode">The policy error code.</param>
        /// <param name="message">The error message.</param>
        public McpPolicyException(McpPolicyErrorCode errorCode, string? message)
            : base(message ?? GetDefaultMessage(errorCode))
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPolicyException"/> class.
        /// </summary>
        /// <param name="errorCode">The policy error code.</param>
        /// <param name="serverName">The MCP server name.</param>
        /// <param name="toolName">The tool name.</param>
        /// <param name="message">The error message.</param>
        public McpPolicyException(
            McpPolicyErrorCode errorCode,
            string serverName,
            string toolName,
            string? message = null)
            : base(message ?? GetDefaultMessage(errorCode, serverName, toolName))
        {
            ErrorCode = errorCode;
            ServerName = serverName;
            ToolName = toolName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPolicyException"/> class.
        /// </summary>
        /// <param name="errorCode">The policy error code.</param>
        /// <param name="serverName">The MCP server name.</param>
        /// <param name="toolName">The tool name.</param>
        /// <param name="message">The error message.</param>
        /// <param name="additionalData">Additional data about the policy decision.</param>
        public McpPolicyException(
            McpPolicyErrorCode errorCode,
            string serverName,
            string toolName,
            string message,
            IDictionary<string, object>? additionalData)
            : base(message)
        {
            ErrorCode = errorCode;
            ServerName = serverName;
            ToolName = toolName;
            AdditionalData = additionalData;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPolicyException"/> class.
        /// </summary>
        /// <param name="errorCode">The policy error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public McpPolicyException(McpPolicyErrorCode errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        private static string GetDefaultMessage(McpPolicyErrorCode errorCode, string? serverName = null, string? toolName = null)
        {
            var toolInfo = serverName != null && toolName != null
                ? $" ({serverName}/{toolName})"
                : string.Empty;

            return errorCode switch
            {
                McpPolicyErrorCode.AccessDenied =>
                    $"Access to MCP server{toolInfo} is denied by organization policy.",
                McpPolicyErrorCode.DevicePathRequired =>
                    $"Access to MCP server{toolInfo} requires routing through an Intune-managed device. " +
                    "Your IT admin policy requires this action to be executed via a managed device.",
                McpPolicyErrorCode.ElevationRequired =>
                    $"Access to MCP server{toolInfo} requires user approval on the managed device.",
                McpPolicyErrorCode.ProtectedResource =>
                    $"The target resource{toolInfo} is protected by organization policy.",
                _ =>
                    $"MCP policy violation{toolInfo}."
            };
        }
    }
}
