// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Links to a parent span when <see cref="A365ParentTraceparentKey"/> is set in
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
        /// trace context reference. Set this value to a W3C traceparent string
        /// (e.g. <c>"00-{trace_id}-{span_id}-{trace_flags}"</c>) to link
        /// <see cref="OutputScope"/> spans as children of an
        /// <see cref="InvokeAgentScope"/>.
        /// </summary>
        public const string A365ParentTraceparentKey = "A365ParentTraceparent";

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
            var channel = DeriveChannel(turnContext);

            turnContext.OnSendActivities(CreateSendHandler(
                turnContext,
                agentDetails,
                tenantDetails,
                callerDetails,
                conversationId,
                channel));

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

        private static Channel? DeriveChannel(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity?.ChannelId;
            if (channelId == null)
            {
                return null;
            }

            return new Channel(
                name: channelId.Channel,
                link: channelId.SubChannel);
        }

        private static SendActivitiesHandler CreateSendHandler(
            ITurnContext turnContext,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            CallerDetails? callerDetails,
            string? conversationId,
            Channel? channel)
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

                // Read parent traceparent lazily so the agent handler can set it during logic()
                ActivityContext? parentContext = null;
                if (turnContext.StackState.TryGetValue(A365ParentTraceparentKey, out var traceparentValue) && traceparentValue != null)
                {
                    var traceparent = traceparentValue.ToString();
                    if (!string.IsNullOrEmpty(traceparent))
                    {
                        parentContext = TraceContextHelper.ExtractContextFromHeaders(
                            new Dictionary<string, string> { { "traceparent", traceparent } });
                    }
                }

                var outputScope = OutputScope.Start(
                    agentDetails: agentDetails,
                    tenantDetails: tenantDetails,
                    response: new Response(messages),
                    conversationId: conversationId,
                    channel: channel,
                    callerDetails: callerDetails,
                    parentContext: parentContext);

                try
                {
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
