// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Handlers;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Tests.Handlers
{
    /// <summary>
    /// Unit tests for HttpContextHeadersHandler message ID header functionality.
    /// </summary>
    public class HttpContextHeadersHandlerTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly ToolOptions _toolOptions;

        public HttpContextHeadersHandlerTests()
        {
            _loggerMock = new Mock<ILogger>();
            _toolOptions = new ToolOptions();
        }

        /// <summary>
        /// Tests that the x-ms-message-id header is added when Activity.Id is present.
        /// </summary>
        [Fact]
        public async Task SendAsync_AddsMessageIdHeader_WhenActivityIdIsPresent()
        {
            // Arrange
            var expectedMessageId = "test-message-id-123";
            var turnContextMock = CreateTurnContextMock(messageId: expectedMessageId);

            var innerHandler = new TestableHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
            var handler = new HttpContextHeadersHandler(turnContextMock.Object, _loggerMock.Object, _toolOptions)
            {
                InnerHandler = innerHandler
            };

            var httpClient = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com/api");

            // Act
            await httpClient.SendAsync(request);

            // Assert
            innerHandler.CapturedRequest.Should().NotBeNull();
            innerHandler.CapturedRequest!.Headers.Contains("x-ms-message-id").Should().BeTrue();
            innerHandler.CapturedRequest.Headers.GetValues("x-ms-message-id").Should().ContainSingle()
                .Which.Should().Be(expectedMessageId);
        }

        /// <summary>
        /// Tests that a warning is logged when Activity.Id is missing.
        /// </summary>
        [Fact]
        public async Task SendAsync_LogsWarning_WhenActivityIdIsMissing()
        {
            // Arrange
            var turnContextMock = CreateTurnContextMock(messageId: null);

            var innerHandler = new TestableHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
            var handler = new HttpContextHeadersHandler(turnContextMock.Object, _loggerMock.Object, _toolOptions)
            {
                InnerHandler = innerHandler
            };

            var httpClient = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com/api");

            // Act
            await httpClient.SendAsync(request);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Activity does not contain a message ID")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Tests that the request proceeds without the x-ms-message-id header when Activity.Id is missing.
        /// </summary>
        [Fact]
        public async Task SendAsync_ProceedsWithoutMessageIdHeader_WhenActivityIdIsMissing()
        {
            // Arrange
            var turnContextMock = CreateTurnContextMock(messageId: null);

            var innerHandler = new TestableHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
            var handler = new HttpContextHeadersHandler(turnContextMock.Object, _loggerMock.Object, _toolOptions)
            {
                InnerHandler = innerHandler
            };

            var httpClient = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com/api");

            // Act
            var response = await httpClient.SendAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            innerHandler.CapturedRequest.Should().NotBeNull();
            innerHandler.CapturedRequest!.Headers.Contains("x-ms-message-id").Should().BeFalse();
        }

        /// <summary>
        /// Tests that the request proceeds without the x-ms-message-id header when Activity.Id is empty.
        /// </summary>
        [Fact]
        public async Task SendAsync_ProceedsWithoutMessageIdHeader_WhenActivityIdIsEmpty()
        {
            // Arrange
            var turnContextMock = CreateTurnContextMock(messageId: string.Empty);

            var innerHandler = new TestableHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
            var handler = new HttpContextHeadersHandler(turnContextMock.Object, _loggerMock.Object, _toolOptions)
            {
                InnerHandler = innerHandler
            };

            var httpClient = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com/api");

            // Act
            var response = await httpClient.SendAsync(request);

            // Assert
            response.Should().NotBeNull();
            innerHandler.CapturedRequest.Should().NotBeNull();
            innerHandler.CapturedRequest!.Headers.Contains("x-ms-message-id").Should().BeFalse();
        }

        private static Mock<ITurnContext> CreateTurnContextMock(string? messageId)
        {
            var conversationAccount = new ConversationAccount { Id = "conv-123" };

            var activityMock = new Mock<IActivity>();
            activityMock.Setup(a => a.Id).Returns(messageId!);
            activityMock.Setup(a => a.Conversation).Returns(conversationAccount);
            activityMock.Setup(a => a.Text).Returns("Test message");

            var stackStateMock = new Mock<TurnContextStateCollection>();

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(activityMock.Object);
            turnContextMock.Setup(tc => tc.StackState).Returns(stackStateMock.Object);

            return turnContextMock;
        }

        /// <summary>
        /// Test helper class to capture the HTTP request for assertion.
        /// </summary>
        private class TestableHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public TestableHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            public HttpRequestMessage? CapturedRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                return Task.FromResult(_response);
            }
        }
    }
}
