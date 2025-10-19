using System;
using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides tracing scope for Agent execution.
    /// </summary>
    public sealed class ExecuteAgentScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for agent execution tracing.
        /// </summary>
        public const string OperationName = "execute_agent";

        /// <summary>
        /// Creates and starts a new scope for agent execution tracing.
        /// </summary>
        public static ExecuteAgentScope Start(AgentDetails details, TenantDetails tenantDetails, Request? request = null) =>
            new ExecuteAgentScope(details, tenantDetails, request);
        
        /// <inheritdoc cref="Start(AgentDetails,TenantDetails,Request?)"/>
        public static ExecuteAgentScope? Start(string agentId, Guid tenantId, Request? request = null) =>
            Start(new AgentDetails(agentId), new TenantDetails(tenantId), request);

        private ExecuteAgentScope(AgentDetails details,TenantDetails tenantDetails, Request? request)
            : base(
                ActivityKind.Internal,
                details,
                tenantDetails,
                OperationName,
                $"execute_agent {details.AgentName}")
        {
            SetTagMaybe(GenAiAgentIdKey, details.AgentId);
            AddBaggage(GenAiAgentIdKey, details.AgentId);

            SetTagMaybe(GenAiAgentNameKey, details.AgentName);
            if (details.AgentName != null)
            {
                AddBaggage(GenAiAgentNameKey, details.AgentName);
            }

            SetTagMaybe(GenAiRequestContentKey, request?.Content);
            SetTagMaybe(GenAiExecutionTypeKey, request?.ExecutionType.ToString());
            SetTagMaybe(SessionIdKey, request?.SessionId);
            SetTagMaybe(GenAiExecutionSourceIdKey, request?.SourceMetadata?.Id);
            SetTagMaybe(GenAiExecutionSourceNameKey, request?.SourceMetadata?.Name);
            SetTagMaybe(GenAiIconUriKey, request?.SourceMetadata?.IconUri);
            SetTagMaybe(GenAiExecutionSourceDescriptionKey, request?.SourceMetadata?.Description);
        }
        
        /// <summary>
        /// Records response information for telemetry tracking.
        /// </summary>
        public void RecordResponse(string response)
        {
            SetTagMaybe(GenAiEventContent, response);
        }
    }
}