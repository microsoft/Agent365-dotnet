// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests.TestHelpers;

/// <summary>
/// Test implementation of ChatMessageStore that allows setting chat messages for testing.
/// </summary>
internal class TestChatMessageStore : ChatMessageStore
{
    private readonly IList<ChatMessage>? _chatMessages;
    private readonly bool _throwException;

    public TestChatMessageStore(IList<ChatMessage>? chatMessages = null, bool throwException = false)
    {
        _chatMessages = chatMessages;
        _throwException = throwException;
    }

    public override Task<IEnumerable<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (_throwException)
        {
            throw new InvalidOperationException("Test exception from ChatMessageStore");
        }

        IEnumerable<ChatMessage> result = _chatMessages ?? Enumerable.Empty<ChatMessage>();
        return Task.FromResult(result);
    }

    public override Task AddMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override JsonElement Serialize(JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(_chatMessages ?? Enumerable.Empty<ChatMessage>(), options);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
