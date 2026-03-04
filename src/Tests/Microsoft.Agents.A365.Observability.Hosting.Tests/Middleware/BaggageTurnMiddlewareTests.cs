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
            capturedCallerId = Baggage.Current.GetBaggage(OpenTelemetryConstants.GenAiCallerIdKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedTenantId.Should().Be("tenant-123");
        capturedCallerId.Should().Be("caller-id");
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
            capturedCallerId = Baggage.Current.GetBaggage(OpenTelemetryConstants.GenAiCallerIdKey);
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

    private static ITurnContext CreateTurnContext(
        string activityType = "message",
        string? activityName = null)
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
            Id = "caller-id",
            Name = "Caller",
            AadObjectId = "caller-aad",
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
        mockActivity.Setup(a => a.ChannelId).Returns(new ChannelId("test-channel"));

        var mockTurnContext = new Mock<ITurnContext>();
        mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);

        return mockTurnContext.Object;
    }
}
