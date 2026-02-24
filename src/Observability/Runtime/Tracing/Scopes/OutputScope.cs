// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for AI agent output operations.
    /// </summary>
    public sealed class OutputScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for output tracing.
        /// </summary>
        public const string OperationName = "output_messages";

        private readonly List<string> outputMessages = new List<string>();

        /// <summary>
        /// Creates and starts a new scope for output tracing.
        /// </summary>
        /// <param name="agentDetails">Information about the agent producing the output.</param>
        /// <param name="tenantDetails">Tenant context used for telemetry enrichment and correlation.</param>
        /// <param name="response">Response containing output messages.</param>
        /// <param name="parentId">Optional parent Activity ID used to link this span to an upstream operation.</param>
        /// <param name="startTime">Optional explicit start time. Useful when recording an output operation after execution has already completed.</param>
        /// <param name="endTime">Optional explicit end time. When provided, the span will use this timestamp when disposed instead of the current wall-clock time.</param>
        /// <returns>A new OutputScope instance.</returns>
        public static OutputScope Start(AgentDetails agentDetails, TenantDetails tenantDetails, Response response, string? parentId = null, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null)
            => new OutputScope(agentDetails, tenantDetails, response, parentId, startTime, endTime);

        private OutputScope(AgentDetails agentDetails, TenantDetails tenantDetails, Response response, string? parentId, DateTimeOffset? startTime, DateTimeOffset? endTime)
            : base(
                kind: ActivityKind.Client,
                agentDetails: agentDetails,
                tenantDetails: tenantDetails,
                operationName: OperationName,
                activityName: $"{OperationName} {agentDetails?.AgentId}",
                startTime: startTime,
                endTime: endTime,
                parentId: parentId)
        {
            if (response.Messages.Count > 0)
            {
                foreach (var message in response.Messages)
                {
                    outputMessages.Add(message);
                }

                SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages));
            }
        }

        /// <summary>
        /// Records additional output messages and appends them to the existing output messages attribute.
        /// </summary>
        /// <param name="messages">The messages to append to the output.</param>
        public void RecordOutputMessages(IEnumerable<string> messages)
        {
            if (messages == null)
            {
                return;
            }

            foreach (var message in messages)
            {
                outputMessages.Add(message);
            }

            SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages));
        }
    }
}
