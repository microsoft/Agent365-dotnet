// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing;
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
            OpenTelemetryConstants.GenAiAgentVersionKey,
            OpenTelemetryConstants.AgentAUIDKey,
            OpenTelemetryConstants.AgentEmailKey,
            OpenTelemetryConstants.AgentBlueprintIdKey,
            OpenTelemetryConstants.AgentPlatformIdKey,
            OpenTelemetryConstants.TenantIdKey,
            OpenTelemetryConstants.GenAiProviderNameKey,
            OpenTelemetryConstants.ServerAddressKey,
            OpenTelemetryConstants.ServerPortKey,
            OpenTelemetryConstants.ChannelNameKey,
            OpenTelemetryConstants.ChannelLinkKey,
            OpenTelemetryConstants.UserIdKey,
            OpenTelemetryConstants.UserEmailKey,
            OpenTelemetryConstants.UserNameKey,
            OpenTelemetryConstants.CallerAgentNameKey,
            OpenTelemetryConstants.CallerAgentIdKey,
            OpenTelemetryConstants.CallerAgentBlueprintIdKey,
            OpenTelemetryConstants.CallerAgentAUIDKey,
            OpenTelemetryConstants.CallerAgentEmailKey,
            OpenTelemetryConstants.CallerAgentPlatformIdKey,
            OpenTelemetryConstants.CallerAgentVersionKey,
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
                var wrapper = MessageUtils.NormalizeInputMessages(messages);
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiInputMessagesKey, MessageUtils.Serialize(wrapper));
            }
        }

        /// <summary>
        /// Adds attributes for output messages.
        /// </summary>
        protected static void AddOutputMessagesAttributes(IDictionary<string, object?> attributes, string[]? messages)
        {
            if (messages != null && messages.Length > 0)
            {
                var wrapper = MessageUtils.NormalizeOutputMessages(messages);
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOutputMessagesKey, MessageUtils.Serialize(wrapper));
            }
        }

        /// <summary>
        /// Adds agent details to the attributes dictionary, including tenant ID.
        /// </summary>
        protected static void AddAgentDetails(IDictionary<string, object?> attributes, AgentDetails agentDetails)
        {
            if (agentDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentDescriptionKey, agentDetails.AgentDescription);
            AddIfNotNull(attributes, OpenTelemetryConstants.AgentAUIDKey, agentDetails.AgenticUserId);
            AddIfNotNull(attributes, OpenTelemetryConstants.AgentEmailKey, agentDetails.AgenticUserEmail);
            AddIfNotNull(attributes, OpenTelemetryConstants.AgentBlueprintIdKey, agentDetails.AgentBlueprintId);
            AddIfNotNull(attributes, OpenTelemetryConstants.AgentPlatformIdKey, agentDetails.AgentPlatformId);
            AddIfNotNull(attributes, OpenTelemetryConstants.TenantIdKey, agentDetails.TenantId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiProviderNameKey, agentDetails.ProviderName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentVersionKey, agentDetails.AgentVersion);
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

            AddChannelAttributes(attributes, request.Channel);
        }

        /// <summary>
        /// Adds caller details to the attributes dictionary.
        /// Extracts user details and caller agent details from the composite CallerDetails.
        /// </summary>
        protected static void AddCallerDetails(IDictionary<string, object?> attributes, CallerDetails? callerDetails)
        {
            if (callerDetails == null) return;

            var userDetails = callerDetails.UserDetails;
            if (userDetails != null)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.UserIdKey, userDetails.UserId);
                AddIfNotNull(attributes, OpenTelemetryConstants.UserEmailKey, userDetails.UserEmail);
                AddIfNotNull(attributes, OpenTelemetryConstants.UserNameKey, userDetails.UserName);
                AddIfNotNull(attributes, OpenTelemetryConstants.CallerClientIpKey, userDetails.UserClientIP?.ToString());
            }

            AddCallerAgentDetails(attributes, callerDetails.CallerAgentDetails);
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
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentAUIDKey, callerAgentDetails.AgenticUserId);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentEmailKey, callerAgentDetails.AgenticUserEmail);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentPlatformIdKey, callerAgentDetails.AgentPlatformId);
            AddIfNotNull(attributes, OpenTelemetryConstants.CallerAgentVersionKey, callerAgentDetails.AgentVersion);
        }

        /// <summary>
        /// Adds channel attributes to the attributes dictionary.
        /// </summary>
        protected static void AddChannelAttributes(IDictionary<string, object?> attributes, Channel? channel)
        {
            if (channel == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.ChannelNameKey, channel.Name);
            AddIfNotNull(attributes, OpenTelemetryConstants.ChannelLinkKey, channel.Link);
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
