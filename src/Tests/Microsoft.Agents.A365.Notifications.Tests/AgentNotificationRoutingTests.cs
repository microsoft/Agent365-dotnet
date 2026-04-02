// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AgentNotification;
using FluentAssertions;
using Microsoft.Agents.A365.Notifications;
using Microsoft.Agents.A365.Notifications.Extensions;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;

namespace Microsoft.Agents.A365.Notifications.Tests;

/// <summary>
/// Integration tests that verify A365 notification routes are dispatched correctly
/// when competing with application-level agentic message handlers.
/// This is the scenario broken by SDK 1.5.x route ordering changes and fixed by
/// passing isAgenticOnly: true in AgentNotification.AddRoute.
/// </summary>
[TestClass]
public class AgentNotificationRoutingTests
{
    /// <summary>
    /// Reproduces the real-world pattern: OnAgentNotification registered before OnActivity(Message, isAgenticOnly: true).
    /// An agentic email notification (type=message, channelId=agents, recipient.role=agenticUser)
    /// must be routed to the notification handler, NOT to the generic message handler.
    /// </summary>
    [TestMethod]
    public async Task AgenticEmailNotification_RoutesToNotificationHandler_NotGenericMessageHandler()
    {
        // Arrange
        string handlerCalled = null!;
        var app = CreateTestApp();

        // Register notification handler first (mimics real app constructor order)
        app.OnAgenticEmailNotification(
            (turnContext, turnState, notification, ct) =>
            {
                handlerCalled = "notification";
                return Task.CompletedTask;
            },
            RouteRank.Last);

        // Register generic agentic message handler after (mimics real app)
        app.OnActivity(ActivityTypes.Message,
            (turnContext, turnState, ct) =>
            {
                handlerCalled = "genericMessage";
                return Task.CompletedTask;
            },
            RouteRank.Last,
            isAgenticOnly: true);

        using var turnContext = CreateAgenticNotificationTurnContext(SubChannels.AgentsEmailSubChannel);

        // Act
        await app.OnTurnAsync(turnContext, CancellationToken.None);

        // Assert
        handlerCalled.Should().Be("notification",
            "agentic email notification should be routed to the notification handler, not the generic message handler");
    }

    /// <summary>
    /// Same scenario but with the wildcard (*) OnAgentNotification handler,
    /// using the string overload as real apps do: this.OnAgentNotification("*", handler).
    /// </summary>
    [TestMethod]
    public async Task AgenticNotification_WithWildcardHandler_RoutesToNotificationHandler()
    {
        // Arrange
        string handlerCalled = null!;
        var app = CreateTestApp();

        // Use string "*" just like real app code: this.OnAgentNotification("*", HandleNotificationAsync, ...)
        app.OnAgentNotification(
            "*",
            (turnContext, turnState, notification, ct) =>
            {
                handlerCalled = "notification";
                return Task.CompletedTask;
            },
            RouteRank.Last);

        app.OnActivity(ActivityTypes.Message,
            (turnContext, turnState, ct) =>
            {
                handlerCalled = "genericMessage";
                return Task.CompletedTask;
            },
            RouteRank.Last,
            isAgenticOnly: true);

        using var turnContext = CreateAgenticNotificationTurnContext(SubChannels.AgentsWordSubChannel);

        // Act
        await app.OnTurnAsync(turnContext, CancellationToken.None);

        // Assert
        handlerCalled.Should().Be("notification",
            "agentic notification with wildcard handler should be routed to the notification handler");
    }

