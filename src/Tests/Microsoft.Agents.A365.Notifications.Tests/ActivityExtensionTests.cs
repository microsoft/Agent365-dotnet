using FluentAssertions;
using Microsoft.Agents.A365.Notifications.Extensions;
using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Moq;

namespace Microsoft.Agents.A365.Notifications.Tests;

/// <summary>
/// Core tests for ActivityExtension methods that handle entity extraction logic.
/// </summary>
[TestClass]
public class ActivityExtensionTests
{
    [TestMethod]
    public void GetEmailReference_WithEmailEntity_ReturnsEmailReference()
    {
        // Arrange - Test core entity extraction logic
        var mockActivity = new Mock<IActivity>();
        var emailRef = new EmailReference { Id = "email-123", ConversationId = "conv-456" };
        emailRef.Type = EmailReference.EntityTypeName;
        mockActivity.Setup(a => a.Entities).Returns(new List<Entity> { emailRef });
        
        // Act
        var result = mockActivity.Object.GetEmailReference();
        
        // Assert - Verify core extraction works
        result.Should().NotBeNull();
        result!.Id.Should().Be("email-123");
        result.ConversationId.Should().Be("conv-456");
    }

    [TestMethod]
    public void GetWpxComment_WithWpxCommentEntity_ReturnsWpxComment()
    {
        // Arrange - Test core entity extraction logic
        var mockActivity = new Mock<IActivity>();
        var wpxComment = new WpxComment { CommentId = "comment-123", DocumentId = "doc-456" };
        wpxComment.Type = nameof(WpxComment);
        mockActivity.Setup(a => a.Entities).Returns(new List<Entity> { wpxComment });
        
        // Act
        var result = mockActivity.Object.GetWpxComment();
        
        // Assert - Verify core extraction works
        result.Should().NotBeNull();
        result!.CommentId.Should().Be("comment-123");
        result.DocumentId.Should().Be("doc-456");
    }

    [TestMethod]
    public void GetAgentNotificationActivity_WithValidActivity_ReturnsWrapper()
    {
        // Arrange - Test core wrapper creation logic
        var mockActivity = new Mock<IActivity>();
        mockActivity.Setup(a => a.Id).Returns("activity-123");
        mockActivity.Setup(a => a.Text).Returns("test message");
        
        // Act
        var result = mockActivity.Object.GetAgentNotificationActivity();
        
        // Assert - Verify wrapper creation works
        result.Should().NotBeNull();
        result.Text.Should().Be("test message");
        result.NotificationType.Should().Be(NotificationTypeEnum.Unknown);
    }

    [TestMethod] 
    public void GetAgentNotificationActivity_WithNullActivity_ThrowsException()
    {
        // Arrange & Act & Assert - Test error handling
        IActivity? nullActivity = null;
        Action act = () => nullActivity!.GetAgentNotificationActivity();
        act.Should().Throw<ArgumentNullException>();
    }
}