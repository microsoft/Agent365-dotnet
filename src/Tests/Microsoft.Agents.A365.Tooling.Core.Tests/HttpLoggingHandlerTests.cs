// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Handlers;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Moq;
using Moq.Protected;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// Tests for <see cref="HttpLoggingHandler"/> with streaming responses.
/// </summary>
public class HttpLoggingHandlerTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SendAsync_StreamingResponseThatNeverEnds_ReturnsWhenHeadersArrive()
    {
        // Arrange
        var content = new StreamContent(new FakeMcpServer.OpenEventStream());
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        var inner = new Mock<HttpMessageHandler>();
        inner.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var invoker = new HttpMessageInvoker(new HttpLoggingHandler(new Mock<ILogger>().Object) { InnerHandler = inner.Object });

        // Act
        using var response = await invoker
            .SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://mcp.test/agents/servers/mcp_MailTools"), CancellationToken.None)
            .WaitAsync(s_timeout);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task McpClientDisposeAsync_WhileGetStreamIsOpen_Completes()
    {
        // Arrange: an MCP client whose requests go through HttpLoggingHandler to a server that keeps the GET stream open.
        var server = new FakeMcpServer();
        using var httpClient = new HttpClient(new HttpLoggingHandler(new Mock<ILogger>().Object) { InnerHandler = server.CreateHandler() });
        var transport = new SseClientTransport(
            new SseClientTransportOptions
            {
                Endpoint = new Uri("http://mcp.test/agents/servers/mcp_MailTools"),
                TransportMode = HttpTransportMode.AutoDetect,
            },
            httpClient);
        var client = await McpClientFactory.CreateAsync(transport).WaitAsync(s_timeout);
        (await FakeMcpServer.WaitUntilAsync(() => server.OpenGetStreams == 1)).Should().BeTrue("the client opens a GET stream");

        // Act
        await client.DisposeAsync().AsTask().WaitAsync(s_timeout);

        // Assert
        server.OpenGetStreams.Should().Be(0);
    }
}
