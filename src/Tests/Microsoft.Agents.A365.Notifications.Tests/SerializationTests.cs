using FluentAssertions;
using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json;

namespace Microsoft.Agents.A365.Notifications.Tests;

/// <summary>
/// Core serialization tests for notification models and converters.
/// </summary>
[TestClass]
public class SerializationTests
{
    [TestMethod]
    public void EmailReference_SerializeDeserialize_PreservesExtensionProperties()
    {
        // Arrange - Test core converter logic with extension properties
        var emailRef = new EmailReference { Id = "test123", ConversationId = "conv456" };
        emailRef.Properties.Add("customData", JsonDocument.Parse("{\"nested\": \"value\"}").RootElement);

        // Act
        var json = ProtocolJsonSerializer.ToJson(emailRef);
        var deserialized = ProtocolJsonSerializer.ToObject<EmailReference>(json);

        // Assert - Verify converter preserves core and extension data
        deserialized!.Id.Should().Be("test123");
        deserialized.ConversationId.Should().Be("conv456");
        deserialized.Properties["customData"].GetProperty("nested").GetString().Should().Be("value");
    }

    [TestMethod]
    public void WpxComment_SerializeDeserialize_HandlesComplexExtensionData()
    {
        // Arrange - Test converter with array extension data
        var wpxComment = new WpxComment { CommentId = "comment123" };
        wpxComment.Properties.Add("mentions", JsonDocument.Parse("[\"user1\", \"user2\"]").RootElement);

        // Act
        var json = ProtocolJsonSerializer.ToJson(wpxComment);
        var deserialized = ProtocolJsonSerializer.ToObject<WpxComment>(json);

        // Assert - Verify converter handles arrays correctly
        deserialized!.CommentId.Should().Be("comment123");
        deserialized.Properties["mentions"].GetArrayLength().Should().Be(2);
    }
}