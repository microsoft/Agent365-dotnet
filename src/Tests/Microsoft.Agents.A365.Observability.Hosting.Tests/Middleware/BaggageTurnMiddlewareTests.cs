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
    public async Task OnTurnAsync_UserId_FallsBackToFromId_WhenAadObjectIdIsNull()
    {
        // Arrange — simulates email/Word/SPO channel where AadObjectId is null
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            channelName: "outlook",
            fromId: "lukemoenning@microsoft.com",
            fromAadObjectId: null);

        string? capturedUserId = null;

        NextDelegate next = (ct) =>
        {
            capturedUserId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedUserId.Should().Be("lukemoenning@microsoft.com");
    }

    [TestMethod]
    public async Task OnTurnAsync_UserId_FallsBackToAgenticUserId_WhenAadObjectIdIsNull()
    {
        // Arrange — simulates A2A call where AadObjectId is null but AgenticUserId is set
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            fromId: "29:1sH5NArUwkWAX",
            fromAadObjectId: null,
            fromAgenticUserId: "agent@contoso.onmicrosoft.com");

        string? capturedUserId = null;

        NextDelegate next = (ct) =>
        {
            capturedUserId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedUserId.Should().Be("agent@contoso.onmicrosoft.com");
    }

    [TestMethod]
    public async Task OnTurnAsync_UserId_PrefersAadObjectId_WhenBothAadAndAgenticUserIdSet()
    {
        // Arrange — both AadObjectId and AgenticUserId are populated; AadObjectId should win
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            channelName: "msteams",
            fromId: "8:orgid:17649762-cd35-4a35-95ab-75eeb3017308",
            fromAadObjectId: "aad-object-id-123",
            fromAgenticUserId: "agent@contoso.onmicrosoft.com");

        string? capturedUserId = null;

        NextDelegate next = (ct) =>
        {
            capturedUserId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedUserId.Should().Be("aad-object-id-123");
    }

    [TestMethod]
    public async Task OnTurnAsync_UserId_FallsBackToGuidAgenticUserId()
    {
        // Arrange — A2A where AgenticUserId is a GUID, not an email
        var middleware = new BaggageTurnMiddleware();
        var turnContext = CreateTurnContext(
            fromId: "29:1sH5NArUwkWAX",
            fromAadObjectId: null,
            fromAgenticUserId: "bef730f4-d6f5-4ffb-b759-26ffa449ed7e");

        string? capturedUserId = null;

        NextDelegate next = (ct) =>
        {
            capturedUserId = Baggage.Current.GetBaggage(OpenTelemetryConstants.UserIdKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedUserId.Should().Be("bef730f4-d6f5-4ffb-b759-26ffa449ed7e");
    }

    [TestMethod]
    public async Task OnTurnAsync_ExtractsProductContextFromChannelData()
    {
        // Arrange
        var middleware = new BaggageTurnMiddleware();
        
        // Set up ChannelData with productContext using a real JSON-backed object.
        var channelData = new System.Text.Json.Nodes.JsonObject
        {
            ["productContext"] = "COPILOT",
        };
        
        var turnContext = CreateTurnContext(channelData: channelData);

        string? capturedChannelLink = null;

        NextDelegate next = (ct) =>
        {
            capturedChannelLink = Baggage.Current.GetBaggage(OpenTelemetryConstants.ChannelLinkKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedChannelLink.Should().Be("COPILOT");
    }

    [TestMethod]
    public async Task OnTurnAsync_SubChannelTakesPrecedenceOverProductContext()
    {
        // Arrange
        var middleware = new BaggageTurnMiddleware();
        
        // Set up ChannelData with productContext (should be ignored)
        var channelDataJson = """{"productContext":"COPILOT"}""";
        var mockChannelData = new Mock<object>();
        mockChannelData.Setup(x => x.ToString()).Returns(channelDataJson);
        
        var turnContext = CreateTurnContext(
            subChannel: "teams-subchannel",
            channelData: mockChannelData.Object);

        string? capturedChannelLink = null;

        NextDelegate next = (ct) =>
        {
            capturedChannelLink = Baggage.Current.GetBaggage(OpenTelemetryConstants.ChannelLinkKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert - SubChannel should take precedence, productContext should be ignored
        capturedChannelLink.Should().Be("teams-subchannel");
    }

    [TestMethod]
    public async Task OnTurnAsync_ExtractsProductContextFromJsonStringChannelData()
    {
        // Arrange
        var middleware = new BaggageTurnMiddleware();
        
        // ChannelData as a JSON string (simulating deserialization from wire format)
        var channelDataJson = """{"productContext":"COPILOT"}""";
        
        var turnContext = CreateTurnContext(channelData: channelDataJson);

        string? capturedChannelLink = null;

        NextDelegate next = (ct) =>
        {
            capturedChannelLink = Baggage.Current.GetBaggage(OpenTelemetryConstants.ChannelLinkKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert
        capturedChannelLink.Should().Be("COPILOT");
    }

    [TestMethod]
    public async Task OnTurnAsync_HandlesInvalidJsonChannelDataGracefully()
    {
        // Arrange
        var middleware = new BaggageTurnMiddleware();
        
        var turnContext = CreateTurnContext(channelData: "not valid json");

        string? capturedChannelLink = null;

        NextDelegate next = (ct) =>
        {
            capturedChannelLink = Baggage.Current.GetBaggage(OpenTelemetryConstants.ChannelLinkKey);
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnTurnAsync(turnContext, next);

        // Assert - Should not set ChannelLink, should fail gracefully
        capturedChannelLink.Should().BeNull();
    }

    private static ITurnContext CreateTurnContext(
        string activityType = "message",
        string? activityName = null,
        string? fromId = "caller-id",
        string? fromAadObjectId = "caller-aad",
        string? fromAgenticUserId = null,
        string channelName = "msteams",
        string? subChannel = null,
        object? channelData = null)
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
        
        // Set up ChannelId with optional SubChannel
        var channelId = new ChannelId(channelName);
        if (subChannel != null)
        {
            channelId.SubChannel = subChannel;
        }
        mockActivity.Setup(a => a.ChannelId).Returns(channelId);
        
        // Set up ChannelData if provided
        if (channelData != null)
        {
            mockActivity.Setup(a => a.ChannelData).Returns(channelData);
        }

        var mockTurnContext = new Mock<ITurnContext>();
        mockTurnContext.Setup(tc => tc.Activity).Returns(mockActivity.Object);

        return mockTurnContext.Object;
    }
}
