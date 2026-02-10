// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an OutputData instance.
    /// </summary>
    public class OutputDataBuilder : BaseDataBuilder<OutputData>
    {
        private const string OutputMessagesOperationName = "output_messages";

        /// <summary>
        /// Builds complete data for an output_messages operation.
        /// </summary>
        /// <param name="agentDetails">The details of the agent.</param>
        /// <param name="tenantDetails">The details of the tenant.</param>
        /// <param name="response">The response containing output messages.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <returns>An OutputData object containing all telemetry data.</returns>
        public static OutputData Build(
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Response response,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = BuildAttributes(agentDetails, tenantDetails, response, extraAttributes);

            return new OutputData(attributes, startTime, endTime, spanId, parentSpanId);
        }

        private static Dictionary<string, object?> BuildAttributes(
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Response response,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = new Dictionary<string, object?>();

            // Operation name
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOperationNameKey, OutputMessagesOperationName);

            // Agent & tenant
            AddAgentDetails(attributes, agentDetails);
            AddTenantDetails(attributes, tenantDetails);

            // Output messages from response
            if (response.Messages.Count > 0)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", response.Messages));
            }

            // Add any extra attributes
            AddExtraAttributes(attributes, extraAttributes);

            return attributes;
        }
    }
}
