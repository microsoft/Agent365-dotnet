// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Microsoft.Agents.A365.Tooling.Core.Tests;

/// <summary>
/// In-memory Streamable HTTP MCP server. It answers <c>initialize</c> with an <c>Mcp-Session-Id</c>, lists one tool,
/// answers tool calls with the request's <c>Authorization</c> header, keeps each GET stream open until the client
/// stops reading it, and records the sessions that DELETE requests end.
/// </summary>
internal sealed class FakeMcpServer
{
    private int _openGetStreams;
    private int _disposedHandlers;

    /// <summary>Gets the number of GET streams that are open.</summary>
    public int OpenGetStreams => Volatile.Read(ref _openGetStreams);

    /// <summary>Gets the number of handlers created by <see cref="CreateHandler"/> that were disposed.</summary>
    public int DisposedHandlers => Volatile.Read(ref _disposedHandlers);

    /// <summary>Gets the session IDs that <c>initialize</c> issued.</summary>
    public ConcurrentBag<string> CreatedSessions { get; } = new();

    /// <summary>Gets the session IDs and <c>Authorization</c> headers of DELETE requests.</summary>
    public ConcurrentBag<(string? SessionId, string? Authorization)> DeletedSessions { get; } = new();

    /// <summary>Gets or sets the path of a server that answers <c>tools/list</c> with HTTP 500.</summary>
    public string? FailToolsListPath { get; set; }

    /// <summary>Gets or sets a task that <c>tools/list</c> of the server at <see cref="ToolsListGatePath"/> waits for before it answers.</summary>
    public Task? ToolsListGate { get; set; }

    /// <summary>Gets or sets the path of the server whose <c>tools/list</c> waits for <see cref="ToolsListGate"/>.</summary>
    public string? ToolsListGatePath { get; set; }

    /// <summary>Creates a handler that serves requests from one MCP client.</summary>
    public HttpMessageHandler CreateHandler() => new Handler(this);

    /// <summary>Waits until <paramref name="condition"/> is true or the timeout elapses.</summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(10);
        }

        return true;
    }

    private sealed class Handler(FakeMcpServer server) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sessionId = request.Headers.TryGetValues("Mcp-Session-Id", out var ids) ? ids.FirstOrDefault() : null;
            var authorization = request.Headers.Authorization?.ToString();

            if (request.Method == HttpMethod.Get)
            {
                var stream = new OpenEventStream(server);
                var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            }

            if (request.Method == HttpMethod.Delete)
            {
                server.DeletedSessions.Add((sessionId, authorization));
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            using var message = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var method = message.RootElement.GetProperty("method").GetString();
            if (!message.RootElement.TryGetProperty("id", out var id))
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            switch (method)
            {
                case "initialize":
                    var protocolVersion = message.RootElement.GetProperty("params").GetProperty("protocolVersion").GetString();
                    var newSessionId = Guid.NewGuid().ToString();
                    server.CreatedSessions.Add(newSessionId);
                    var response = Result(id, $"{{\"protocolVersion\":\"{protocolVersion}\",\"capabilities\":{{\"tools\":{{}}}},\"serverInfo\":{{\"name\":\"fake\",\"version\":\"1.0.0\"}}}}");
                    response.Headers.Add("Mcp-Session-Id", newSessionId);
                    return response;

                case "tools/list":
                    if (server.ToolsListGate is { } gate && request.RequestUri!.AbsolutePath == server.ToolsListGatePath)
                    {
                        await gate.WaitAsync(cancellationToken);
                    }

                    if (request.RequestUri!.AbsolutePath == server.FailToolsListPath)
                    {
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    }

                    return Result(id, "{\"tools\":[{\"name\":\"whoami\",\"description\":\"Returns the caller\",\"inputSchema\":{\"type\":\"object\",\"properties\":{}}}]}");

                case "tools/call":
                    return Result(id, $"{{\"content\":[{{\"type\":\"text\",\"text\":{JsonSerializer.Serialize(authorization)}}}],\"isError\":false}}");

                default:
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref server._disposedHandlers);
            }

            base.Dispose(disposing);
        }

        private static HttpResponseMessage Result(JsonElement id, string result) => new(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"result\":{result}}}", Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>
    /// An SSE response body that never ends: reads complete only when they are cancelled.
    /// </summary>
    internal sealed class OpenEventStream : Stream
    {
        private readonly FakeMcpServer? _server;
        private int _closed;

        public OpenEventStream(FakeMcpServer? server = null)
        {
            _server = server;
            if (_server is not null)
            {
                Interlocked.Increment(ref _server._openGetStreams);
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0 && _server is not null)
            {
                Interlocked.Decrement(ref _server._openGetStreams);
            }

            base.Dispose(disposing);
        }
    }
}