    /// <summary>
    /// Lifecycle notification (type=event) should route to OnLifecycleNotification, not a generic event handler.
    /// </summary>
    [TestMethod]
    public async Task LifecycleNotification_RoutesToLifecycleHandler_NotGenericEventHandler()
    {
        // Arrange
        string handlerCalled = null!;
        var app = CreateTestApp();

        app.OnLifecycleNotification(
            (turnContext, turnState, notification, ct) =>
            {
                handlerCalled = "lifecycle";
                return Task.CompletedTask;
            },
            RouteRank.Last);

        app.OnActivity(ActivityTypes.Event,
            (turnContext, turnState, ct) =>
            {
                handlerCalled = "genericEvent";
                return Task.CompletedTask;
            },
            RouteRank.Last,
            isAgenticOnly: true);

        using var turnContext = CreateAgenticLifecycleNotificationTurnContext(Events.AgenticUserIdentityCreated);

        // Act
        await app.OnTurnAsync(turnContext, CancellationToken.None);

        // Assert
        handlerCalled.Should().Be("lifecycle",
            "agentic lifecycle notification should be routed to the lifecycle handler, not the generic event handler");
    }

    /// <summary>
    /// A non-agentic (no recipient.role=agenticUser) regular message should NOT be routed
    /// to the notification handler — it should go to the non-agentic message handler.
    /// </summary>
    [TestMethod]
    public async Task NonAgenticMessage_RoutesToNonAgenticHandler()
    {
        // Arrange
        string handlerCalled = null!;
        var app = CreateTestApp();

        app.OnAgenticEmailNotification(
            (turnContext, turnState, notification, ct) =>
            {
                handlerCalled = "notification";
                return Task.CompletedTask;
            },
            RouteRank.Last);

        app.OnActivity(ActivityTypes.Message,
            (turnContext, turnState, ct) =>
            {
                handlerCalled = "agenticMessage";
                return Task.CompletedTask;
            },
            RouteRank.Last,
            isAgenticOnly: true);

        app.OnActivity(ActivityTypes.Message,
            (turnContext, turnState, ct) =>
            {
                handlerCalled = "nonAgenticMessage";
                return Task.CompletedTask;
            },
            RouteRank.Last,
            isAgenticOnly: false);

        // Create a normal (non-agentic) message — no agents channel, no agenticUser role
        using var turnContext = CreateNonAgenticMessageTurnContext();

        // Act
        await app.OnTurnAsync(turnContext, CancellationToken.None);

        // Assert
        handlerCalled.Should().Be("nonAgenticMessage",
            "non-agentic message should be routed to the non-agentic handler");
    }

    /// <summary>
    /// An agentic message from a non-agents channel (e.g. teams) should NOT match
    /// the notification handler — it should fall through to the generic agentic message handler.
    /// </summary>
    [TestMethod]
    public async Task AgenticMessage_NonAgentsChannel_RoutesToGenericAgenticHandler()
    {
        // Arrange
        string handlerCalled = null!;
        var app = CreateTestApp();

        app.OnAgenticEmailNotification(
            (turnContext, turnState, notification, ct) =>
            {
                handlerCalled = "notification";
                return Task.CompletedTask;
            },
            RouteRank.Last);

        app.OnActivity(ActivityTypes.Message,
            (turnContext, turnState, ct) =>
            {
                handlerCalled = "agenticMessage";
                return Task.CompletedTask;
            },
            RouteRank.Last,
            isAgenticOnly: true);

        // Agentic message, but from "msteams" channel — not an A365 notification
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "Hello from Teams",
            ChannelId = new ChannelId("msteams"),
            Recipient = new ChannelAccount { Id = "bot1", Role = RoleTypes.AgenticUser },
            From = new ChannelAccount { Id = "user1" },
            Conversation = new ConversationAccount { Id = "conv1" },
        };
        using var turnContext = new TurnContext(new StubAdapter(), activity);

        // Act
        await app.OnTurnAsync(turnContext, CancellationToken.None);

