using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.Builder;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Extension methods for extracting values from ITurnContext.
    /// </summary>
    public static class TurnContextExtensions
    {
        private const string ChannelIdAgents = "agents";
        private const string EntityTypeWpxComment = "wpxcomment";
        private const string EntityTypeEmailNotification = "emailNotification";
        private const string WpxCommentConversationIdFormat = "{0}_{1}";
        private const string AgentRole = "agenticUser";
        private const string O11ySpanIdKey = "O11ySpanId";
        private const string O11yTraceIdKey = "O11yTraceId";

        /// <summary>
        /// Extracts caller-related baggage key-value pairs from the provided turn context.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetCallerBaggagePairs(this ITurnContext turnContext)
        {
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiCallerIdKey, turnContext.Activity?.From?.Id);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiCallerNameKey, turnContext.Activity?.From?.Name);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiCallerUpnKey, turnContext.Activity?.From?.Name);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiCallerUserIdKey, turnContext.Activity?.From?.AgenticUserId ?? turnContext.Activity?.From?.AadObjectId);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiCallerTenantIdKey, turnContext.Activity?.From?.TenantId);
        }

        /// <summary>
        /// Extracts the execution type baggage key-value pair based on caller and recipient agentic status.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetExecutionTypePair(this ITurnContext turnContext)
        {
            var isAgenticCaller = turnContext.Activity?.From?.AgenticUserId != null
                || (turnContext.Activity?.From?.Role != null && turnContext.Activity.From.Role.Equals(AgentRole, StringComparison.OrdinalIgnoreCase));
            var isAgenticRecipient = turnContext.Activity?.Recipient?.AgenticUserId != null
                || (turnContext.Activity?.Recipient?.Role != null && turnContext.Activity.Recipient.Role.Equals(AgentRole, StringComparison.OrdinalIgnoreCase));
            var executionType = isAgenticRecipient && isAgenticCaller ? ExecutionType.Agent2Agent.ToString() : ExecutionType.HumanToAgent.ToString();
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiExecutionTypeKey, executionType);
        }

        /// <summary>
        /// Extracts target agent-related baggage key-value pairs from the provided turn context.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetTargetAgentBaggagePairs(this ITurnContext turnContext)
        {
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiAgentIdKey, turnContext.Activity?.Recipient?.AgenticAppId ?? turnContext.Activity?.Recipient?.Id);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiAgentNameKey, turnContext.Activity?.Recipient?.Name);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiAgentAUIDKey, turnContext.Activity?.Recipient?.AgenticUserId ?? turnContext.Activity?.Recipient?.AadObjectId);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiAgentUPNKey, turnContext.Activity?.Recipient?.Name);
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
        /// Extracts source metadata baggage key-value pairs from the provided turn context.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetSourceMetadataBaggagePairs(this ITurnContext turnContext)
        {
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiExecutionSourceIdKey, turnContext.Activity?.ChannelId);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiExecutionSourceNameKey, turnContext.Activity?.ChannelId);
            yield return new KeyValuePair<string, object?>(OpenTelemetryConstants.GenAiExecutionSourceDescriptionKey, turnContext.Activity?.Type);
        }

        /// <summary>
        /// Extracts conversation ID and item link baggage key-value pairs from the provided turn context.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, object?>> GetConversationIdAndItemLinkPairs(this ITurnContext turnContext)
        {
            string? conversationId = null;
            if (turnContext.Activity?.ChannelId == ChannelIdAgents)
            {
                var wpxCommentEntity = turnContext.Activity.Entities?.FirstOrDefault(e => (e as dynamic)?.type == EntityTypeWpxComment);
                if (wpxCommentEntity != null)
                {
                    dynamic entity = wpxCommentEntity;
                    string documentId = entity.documentId;
                    string parentCommentId = entity.parentCommentId;
                    if (!string.IsNullOrWhiteSpace(documentId) && !string.IsNullOrWhiteSpace(parentCommentId))
                    {
                        conversationId = string.Format(WpxCommentConversationIdFormat, documentId, parentCommentId);
                    }
                }
                else
                {
                    var emailNotificationEntity = turnContext.Activity.Entities?.FirstOrDefault(e => (e as dynamic)?.type == EntityTypeEmailNotification);
                    if (emailNotificationEntity != null)
                    {
                        dynamic entity = emailNotificationEntity;
                        conversationId = entity.conversationId;
                    }
                }
            }
            else
            {
                conversationId = turnContext?.Activity?.Conversation?.Id;
            }
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
