// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing;

using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;

[TestClass]
public sealed class MessageUtilsTest
{
    [TestMethod]
    public void NormalizeInputMessages_FromStrings_WrapsAsUserRole()
    {
        var result = MessageUtils.NormalizeInputMessages(new[] { "Hello" });
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Role.Should().Be(MessageRole.User);
        result.Messages[0].Parts.Should().HaveCount(1);
        result.Messages[0].Parts[0].Should().BeOfType<TextPart>().Which.Content.Should().Be("Hello");
    }

    [TestMethod]
    public void NormalizeOutputMessages_FromStrings_WrapsAsAssistantRole()
    {
        var result = MessageUtils.NormalizeOutputMessages(new[] { "Response" });
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Role.Should().Be(MessageRole.Assistant);
        result.Messages[0].Parts[0].Should().BeOfType<TextPart>().Which.Content.Should().Be("Response");
        result.Messages[0].FinishReason.Should().Be("stop");
    }

    [TestMethod]
    public void SerializeMessages_InputMessages_ProducesValidJsonArray()
    {
        var wrapper = new InputMessages(new[]
        {
            new ChatMessage(MessageRole.User, new IMessagePart[] { new TextPart("Hello") })
        });
        var json = MessageUtils.Serialize(wrapper);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("role").GetString().Should().Be("user");
        doc.RootElement[0].GetProperty("parts")[0].GetProperty("type").GetString().Should().Be("text");
        doc.RootElement[0].GetProperty("parts")[0].GetProperty("content").GetString().Should().Be("Hello");
    }

    [TestMethod]
    public void SerializeMessages_OutputMessages_IncludesFinishReason()
    {
        var wrapper = new OutputMessages(new[]
        {
            new OutputMessage(MessageRole.Assistant, new IMessagePart[] { new TextPart("Answer") }, finishReason: "stop")
        });
        var json = MessageUtils.Serialize(wrapper);
        var doc = JsonDocument.Parse(json);
        doc.RootElement[0].GetProperty("finish_reason").GetString().Should().Be("stop");
    }

    [TestMethod]
    public void SerializeMessages_OutputMessages_DefaultsFinishReasonToStop()
    {
        var wrapper = new OutputMessages(new[]
        {
            new OutputMessage(MessageRole.Assistant, new IMessagePart[] { new TextPart("Answer") })
        });
        var json = MessageUtils.Serialize(wrapper);
        var doc = JsonDocument.Parse(json);
        doc.RootElement[0].GetProperty("finish_reason").GetString().Should().Be("stop");
    }

    [TestMethod]
    public void SerializeMessages_OmitsNullValues()
    {
        var wrapper = new InputMessages(new[]
        {
            new ChatMessage(MessageRole.User, new IMessagePart[] { new TextPart("Hi") })
        });
        var json = MessageUtils.Serialize(wrapper);
        json.Should().NotContain("\"name\"");
    }

    [TestMethod]
    public void SerializeMessages_EnumSerializesAsSnakeCaseString()
    {
        var wrapper = new InputMessages(new[]
        {
            new ChatMessage(MessageRole.User, new IMessagePart[] { new TextPart("test") })
        });
        var json = MessageUtils.Serialize(wrapper);
        json.Should().Contain("\"role\":\"user\"");
    }

    [TestMethod]
    public void SerializeMessages_PreservesUnicode()
    {
        var content = "日本語テスト 🚀";
        var wrapper = new InputMessages(new[]
        {
            new ChatMessage(MessageRole.User, new IMessagePart[] { new TextPart(content) })
        });
        var json = MessageUtils.Serialize(wrapper);
        var doc = JsonDocument.Parse(json);
        doc.RootElement[0].GetProperty("parts")[0]
            .GetProperty("content").GetString().Should().Be(content);
    }

    [TestMethod]
    public void SerializeMessages_EmptyMessages_ProducesEmptyArray()
    {
        var wrapper = new InputMessages(Array.Empty<ChatMessage>());
        var json = MessageUtils.Serialize(wrapper);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public void SerializeMessages_MultipleMessages_ProducesArray()
    {
        var wrapper = new InputMessages(new[]
        {
            new ChatMessage(MessageRole.System, new IMessagePart[] { new TextPart("You are helpful.") }),
            new ChatMessage(MessageRole.User, new IMessagePart[] { new TextPart("Question?") })
        });
        var json = MessageUtils.Serialize(wrapper);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(2);
        doc.RootElement[0].GetProperty("role").GetString().Should().Be("system");
        doc.RootElement[1].GetProperty("role").GetString().Should().Be("user");
    }
}