        // Assert
        handlerCalled.Should().Be("agenticMessage",
            "agentic message from a non-agents channel should go to the generic agentic handler, not the notification handler");
    }

    /// <summary>
    /// Test all subchannel-specific extension methods route correctly.
    /// Uses the dedicated OnAgentic*Notification helpers, mirroring real app code.
    /// </summary>
    [TestMethod]
    public async Task AgenticNotification_EmailSubChannel_RoutesToCorrectHandler()
    {
        await AssertSubChannelRoutesToNotificationHandler(
            (app, handler) => app.OnAgenticEmailNotification(handler, RouteRank.Last),
            SubChannels.AgentsEmailSubChannel);
    }

    [TestMethod]
    public async Task AgenticNotification_WordSubChannel_RoutesToCorrectHandler()
    {
        await AssertSubChannelRoutesToNotificationHandler(
            (app, handler) => app.OnAgenticWordNotification(handler, RouteRank.Last),
            SubChannels.AgentsWordSubChannel);
    }

    [TestMethod]
    public async Task AgenticNotification_ExcelSubChannel_RoutesToCorrectHandler()
    {
        await AssertSubChannelRoutesToNotificationHandler(
            (app, handler) => app.OnAgenticExcelNotification(handler, RouteRank.Last),
            SubChannels.AgentsExcelSubChannel);
    }

    [TestMethod]
    public async Task AgenticNotification_PowerPointSubChannel_RoutesToCorrectHandler()
    {
        await AssertSubChannelRoutesToNotificationHandler(
            (app, handler) => app.OnAgenticPowerPointNotification(handler, RouteRank.Last),
            SubChannels.AgentsPowerPointSubChannel);
    }

    private static async Task AssertSubChannelRoutesToNotificationHandler(
        Action<AgentApplication, AgentNotificationHandler> registerNotification,
        string subChannel)
    {
        // Arrange
        string handlerCalled = null!;
        var app = CreateTestApp();

        AgentNotificationHandler notificationHandler = (turnContext, turnState, notification, ct) =>
        {
            handlerCalled = "notification";
            return Task.CompletedTask;
        };

        registerNotification(app, notificationHandler);

        // Competing generic agentic message handler
        app.OnActivity(ActivityTypes.Message,
            (turnContext, turnState, ct) =>
            {
                handlerCalled = "genericMessage";
                return Task.CompletedTask;
            },
            RouteRank.Last,
            isAgenticOnly: true);

        using var turnContext = CreateAgenticNotificationTurnContext(subChannel);

        // Act
        await app.OnTurnAsync(turnContext, CancellationToken.None);

        // Assert
        handlerCalled.Should().Be("notification",
            $"notification on {subChannel} subchannel should route to its handler, not the generic message handler");
    }

    #region Test helpers

    private static AgentApplication CreateTestApp()
    {
        var options = new AgentApplicationOptions((IStorage)null!)
        {
            RemoveRecipientMention = false,
            StartTypingTimer = false,
        };
        return new TestAgentApplication(options);
    }

    private static TurnContext CreateAgenticNotificationTurnContext(string subChannel)
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "Notification content",
            ChannelId = new ChannelId("agents") { SubChannel = subChannel },
            Recipient = new ChannelAccount { Id = "bot1", Role = RoleTypes.AgenticUser },
            From = new ChannelAccount { Id = "user1" },
            Conversation = new ConversationAccount { Id = "conv1" },
        };
        return new TurnContext(new StubAdapter(), activity);
    }

    private static TurnContext CreateAgenticLifecycleNotificationTurnContext(string lifecycleEvent)
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Event,
            Name = Events.AgentLifecycleEvent,
            ValueType = lifecycleEvent,
            ChannelId = new ChannelId("agents"),
            Recipient = new ChannelAccount { Id = "bot1", Role = RoleTypes.AgenticUser },
            From = new ChannelAccount { Id = "user1" },
            Conversation = new ConversationAccount { Id = "conv1" },
        };
        return new TurnContext(new StubAdapter(), activity);
    }

    private static TurnContext CreateNonAgenticMessageTurnContext()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "Hello",
            ChannelId = new ChannelId("directline"),
            Recipient = new ChannelAccount { Id = "bot1" },
            From = new ChannelAccount { Id = "user1" },
            Conversation = new ConversationAccount { Id = "conv1" },
        };
        return new TurnContext(new StubAdapter(), activity);
    }

    private class TestAgentApplication : AgentApplication
    {
        public TestAgentApplication(AgentApplicationOptions options) : base(options) { }
    }

    private class StubAdapter : ChannelAdapter
    {
        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            return Task.FromResult(Array.Empty<ResourceResponse>());
        }
    }

    #endregion
}
