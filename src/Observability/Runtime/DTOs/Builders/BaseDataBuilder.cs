// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Base class for building telemetry data.
    /// </summary>
    public abstract class BaseDataBuilder<T> where T : BaseData
    {
        // Reserved attribute keys managed by specific builder methods; extra attributes must NOT override these.
        private static readonly HashSet<string> ReservedAttributeKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            OpenTelemetryConstants.GenAiInputMessagesKey,
            OpenTelemetryConstants.GenAiOutputMessagesKey,
            OpenTelemetryConstants.GenAiAgentIdKey,
            OpenTelemetryConstants.GenAiAgentNameKey,
            OpenTelemetryConstants.GenAiAgentDescriptionKey,
            OpenTelemetryConstants.GenAiAgentAUIDKey,
            OpenTelemetryConstants.GenAiAgentUPNKey,
            OpenTelemetryConstants.GenAiAgentBlueprintIdKey,
            OpenTelemetryConstants.GenAiAgentPlatformIdKey,
            OpenTelemetryConstants.TenantIdKey,
            OpenTelemetryConstants.ServerAddressKey,
            OpenTelemetryConstants.ServerPortKey,
            OpenTelemetryConstants.GenAiChannelNameKey,
            OpenTelemetryConstants.GenAiChannelLinkKey,
            OpenTelemetryConstants.GenAiExecutionTypeKey,
            OpenTelemetryConstants.GenAiCallerIdKey,
            OpenTelemetryConstants.GenAiCallerUpnKey,
            OpenTelemetryConstants.GenAiCallerNameKey,
            OpenTelemetryConstants.GenAiCallerTenantIdKey,
            OpenTelemetryConstants.GenAiCallerAgentNameKey,
            OpenTelemetryConstants.GenAiCallerAgentIdKey,
            OpenTelemetryConstants.GenAiCallerAgentApplicationIdKey,
            OpenTelemetryConstants.GenAiCallerAgentAUIDKey,
            OpenTelemetryConstants.GenAiCallerAgentUPNKey,
            OpenTelemetryConstants.GenAiCallerAgentTenantKey,
            OpenTelemetryConstants.GenAiConversationIdKey,
            OpenTelemetryConstants.SessionIdKey,
            OpenTelemetryConstants.GenAiToolNameKey,
            OpenTelemetryConstants.GenAiToolArgumentsKey,
            OpenTelemetryConstants.GenAiToolCallIdKey,
            OpenTelemetryConstants.GenAiToolDescriptionKey,
            OpenTelemetryConstants.GenAiToolTypeKey,
            OpenTelemetryConstants.GenAiEventContent,
            OpenTelemetryConstants.GenAiOperationNameKey,
            OpenTelemetryConstants.GenAiRequestModelKey,
            OpenTelemetryConstants.GenAiProviderNameKey,
            OpenTelemetryConstants.GenAiUsageInputTokensKey,
            OpenTelemetryConstants.GenAiUsageOutputTokensKey,
            OpenTelemetryConstants.GenAiResponseFinishReasonsKey,
            OpenTelemetryConstants.GenAiResponseIdKey
        };

        /// <summary>
        /// Adds attributes for input messages.
        /// </summary>
        protected static void AddInputMessagesAttributes(IDictionary<string, object?> attributes, string[]? messages)
        {
            if (messages != null && messages.Length > 0)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", messages));
            }
        }

        /// <summary>
        /// Adds attributes for output messages.
        /// </summary>
        protected static void AddOutputMessagesAttributes(IDictionary<string, object?> attributes, string[]? messages)
        {
            if (messages != null && messages.Length > 0)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", messages));
            }
        }

        /// <summary>
        /// Adds agent details to the attributes dictionary.
        /// </summary>
        protected static void AddAgentDetails(IDictionary<string, object?> attributes, AgentDetails agentDetails)
        {
            if (agentDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentDescriptionKey, agentDetails.AgentDescription);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentAUIDKey, agentDetails.AgentAUID);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentUPNKey, agentDetails.AgentUPN);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentBlueprintIdKey, agentDetails.AgentBlueprintId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentPlatformIdKey, agentDetails.AgentPlatformId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentTypeKey, agentDetails.AgentType?.ToString());
        }

        /// <summary>
        /// Adds tenant details to the attributes dictionary.
        /// </summary>
        protected static void AddTenantDetails(IDictionary<string, object?> attributes, TenantDetails tenantDetails)
        {
            if (tenantDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.TenantIdKey, tenantDetails.TenantId);
        }

        /// <summary>
        /// Adds endpoint details to the attributes dictionary.
        /// </summary>
        protected static void AddEndpointDetails(IDictionary<string, object?> attributes, Uri? endpoint)
        {
            if (endpoint == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.ServerAddressKey, endpoint.Host);

            // Only record port if it is different from 443
            if (endpoint.Port != 443)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.ServerPortKey, endpoint.Port);
            }
        }

        /// <summary>
        /// Adds request details to the attributes dictionary.
        /// </summary>
        protected static void AddRequestDetails(IDictionary<string, object?> attributes, Request? request)
        {
            if (request == null) return;

            AddSourceMetadataAttributes(attributes, request.SourceMetadata);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionTypeKey, request.ExecutionType?.ToString());
        }

        /// <summary>
        /// Adds caller details to the attributes dictionary.
        /// </summary>
        protected static void AddCallerDetails(IDictionary<string, object?> attributes, CallerDetails? callerDetails)
        {
            if (callerDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerTenantIdKey, callerDetails.TenantId);
        }

        /// <summary>
        /// Adds caller agent details to the attributes dictionary.
        /// </summary>
        protected static void AddCallerAgentDetails(IDictionary<string, object?> attributes, AgentDetails? callerAgentDetails)
        {
            if (callerAgentDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentNameKey, callerAgentDetails.AgentName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentIdKey, callerAgentDetails.AgentId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentApplicationIdKey, callerAgentDetails.AgentBlueprintId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentAUIDKey, callerAgentDetails.AgentAUID);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentUPNKey, callerAgentDetails.AgentUPN);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentTenantKey, callerAgentDetails.TenantId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentPlatformIdKey, callerAgentDetails.AgentPlatformId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentTypeKey, callerAgentDetails.AgentType?.ToString());
        }

        /// <summary>
        /// Adds source metadata attributes to the attributes dictionary.
        /// </summary>
        protected static void AddSourceMetadataAttributes(IDictionary<string, object?> attributes, SourceMetadata? sourceMetadata)
        {
            if (sourceMetadata == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiChannelNameKey, sourceMetadata.Name);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiChannelLinkKey, sourceMetadata.Description);
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary if the value is not null.
        /// </summary>
        protected static void AddIfNotNull(IDictionary<string, object?> attributes, string key, object? value)
        {
            if (value != null)
            {
                attributes[key] = value;
            }
        }

        /// <summary>
        /// Adds extra attributes to the attributes dictionary while ignoring reserved keys.
        /// </summary>
        protected static void AddExtraAttributes(IDictionary<string, object?> attributes, IDictionary<string, object?>? extraAttributes)
        {
            if (extraAttributes == null) return;

            foreach (var kvp in extraAttributes)
            {
                if ((kvp.Value != null && !ReservedAttributeKeys.Contains(kvp.Key)))
                {
                    attributes[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
