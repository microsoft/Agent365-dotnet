// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Observability.Hosting.Extensions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.A365.Observability.Hosting.Middleware
{
    /// <summary>
    /// Middleware for logging incoming and outgoing activities to the Microsoft Agent 365 observability infrastructure.
    /// Creates InputScope and OutputScope spans for incoming and outgoing activities respectively.
    /// </summary>
    public class ObservabilityMiddleware : IMiddleware
    {
        private readonly Func<ITurnContext, AgentDetails>? _agentDetailsResolver;
        private readonly Func<ITurnContext, CallerDetails?>? _callerDetailsResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservabilityMiddleware"/> class.
        /// </summary>
        public ObservabilityMiddleware()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservabilityMiddleware"/> class with custom resolvers.
        /// </summary>
        /// <param name="agentDetailsResolver">Optional resolver to extract agent details from the turn context.</param>
        /// <param name="callerDetailsResolver">Optional resolver to extract caller details from the turn context.</param>
        public ObservabilityMiddleware(
            Func<ITurnContext, AgentDetails>? agentDetailsResolver = null,
            Func<ITurnContext, CallerDetails?>? callerDetailsResolver = null)
        {
            _agentDetailsResolver = agentDetailsResolver;
            _callerDetailsResolver = callerDetailsResolver;
        }

        /// <summary>
        /// Processes an incoming activity, creating observability spans for input and output.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="next">The delegate to call to continue the Agent middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            var outputActivities = new List<IActivity>();

            // Extract details from turn context
            var agentDetails = ResolveAgentDetails(turnContext);
            var tenantDetails = ResolveTenantDetails(turnContext);
            var callerDetails = ResolveCallerDetails(turnContext);
            var request = ResolveRequest(turnContext);
            var conversationId = turnContext.Activity?.Conversation?.Id;
            var sessionId = turnContext.Activity?.Conversation?.Id;
            var sourceMetadata = ResolveSourceMetadata(turnContext);
            var executionType = ResolveExecutionType(turnContext);

            // Set baggage context from turn context for downstream operations
            using (new BaggageBuilder().FromTurnContext(turnContext).Build())
            {
                // Create InputScope for incoming activity
                using (var inputScope = CreateInputScope(turnContext, agentDetails, tenantDetails, request, callerDetails, conversationId, sessionId))
                {
                    // Inject observability context into turn context for downstream use
                    if (inputScope != null)
                    {
                        turnContext.InjectObservabilityContext(inputScope);
                    }

                    // Hook up OnSendActivities to capture outgoing activities
                    turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
                    {
                        // Run the full pipeline first
                        var responses = await nextSend().ConfigureAwait(false);

                        // Collect sent activities for output scope
                        foreach (var activity in activities)
                        {
                            if (activity != null)
                            {
                                outputActivities.Add(CloneActivity(activity));
                            }
                        }

                        return responses;
                    });

                    // Process Agent logic
                    await next(cancellationToken).ConfigureAwait(false);
                }
            }

            // Create OutputScope for outgoing activities after the turn completes
            if (outputActivities.Count > 0)
            {
                var outputMessages = ExtractOutputMessages(outputActivities);
                var response = outputMessages.Length > 0 ? new Response(string.Join(",", outputMessages)) : null;

                using (var outputScope = OutputScope.Start(
                    agentDetails: agentDetails,
                    tenantDetails: tenantDetails,
                    response: response,
                    callerDetails: callerDetails,
                    conversationId: conversationId,
                    sessionId: sessionId,
                    sourceMetadata: sourceMetadata,
                    executionType: executionType))
                {
                    outputScope.RecordOutputMessages(outputMessages);
                }
            }
        }

        private InputScope? CreateInputScope(
            ITurnContext turnContext,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Request? request,
            CallerDetails? callerDetails,
            string? conversationId,
            string? sessionId)
        {
            // Skip creating scope for ContinueConversation events (used to initialize middleware)
            if (turnContext.Activity?.Type == ActivityTypes.Event &&
                turnContext.Activity?.Name == ActivityEventNames.ContinueConversation)
            {
                return null;
            }

            var inputScope = InputScope.Start(
                agentDetails: agentDetails,
                tenantDetails: tenantDetails,
                request: request,
                callerDetails: callerDetails,
                conversationId: conversationId,
                sessionId: sessionId);

            // Record input message if present
            var activityText = turnContext.Activity?.Text;
            if (!string.IsNullOrEmpty(activityText))
            {
                inputScope.RecordInputMessages(new[] { activityText! });
            }

            return inputScope;
        }

        private AgentDetails ResolveAgentDetails(ITurnContext turnContext)
        {
            if (_agentDetailsResolver != null)
            {
                return _agentDetailsResolver(turnContext);
            }

            // Extract from turn context
            var activity = turnContext.Activity;
            return new AgentDetails(
                agentId: activity?.Recipient?.AgenticAppId ?? activity?.Recipient?.Id,
                agentName: activity?.Recipient?.Name,
                agentAUID: activity?.Recipient?.AgenticUserId ?? activity?.Recipient?.AadObjectId,
                agentUPN: activity?.Recipient?.Name,
                tenantId: activity?.Recipient?.TenantId);
        }

        private TenantDetails ResolveTenantDetails(ITurnContext turnContext)
        {
            var tenantId = turnContext.Activity?.Recipient?.TenantId;

            // Try to extract from ChannelData if not available on recipient
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
                    // Ignore parsing errors
                }
            }

            return Guid.TryParse(tenantId, out var guid) ? new TenantDetails(guid) : new TenantDetails(Guid.Empty);
        }

        private CallerDetails? ResolveCallerDetails(ITurnContext turnContext)
        {
            if (_callerDetailsResolver != null)
            {
                return _callerDetailsResolver(turnContext);
            }

            var from = turnContext.Activity?.From;
            if (from == null || string.IsNullOrEmpty(from.Id))
            {
                return null;
            }

            return new CallerDetails(
                callerId: from.Id,
                callerName: from.Name ?? string.Empty,
                callerUpn: from.Name ?? string.Empty,
                tenantId: from.TenantId);
        }

        private Request? ResolveRequest(ITurnContext turnContext)
        {
            var content = turnContext.Activity?.Text;
            if (string.IsNullOrEmpty(content))
            {
                return null;
            }

            return new Request(
                content: content!,
                executionType: ResolveExecutionType(turnContext),
                sourceMetadata: ResolveSourceMetadata(turnContext));
        }

        private SourceMetadata? ResolveSourceMetadata(ITurnContext turnContext)
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

        private ExecutionType ResolveExecutionType(ITurnContext turnContext)
        {
            const string AgentRole = "agenticUser";

            var isAgenticCaller = turnContext.Activity?.From?.AgenticUserId != null
                || (turnContext.Activity?.From?.Role != null &&
                    turnContext.Activity.From.Role.Equals(AgentRole, StringComparison.OrdinalIgnoreCase));

            var isAgenticRecipient = turnContext.Activity?.Recipient?.AgenticUserId != null
                || (turnContext.Activity?.Recipient?.Role != null &&
                    turnContext.Activity.Recipient.Role.Equals(AgentRole, StringComparison.OrdinalIgnoreCase));

            return isAgenticRecipient && isAgenticCaller
                ? ExecutionType.Agent2Agent
                : ExecutionType.HumanToAgent;
        }

        private static IActivity CloneActivity(IActivity activity)
        {
            var cloned = activity.Clone();
            EnsureActivityHasId(cloned);
            return cloned;
        }

        private static void EnsureActivityHasId(IActivity activity)
        {
            if (activity != null && string.IsNullOrEmpty(activity.Id))
            {
                activity.Id = $"g_{Guid.NewGuid()}";
            }
        }

        private static string[] ExtractOutputMessages(List<IActivity> activities)
        {
            var messages = new List<string>();
            foreach (var activity in activities)
            {
                var text = activity?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    messages.Add(text!);
                }
            }
            return messages.ToArray();
        }
    }
}
