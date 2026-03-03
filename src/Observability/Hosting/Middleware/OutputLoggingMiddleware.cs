// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Observability.Hosting.Extensions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.A365.Observability.Hosting.Middleware
{
    /// <summary>
    /// Bot Framework middleware that creates <see cref="OutputScope"/> spans
    /// for outgoing messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Links to a parent span when <see cref="A365ParentSpanKey"/> is set in
    /// <see cref="ITurnContext.StackState"/>.
    /// </para>
    /// <para>
    /// <b>Privacy note:</b> Outgoing message content is captured verbatim as
    /// span attributes and exported to the configured telemetry backend.
    /// </para>
    /// </remarks>
    public sealed class OutputLoggingMiddleware : IMiddleware
    {
        /// <summary>
        /// The <see cref="ITurnContext.StackState"/> key used to store the parent
        /// span reference. Set this value to a W3C traceparent string
        /// (e.g. <c>"00-{trace_id}-{span_id}-{trace_flags}"</c>) to link
        /// <see cref="OutputScope"/> spans as children of an
        /// <see cref="InvokeAgentScope"/>.
        /// </summary>
        public const string A365ParentSpanKey = "A365ParentSpanId";

        /// <inheritdoc/>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            var agentDetails = DeriveAgentDetails(turnContext);
            var tenantDetails = DeriveTenantDetails(turnContext);

            if (agentDetails == null || tenantDetails == null)
            {
                await next(cancellationToken).ConfigureAwait(false);
                return;
            }

            var callerDetails = DeriveCallerDetails(turnContext);
            var conversationId = turnContext.Activity?.Conversation?.Id;
            var sourceMetadata = DeriveSourceMetadata(turnContext);
            var executionType = DeriveExecutionType(turnContext);

            turnContext.OnSendActivities(CreateSendHandler(
                turnContext,
                agentDetails,
                tenantDetails,
                callerDetails,
                conversationId,
                sourceMetadata,
                executionType));

            await next(cancellationToken).ConfigureAwait(false);
        }

        private static AgentDetails? DeriveAgentDetails(ITurnContext turnContext)
        {
            var recipient = turnContext.Activity?.Recipient;
            if (recipient == null)
            {
                return null;
            }

            // Gate on the recipient having an agentic identity
            var agentId = recipient.AgenticAppId ?? recipient.Id;
            if (string.IsNullOrEmpty(agentId))
            {
                return null;
            }

            return new AgentDetails(
                agentId: agentId,
                agentName: recipient.Name,
                agentAUID: recipient.AadObjectId,
                agentUPN: recipient.AgenticUserId,
                agentDescription: recipient.Role,
                tenantId: recipient.TenantId);
        }

        private static TenantDetails? DeriveTenantDetails(ITurnContext turnContext)
        {
            var tenantId = turnContext.Activity?.Recipient?.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out var tenantGuid))
            {
                return null;
            }

            return new TenantDetails(tenantGuid);
        }

        private static CallerDetails? DeriveCallerDetails(ITurnContext turnContext)
        {
            var from = turnContext.Activity?.From;
            if (from == null)
            {
                return null;
            }

            return new CallerDetails(
                callerId: from.Id ?? string.Empty,
                callerName: from.Name ?? string.Empty,
                callerUpn: from.AgenticUserId ?? string.Empty,
                tenantId: from.TenantId);
        }

        private static SourceMetadata? DeriveSourceMetadata(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity?.ChannelId;
            if (channelId == null)
            {
                return null;
            }

            return new SourceMetadata(
                name: channelId.Channel,
                description: channelId.SubChannel);
        }

        private static string? DeriveExecutionType(ITurnContext turnContext)
        {
            var pairs = turnContext.GetExecutionTypePair();
            foreach (var pair in pairs)
            {
                if (pair.Key == OpenTelemetryConstants.GenAiExecutionTypeKey)
                {
                    return pair.Value?.ToString();
                }
            }

            return null;
        }

        private static SendActivitiesHandler CreateSendHandler(
            ITurnContext turnContext,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            CallerDetails? callerDetails,
            string? conversationId,
            SourceMetadata? sourceMetadata,
            string? executionType)
        {
            return async (ctx, activities, nextSend) =>
            {
                var messages = new List<string>();
                foreach (var a in activities)
                {
                    if (string.Equals(a.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(a.Text))
                    {
                        messages.Add(a.Text);
                    }
                }

                if (messages.Count == 0)
                {
                    return await nextSend().ConfigureAwait(false);
                }

                // Read parent span lazily so the agent handler can set it during logic()
                string? parentId = null;
                if (turnContext.StackState.ContainsKey(A365ParentSpanKey))
                {
                    parentId = turnContext.StackState[A365ParentSpanKey]?.ToString();
                }

                var outputScope = OutputScope.Start(
                    agentDetails: agentDetails,
                    tenantDetails: tenantDetails,
                    response: new Response(messages),
                    parentId: parentId);

                try
                {
                    outputScope.SetTagMaybe(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);
                    outputScope.SetTagMaybe(OpenTelemetryConstants.GenAiExecutionTypeKey, executionType);

                    if (sourceMetadata != null)
                    {
                        outputScope.SetTagMaybe(OpenTelemetryConstants.GenAiChannelNameKey, sourceMetadata.Name);
                        outputScope.SetTagMaybe(OpenTelemetryConstants.GenAiChannelLinkKey, sourceMetadata.Description);
                    }

                    if (callerDetails != null)
                    {
                        outputScope.SetTagMaybe(OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId);
                        outputScope.SetTagMaybe(OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn);
                        outputScope.SetTagMaybe(OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName);
                        outputScope.SetTagMaybe(OpenTelemetryConstants.GenAiCallerTenantIdKey, callerDetails.TenantId);
                    }

                    return await nextSend().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    outputScope.RecordError(ex);
                    throw;
                }
                finally
                {
                    outputScope.Dispose();
                }
            };
        }
    }
}
