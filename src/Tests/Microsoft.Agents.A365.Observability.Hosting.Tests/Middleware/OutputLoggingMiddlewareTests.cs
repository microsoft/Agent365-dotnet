// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Hosting.Middleware;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Moq;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Middleware;

[TestClass]
public class OutputLoggingMiddlewareTests
{
    [TestMethod]
    public async Task OnTurnAsync_CallsNextDelegate()
    {
        // Arrange
        var middleware = new OutputLoggingMiddleware();
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
    public async Task OnTurnAsync_RegistersSendHandler_WhenRecipientHasDetails()
    {
        // Arrange
        var middleware = new OutputLoggingMiddleware();
        var mockTurnContext = new Mock<ITurnContext>();
        SetupTurnContext(mockTurnContext);

        NextDelegate next = (ct) => Task.CompletedTask;

        // Act
        await middleware.OnTurnAsync(mockTurnContext.Object, next);

        // Assert
        mockTurnContext.Verify(tc => tc.OnSendActivities(It.IsAny<SendActivitiesHandler>()), Times.Once);
    }

    [TestMethod]
    public async Task OnTurnAsync_PassesThrough_WhenRecipientIsNull()
    {
        // Arrange
        var middleware = new OutputLoggingMiddleware();
        var mockActivity = new Mock<IActivity>();
        mockActivity.Setup(a => a.Recipient).Returns((ChannelAccount)null!);
        mockActivity.Setup(a => a.Type).Returns("message");

        var mockTurnContext = new Mock<ITurnContext>();
        mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);

        bool nextCalled = false;
        NextDelegate next = (ct) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(mockTurnContext.Object, next);

        // Assert
        nextCalled.Should().BeTrue();
        mockTurnContext.Verify(tc => tc.OnSendActivities(It.IsAny<SendActivitiesHandler>()), Times.Never);
    }

    [TestMethod]
    public async Task OnTurnAsync_RegistersHandler_WhenTenantIdIsMissing()
    {
        // Arrange
        var middleware = new OutputLoggingMiddleware();
        var mockActivity = new Mock<IActivity>();
        mockActivity.Setup(a => a.Type).Returns("message");
        mockActivity.Setup(a => a.Recipient).Returns(new ChannelAccount
        {
            Id = "agent-id",
            Name = "Agent",
            // No TenantId set - middleware no longer gates on TenantId
        });

        var mockTurnContext = new Mock<ITurnContext>();
        mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);

        bool nextCalled = false;
        NextDelegate next = (ct) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(mockTurnContext.Object, next);

        // Assert
        nextCalled.Should().BeTrue();
        mockTurnContext.Verify(tc => tc.OnSendActivities(It.IsAny<SendActivitiesHandler>()), Times.Once);
    }

    private static ITurnContext CreateTurnContext()
    {
        var mockTurnContext = new Mock<ITurnContext>();
        SetupTurnContext(mockTurnContext);
        return mockTurnContext.Object;
    }

    private static void SetupTurnContext(Mock<ITurnContext> mockTurnContext)
    {
        var mockActivity = new Mock<IActivity>();
        mockActivity.Setup(a => a.Type).Returns("message");
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
            TenantId = "badf1f56-284d-4dc5-ac59-0dd53900e743",
            Role = "agenticAppInstance",
        });
        mockActivity.Setup(a => a.Conversation).Returns(new ConversationAccount { Id = "conv-id" });
        mockActivity.Setup(a => a.ServiceUrl).Returns("https://example.com");
        mockActivity.Setup(a => a.ChannelId).Returns(new ChannelId("test-channel"));

        mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);
        mockTurnContext.Setup(tc => tc.StackState).Returns(new TurnContextStateCollection());
        mockTurnContext.Setup(tc => tc.OnSendActivities(It.IsAny<SendActivitiesHandler>()))
            .Returns(mockTurnContext.Object);
    }
}
