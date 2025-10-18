// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Ignore Spelling: Agentic

using Microsoft.Agents.A365.Notifications.Extensions;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.A365.Notifications;

namespace AgentNotification
{
    /// <summary>
    /// AgentsSdkExtension for Kairo.
    /// </summary>
    public class AgentNotification : AgentExtension
    {
        private static readonly string ExtensionChannelId = "agents";
        private readonly AgentApplication _app;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentNotification"/> class.
        /// </summary>
        /// <param name="app">The agent application to extend with notification functionality.</param>
        public AgentNotification(AgentApplication app)
        {
            AssertionHelpers.ThrowIfNull(app, nameof(app));
            _app = app;
            ChannelId = new ChannelId(ExtensionChannelId);
            ChannelId.SubChannel = "*";
        }


        /// <summary>
        /// Register a route handler for agent notifications from a specific sub-channel or all known subchannels for a given agent channel.
        /// </summary>
        /// <param name="subChannelId"></param>
        /// <param name="handler"></param>
        /// <param name="rank"></param>
        /// <param name="autoSignInHandlers"></param>
        public AgentNotification OnAgentNotification(string subChannelId, AgentNotificationHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null!)
        {
            RouteSelector routeSelector = (tc, ct) => 
                Task.FromResult(
                    IsChannelForMe(tc.Activity) && 
                    (subChannelId.Equals("*") || IsForKnownSubChannel(tc.Activity, subChannelId))
                );

            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                // Wrap the activity in an AgentNotificationActivity
                var agentNotificationActivity = new AgentNotificationActivity(turnContext.Activity);
                // for now, we will required the handler to return the proper result.. we will change this later to return a structured result and handle the response back. 
                await handler(turnContext, turnState, agentNotificationActivity, cancellationToken);
            };
            AddRoute(_app, routeSelector, routeHandler, false, rank, autoSignInHandlers);
            return this;
        }

        private bool IsChannelForMe(IActivity agentActivity)
        {
            return agentActivity.ChannelId != null 
                   && agentActivity.ChannelId.Channel != null
                   && agentActivity.ChannelId.Channel.Equals(ExtensionChannelId, StringComparison.OrdinalIgnoreCase);

        }

        private bool IsForKnownSubChannel(IActivity agentActivity, string subChannelId)
        {
            if (string.IsNullOrEmpty(subChannelId))
            {
                return false;
            }
            if (!IsValidSubChannel(subChannelId))
            {
                return false;
            }
            return agentActivity.ChannelId != null
                    && agentActivity.ChannelId.Channel != null
                    && agentActivity.ChannelId.SubChannel != null
                    && agentActivity.ChannelId.SubChannel.Equals(subChannelId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidSubChannel(string subChannel)
        {
            return subChannel switch
            {
                SubChannels.AgentsEmailSubChannel => true,
                SubChannels.AgentsExcelSubChannel => true,
                SubChannels.AgentsWordSubChannel => true,
                SubChannels.AgentsPowerPointSubChannel => true,
                SubChannels.FederatedKnowledgeServiceSubChannel => true,
                _ => false,
            };
        }
    }

    /// <summary>
    /// Extension methods for registering agent notification handlers on an AgentApplication.
    /// </summary>
    public static class AgentNotificationExtensions
    {
        /// <summary>
        /// Registers a handler for agent notifications from a specific channel.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="channelId">The channel identifier to listen for notifications.</param>
        /// <param name="routeHandler">The handler to invoke when a notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgentNotification(this AgentApplication app, ChannelId channelId, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnAgentNotification(channelId.ToString(), routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic email notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when an email notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticEmailNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnAgentNotification(SubChannels.AgentsEmailSubChannel, routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic Word notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a Word notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticWordNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnAgentNotification(SubChannels.AgentsWordSubChannel, routeHandler, rank, autoSignInHandlers);
            });
        
        /// <summary>
        /// Registers a handler for agentic Excel notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when an Excel notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticExcelNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnAgentNotification(SubChannels.AgentsExcelSubChannel, routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic PowerPoint notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a PowerPoint notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticPowerPointNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnAgentNotification(SubChannels.AgentsPowerPointSubChannel, routeHandler, rank, autoSignInHandlers);
            });
    }
}
