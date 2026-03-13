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
            OpenTelemetryConstants.AgentAUIDKey,
            OpenTelemetryConstants.AgentUPNKey,
            OpenTelemetryConstants.AgentBlueprintIdKey,
            OpenTelemetryConstants.AgentPlatformIdKey,
            OpenTelemetryConstants.TenantIdKey,
            OpenTelemetryConstants.ServerAddressKey,
            OpenTelemetryConstants.ServerPortKey,
            OpenTelemetryConstants.ChannelNameKey,
            OpenTelemetryConstants.ChannelLinkKey,
            OpenTelemetryConstants.CallerIdKey,
            OpenTelemetryConstants.CallerUpnKey,
            OpenTelemetryConstants.CallerNameKey,
            OpenTelemetryConstants.CallerAgentNameKey,
            OpenTelemetryConstants.CallerAgentIdKey,
            OpenTelemetryConstants.CallerAgentBlueprintIdKey,
            OpenTelemetryConstants.CallerAgentAUIDKey,
            OpenTelemetryConstants.CallerAgentUPNKey,
            OpenTelemetryConstants.CallerClientIpKey,
            OpenTelemetryConstants.GenAiConversationIdKey,
            OpenTelemetryConstants.SessionIdKey,
            OpenTelemetryConstants.GenAiToolNameKey,
            OpenTelemetryConstants.GenAiToolArgumentsKey,
            OpenTelemetryConstants.GenAiToolCallIdKey,
            OpenTelemetryConstants.GenAiToolDescriptionKey,
            OpenTelemetryConstants.GenAiToolTypeKey,
            OpenTelemetryConstants.GenAiToolCallResultKey,
            OpenTelemetryConstants.GenAiOperationNameKey,
            OpenTelemetryConstants.GenAiRequestModelKey,
            OpenTelemetryConstants.GenAiProviderNameKey,
            OpenTelemetryConstants.GenAiUsageInputTokensKey,
            OpenTelemetryConstants.GenAiUsageOutputTokensKey,
            OpenTelemetryConstants.GenAiResponseFinishReasonsKey,
            OpenTelemetryConstants.GenAiAgentThoughtProcessKey
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
            AddIfNotNull(attributes, OpenTelemetryConstants.AgentAUIDKey, agentDetails.AgentAUID);
            AddIfNotNull(attributes, OpenTelemetryConstants.AgentUPNKey, agentDetails.AgentUPN);
            AddIfNotNull(attributes, OpenTelemetryConstants.AgentBlueprintIdKey, agentDetails.AgentBlueprintId);
            AddIfNotNull(attributes, OpenTelemetryConstants.AgentPlatformIdKey, agentDetails.AgentPlatformId);
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
                AddIfNotNull(attributes, OpenTelemetryConstants.ServerPortKey, endpoint.Port.ToString());
            }
        }

        /// <summary>
        /// Adds request details to the attributes dictionary.
        /// </summary>
        protected static void AddRequestDetails(IDictionary<string, object?> attributes, Request? request)
        {
            if (request == null) return;

            AddSourceMetadataAttributes(attributes, request.SourceMetadata);
        }

        /// <summary>
        /// Adds caller details to the attributes dictionary.
        /// </summary>
        protected static void AddCallerDetails(IDictionary<string, object?> attributes, CallerDetails? callerDetails)
        {
            if (callerDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.CallerIdKey, callerDetails.CallerId);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerUpnKey, callerDetails.CallerUpn);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerNameKey, callerDetails.CallerName);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerClientIpKey, callerDetails.CallerClientIP?.ToString());
        }

        /// <summary>
        /// Adds caller agent details to the attributes dictionary.
        /// </summary>
        protected static void AddCallerAgentDetails(IDictionary<string, object?> attributes, AgentDetails? callerAgentDetails)
        {
            if (callerAgentDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentNameKey, callerAgentDetails.AgentName);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentIdKey, callerAgentDetails.AgentId);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentBlueprintIdKey, callerAgentDetails.AgentBlueprintId);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentAUIDKey, callerAgentDetails.AgentAUID);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentUPNKey, callerAgentDetails.AgentUPN);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentPlatformIdKey, callerAgentDetails.AgentPlatformId);
        }

        /// <summary>
        /// Adds source metadata attributes to the attributes dictionary.
        /// </summary>
        protected static void AddSourceMetadataAttributes(IDictionary<string, object?> attributes, SourceMetadata? sourceMetadata)
        {
            if (sourceMetadata == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.ChannelNameKey, sourceMetadata.Name);
            AddIfNotNull(attributes, OpenTelemetryConstants.ChannelLinkKey, sourceMetadata.Description);
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
