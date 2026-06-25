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

        /// <summary>
        /// Applies an OpenTelemetry status to the built data based on an optional error, and records
        /// the <c>error.type</c> attribute when an error is present. When <paramref name="error"/> is
        /// <c>null</c> the status is left as <see cref="SpanStatusCode.Unset"/>.
        /// </summary>
        /// <param name="data">The telemetry data to annotate.</param>
        /// <param name="error">Optional exception describing a failure for the operation.</param>
        /// <returns>The same <paramref name="data"/> instance, for fluent use.</returns>
        protected static T ApplyStatus(T data, Exception? error)
        {
            var status = SpanStatusBuilder.FromError(error, data.Attributes);
            data.StatusCode = status.Code;
            data.StatusMessage = status.Message;
            return data;
        }

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
        /// Adds telemetry SDK attributes (name, version, language) to the attributes dictionary.
        /// </summary>
        protected static void AddSdkAttributes(IDictionary<string, object?> attributes)
        {
            attributes[OpenTelemetryConstants.TelemetrySdkNameKey] = OpenTelemetryConstants.TelemetrySdkNameValue;
            attributes[OpenTelemetryConstants.TelemetrySdkVersionKey] = OpenTelemetryConstants.TelemetrySdkVersionValue;
            attributes[OpenTelemetryConstants.TelemetrySdkLanguageKey] = OpenTelemetryConstants.TelemetrySdkLanguageValue;
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
        /// Adds extra attributes to the attributes dictionary.
        /// Extra attributes cannot override keys already set by the builder on this span.
        /// </summary>
        protected static void AddExtraAttributes(IDictionary<string, object?> attributes, IDictionary<string, object?>? extraAttributes)
        {
            if (extraAttributes == null) return;

            foreach (var kvp in extraAttributes)
            {
                if (kvp.Value != null && !attributes.ContainsKey(kvp.Key))
                {
                    attributes[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
