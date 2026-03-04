// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Threading.Channels;

namespace Microsoft.Agents.A365.Tooling.Transports;

/// <summary>
/// A wrapping MCP client transport that injects agent identity into the <c>_meta</c>
/// field of <c>tools/call</c> JSON-RPC requests before forwarding them to the inner transport.
/// This is used for remote SSE-based MCP servers.
/// </summary>
internal sealed class IdentityInjectingTransport : ITransport
{
    private readonly ITransport _innerTransport;
    private readonly AgentIdentityContext _identityContext;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityInjectingTransport"/> class.
    /// </summary>
    /// <param name="innerTransport">The underlying transport returned by the real client transport.</param>
    /// <param name="identityContext">The agent identity to inject into MCP requests.</param>
    /// <param name="logger">Optional logger.</param>
    public IdentityInjectingTransport(ITransport innerTransport, AgentIdentityContext identityContext, ILogger? logger = null)
    {
        _innerTransport = innerTransport ?? throw new ArgumentNullException(nameof(innerTransport));
        _identityContext = identityContext ?? throw new ArgumentNullException(nameof(identityContext));
        _logger = logger;
    }

    /// <inheritdoc/>
    public string? SessionId => _innerTransport.SessionId;

    /// <inheritdoc/>
    public ChannelReader<JsonRpcMessage> MessageReader => _innerTransport.MessageReader;

    /// <inheritdoc/>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        // Intercept tools/call messages to inject agent identity into _meta
        var messageJson = JsonSerializer.Serialize(message);

        var modifiedJson = AgentIdentityHelper.InjectIdentityIntoMcpMessage(messageJson, _identityContext);

        if (modifiedJson != messageJson)
        {
            _logger?.LogDebug("[IdentityTransport] Injected agent identity into tools/call _meta");

            // Deserialize back to JsonRpcMessage and send through the inner transport
            var modifiedMessage = JsonSerializer.Deserialize<JsonRpcMessage>(modifiedJson);
            if (modifiedMessage != null)
            {
                await _innerTransport.SendMessageAsync(modifiedMessage, cancellationToken);
                return;
            }
        }

        // If not a tools/call or injection failed, send original message
        await _innerTransport.SendMessageAsync(message, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return _innerTransport.DisposeAsync();
    }
}

/// <summary>
/// A wrapping <see cref="IClientTransport"/> that injects agent identity into MCP requests.
/// Wraps an existing <see cref="IClientTransport"/> (e.g., <see cref="SseClientTransport"/>)
/// and returns an <see cref="IdentityInjectingTransport"/> from <see cref="ConnectAsync"/>.
/// </summary>
internal sealed class IdentityInjectingClientTransport : IClientTransport
{
    private readonly IClientTransport _innerClientTransport;
    private readonly AgentIdentityContext _identityContext;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityInjectingClientTransport"/> class.
    /// </summary>
    /// <param name="innerClientTransport">The actual client transport (SSE, etc.).</param>
    /// <param name="identityContext">The agent identity to inject.</param>
    /// <param name="logger">Optional logger.</param>
    public IdentityInjectingClientTransport(IClientTransport innerClientTransport, AgentIdentityContext identityContext, ILogger? logger = null)
    {
        _innerClientTransport = innerClientTransport ?? throw new ArgumentNullException(nameof(innerClientTransport));
        _identityContext = identityContext ?? throw new ArgumentNullException(nameof(identityContext));
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => _innerClientTransport.Name;

    /// <inheritdoc/>
    public async Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var innerTransport = await _innerClientTransport.ConnectAsync(cancellationToken);
        _logger?.LogInformation("[IdentityTransport] Wrapping transport with agent identity injection (agentInstanceId: {AgentInstanceId})",
            _identityContext.AgentInstanceId);
        return new IdentityInjectingTransport(innerTransport, _identityContext, _logger);
    }
}
