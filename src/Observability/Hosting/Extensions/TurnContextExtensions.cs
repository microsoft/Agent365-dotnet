// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.A365.Observability.Hosting.Extensions
{
    /// <summary>
    /// Extension methods for extracting values from ITurnContext.
    /// </summary>
    public static class TurnContextExtensions
    {
        private const string O11ySpanIdKey = "O11ySpanId";
        private const string O11yTraceIdKey = "O11yTraceId";

        /// <summary>
        /// Extracts caller-related baggage key-value pairs from the provided turn context.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetCallerBaggagePairs(this ITurnContext turnContext)
        {
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.UserIdKey, turnContext.Activity?.From?.AadObjectId);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.UserNameKey, turnContext.Activity?.From?.Name);
        }

        /// <summary>
        /// Extracts target agent-related baggage key-value pairs from the provided turn context.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetTargetAgentBaggagePairs(this ITurnContext turnContext)
        {
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiAgentIdKey, turnContext.Activity?.Recipient?.AgenticAppId ?? turnContext.Activity?.Recipient?.Id);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiAgentNameKey, turnContext.Activity?.Recipient?.Name);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.AgentAUIDKey, turnContext.Activity?.Recipient?.AgenticUserId ?? turnContext.Activity?.Recipient?.AadObjectId);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiAgentDescriptionKey, turnContext.Activity?.Recipient?.Role);
        }

        /// <summary>
        /// Extracts the tenant ID baggage key-value pair, attempting to retrieve from ChannelData if necessary.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetTenantIdPair(this ITurnContext turnContext)
        {
            var tenantId = turnContext.Activity?.Recipient?.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId) && turnContext.Activity?.ChannelData != null)
            {
                try
                {
                    var channelDataJson = turnContext.Activity.ChannelData.ToString();
                    if (!string.IsNullOrWhiteSpace(channelDataJson))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(channelDataJson);
                        if (doc.RootElement.TryGetProperty("tenant", out var tenantElem) &&
                            tenantElem.TryGetProperty("id", out var idElem) &&
                            idElem.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            tenantId = idElem.GetString();
                        }
                    }
                }
                catch
                {
                }
            }
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.TenantIdKey, tenantId);
        }

        /// <summary>
        /// Extracts channel baggage key-value pairs from the provided turn context.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetChannelBaggagePairs(this ITurnContext turnContext)
        {
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.ChannelNameKey, turnContext.Activity?.ChannelId?.Channel);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.ChannelLinkKey, turnContext.Activity?.ChannelId?.SubChannel);
        }

        /// <summary>
        /// Extracts conversation ID and item link baggage key-value pairs from the provided turn context.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetConversationIdAndItemLinkPairs(this ITurnContext turnContext)
        {
            string? conversationId = turnContext?.Activity?.Conversation?.Id;
            string? itemLink = turnContext?.Activity?.ServiceUrl;
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiConversationItemLinkKey, itemLink);
        }

        /// <summary>
        /// Injects observability context into the turn context.
        /// </summary>
        public static void InjectObservabilityContext(this ITurnContext turnContext, OpenTelemetryScope observabilityScope)
        {
            turnContext.StackState[O11ySpanIdKey] = observabilityScope.Id;
            turnContext.StackState[O11yTraceIdKey] = observabilityScope.TraceId;
        }
    }
}
