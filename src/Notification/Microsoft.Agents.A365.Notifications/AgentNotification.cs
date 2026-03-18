// Copyright (c) Microsoft Corporation.
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
    /// AgentsSdkExtension for Microsoft Agent 365.
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

        /// <summary>
        /// Register a route handler for agent lifecycle notifications.
        /// </summary>
        /// <param name="lifecycleEvent"></param>
        /// <param name="handler"></param>
        /// <param name="rank"></param>
        /// <param name="autoSignInHandlers"></param>
        public AgentNotification OnLifecycleNotification(string lifecycleEvent, AgentNotificationHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null!)
        {
            RouteSelector routeSelector = (tc, ct) =>
                Task.FromResult(
                    IsChannelForMe(tc.Activity) &&
                    IsLifecycleEvent(tc.Activity) &&
                    (lifecycleEvent.Equals("*") || IsForKnownLifecycleEvent(tc.Activity, lifecycleEvent))
                );

            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                // Wrap the activity in an AgentNotificationActivity
                var agentNotificationActivity = new AgentNotificationActivity(turnContext.Activity);
                await handler(turnContext, turnState, agentNotificationActivity, cancellationToken);
            };
            AddRoute(_app, routeSelector, routeHandler, false, rank, autoSignInHandlers);
            return this;
        }

        private bool IsLifecycleEvent(IActivity agentActivity)
        {
            if (agentActivity.Type != ActivityTypes.Event)
            {
                return false;
            }
            if (string.IsNullOrEmpty(agentActivity.Name))
            {
                return false;
            }

            return agentActivity.Name.Equals(Events.AgentLifecycleEvent, StringComparison.OrdinalIgnoreCase);
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

        private bool IsForKnownLifecycleEvent(IActivity agentActivity, string lifecycleEvent)
        {
            if (string.IsNullOrEmpty(lifecycleEvent))
            {
                return false;
            }
            if (!IsValidLifecycleEvent(lifecycleEvent))
            {
                return false;
            }
            return agentActivity.ValueType != null
                    && agentActivity.ValueType.Equals(lifecycleEvent, StringComparison.OrdinalIgnoreCase);
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

        private static bool IsValidLifecycleEvent(string lifecycleEvent)
        {
            return lifecycleEvent switch
            {
                Events.AgenticUserIdentityCreated => true,
                Events.AgenticUserWorkloadOnboardingUpdated => true,
                Events.AgenticUserDeleted => true,
                Events.AgenticUserUndeleted => true,
                Events.AgenticUserIdentityUpdated => true,
                Events.AgenticUserManagerUpdated => true,
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

        /// <summary>
        /// Registers a handler for all agent lifecycle notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnLifecycleNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnLifecycleNotification("*", routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic user creation lifecycle notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticUserIdentityCreatedNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnLifecycleNotification(Events.AgenticUserIdentityCreated, routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic user workload onboarding lifecycle notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticUserWorkloadOnboardingNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnLifecycleNotification(Events.AgenticUserWorkloadOnboardingUpdated, routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic user deleted lifecycle notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticUserDeletedNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnLifecycleNotification(Events.AgenticUserDeleted, routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic user un-deleted lifecycle notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticUserUndeletedNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnLifecycleNotification(Events.AgenticUserUndeleted, routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic user identity updated lifecycle notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticUserIdentityUpdatedNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnLifecycleNotification(Events.AgenticUserIdentityUpdated, routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Registers a handler for agentic user manager updated lifecycle notifications.
        /// </summary>
        /// <param name="app">The agent application to extend.</param>
        /// <param name="routeHandler">The handler to invoke when a notification is received.</param>
        /// <param name="rank">The route priority rank (default is 32767).</param>
        /// <param name="autoSignInHandlers">Optional array of auto sign-in handlers.</param>
        public static void OnAgenticUserManagerUpdatedNotification(this AgentApplication app, AgentNotificationHandler routeHandler, ushort rank = 32767, string[] autoSignInHandlers = null!) =>
            app.RegisterExtension(new AgentNotification(app), a365 =>
            {
                a365.OnLifecycleNotification(Events.AgenticUserManagerUpdated, routeHandler, rank, autoSignInHandlers);
            });

        /// <summary>
        /// Creates a reply Activity containing an <see cref="EmailResponse"/> entity populated with the provided HTML body.
        /// </summary>
        /// <param name="activity">The source Activity this reply is based on. Routing/conversation metadata is copied via <see cref="IActivity.CreateReply(string, string)"/>.</param>
        /// <param name="emailResponseHtmlBody">The HTML body content to include in the <see cref="EmailResponse"/> entity.</param>
        /// <returns>
        /// A new <see cref="IActivity"/> reply whose <c>Entities</c> collection includes an <see cref="EmailResponse"/> carrying the supplied HTML body.
        /// </returns>
        /// <remarks>
        /// This helper wraps two operations:
        /// 1. Calls <see cref="IActivity.CreateReply(string, string)"/> to initialize a response Activity with proper conversation context.
        /// 2. Adds a newly constructed <see cref="EmailResponse"/> (with the provided HTML body) to the reply's <c>Entities</c>.
        /// The method does not perform HTML validation or sanitization; callers should ensure the HTML body is safe for downstream rendering.
        /// </remarks>
        public static IActivity CreateEmailResponseActivity(this IActivity activity, string emailResponseHtmlBody)
        {
            var workingActivity = activity.CreateReply();
            var emailResponse = new EmailResponse(emailResponseHtmlBody);
            workingActivity.Entities ??= new List<Entity>();
            workingActivity.Entities.Add(emailResponse);
            return workingActivity;
        }
    }
}
