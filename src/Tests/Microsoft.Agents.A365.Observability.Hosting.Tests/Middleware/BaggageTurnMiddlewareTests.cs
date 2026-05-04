// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Hosting.Middleware;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Moq;
using OpenTelemetry;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Middleware;

[TestClass]
public class BaggageTurnMiddlewareTests
{
    [TestMethod]
    public async Task OnTurnAsync_SetsOpenTelemetryBaggage()
    {
        // Arrange
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext();

        string? capturedTenantId = null;
        string? capturedCallerId = null;

        NextDelegate next = (ct) =>
        {
            capturedTenantId = Baggage.Current.GetBaggage(OpenTelemetryConstants.TenantIdKey);
            capturedCallerId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedTenantId.Should().Be("tenant-123");
        capturedCallerId.Should().Be("caller-aad");
    }

    [TestMethod]
    public async Task OnTurnAsync_SkipsBaggageForContinueConversation()
    {
        // Arrange
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            activityType: ActivityTypes.Event,
            activityName: ActivityEventNames.ContinueConversation);

        bool logicCalled = false;
        string? capturedCallerId = null;

        NextDelegate next = (ct) =>
        {
            logicCalled = true;
            capturedCallerId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        logicCalled.Should().BeTrue();
        // Baggage should NOT be set because the middleware skipped it
        capturedCallerId.Should().BeNull();
    }

    [TestMethod]
    public async Task OnTurnAsync_CallsNextDelegate()
    {
        // Arrange
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext();

        bool nextCalled = false;
        NextDelegate next = (ct) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [TestMethod]
    public async Task OnTurnAsync_RestoresBaggageAfterNext()
    {
        // Arrange
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext();

        string? baggageBeforeMiddleware = Baggage.Current.GetBaggage(OpenTelemetryConstants.TenantIdKey);

        NextDelegate next = (ct) => Task.CompletedTask;

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert – the baggage scope should be disposed after OnTurnAsync returns
        string? baggageAfterMiddleware = Baggage.Current.GetBaggage(OpenTelemetryConstants.TenantIdKey);
        baggageAfterMiddleware.Should().Be(baggageBeforeMiddleware);
    }

    [TestMethod]
    public async Task OnTurnAsync_SetsUserIdAndEmail_WhenEmailChannel()
    {
        // Arrange — simulates email channel where AadObjectId is null and SubChannel is set
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            fromId: "lukemoenning@microsoft.com",
            fromAadObjectId: null,
            subChannel: "email");

        string? capturedCallerId = null;
        string? capturedUserEmail = null;

        NextDelegate next = (ct) =>
        {
            capturedCallerId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            capturedUserEmail = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserEmailKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedCallerId.Should().Be("lukemoenning@microsoft.com");
        capturedUserEmail.Should().Be("lukemoenning@microsoft.com");
    }

    [TestMethod]
    public async Task OnTurnAsync_DoesNotSetUserEmail_WhenTeamsChannel()
    {
        // Arrange — simulates Teams channel (no SubChannel)
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            fromId: "8:orgid:17649762-cd35-4a35-95ab-75eeb3017308");

        string? capturedCallerId = null;
        string? capturedUserEmail = null;

        NextDelegate next = (ct) =>
        {
            capturedCallerId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            capturedUserEmail = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserEmailKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedCallerId.Should().Be("caller-aad");
        capturedUserEmail.Should().BeNull();
    }

    [TestMethod]
    public async Task OnTurnAsync_SetsUserEmailFromAgenticUserId_WhenA2AWithEmail()
    {
        // Arrange — simulates A2A where the calling agent has an email-based agenticUserId
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            fromId: "29:1sH5NArUwkWAX",
            fromAadObjectId: null,
            fromAgenticUserId: "agent@contoso.onmicrosoft.com");

        string? capturedCallerId = null;
        string? capturedUserEmail = null;

        NextDelegate next = (ct) =>
        {
            capturedCallerId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            capturedUserEmail = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserEmailKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedCallerId.Should().Be("agent@contoso.onmicrosoft.com");
        capturedUserEmail.Should().Be("agent@contoso.onmicrosoft.com");
    }

    [TestMethod]
    public async Task OnTurnAsync_DoesNotSetUserEmail_WhenA2AWithGuidAgenticUserId()
    {
        // Arrange — simulates A2A where agenticUserId is a GUID, not an email
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            fromId: "29:1sH5NArUwkWAX",
            fromAadObjectId: null,
            fromAgenticUserId: "bef730f4-d6f5-4ffb-b759-26ffa449ed7e");

        string? capturedCallerId = null;
        string? capturedUserEmail = null;

        NextDelegate next = (ct) =>
        {
            capturedCallerId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            capturedUserEmail = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserEmailKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedCallerId.Should().Be("bef730f4-d6f5-4ffb-b759-26ffa449ed7e");
        capturedUserEmail.Should().BeNull();
    }

    private static ITurnContext CreateTurnContext(
        string activityType = "message",
        string? activityName = null,
        string? fromId = "caller-id",
        string? fromAadObjectId = "caller-aad",
        string? fromAgenticUserId = null,
        string? subChannel = null)
    {
        var mockActivity = new Mock<IActivity>();
        mockActivity.Setup(a => a.Type).Returns(activityType);
        if (activityName != null)
        {
            mockActivity.Setup(a => a.Name).Returns(activityName);
        }
        mockActivity.Setup(a => a.Text).Returns("Hello");
        mockActivity.Setup(a => a.From).Returns(new ChannelAccount
        {
            Id = fromId,
            Name = "Caller",
            AadObjectId = fromAadObjectId,
            AgenticUserId = fromAgenticUserId,
        });
        mockActivity.Setup(a => a.Recipient).Returns(new ChannelAccount
        {
            Id = "agent-id",
            Name = "Agent",
            TenantId = "tenant-123",
            Role = "user",
        });
        mockActivity.Setup(a => a.Conversation).Returns(new ConversationAccount { Id = "conv-id" });
        mockActivity.Setup(a => a.ServiceUrl).Returns("https://example.com");
        mockActivity.Setup(a => a.ChannelId).Returns(new ChannelId("test-channel") { SubChannel = subChannel });

        var mockTurnContext = new Mock<ITurnContext>();
        mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);

        return mockTurnContext.Object;
    }
}
