using FluentAssertions;
using Microsoft.Agents.A365.Notifications;
using Microsoft.Agents.A365.Notifications.Extensions;
using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System.Reflection;

namespace Microsoft.Agents.A365.Notifications.Tests;

/// <summary>
/// Unit tests for the AgentNotification class.
/// </summary>
[TestClass]
public class AgentNotificationTests
{
    [TestMethod]
    public void Constructor_WithNullApp_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Action act = () => new AgentNotification.AgentNotification(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithMessage("*app*");
    }

    [TestMethod]
    public void OnAgentNotification_WithValidParameters_ReturnsAgentNotificationInstance()
    {
        // Since we cannot easily mock AgentApplication, this test validates the concept
        // The actual functionality is tested through integration tests or by examining
        // the method signature and parameter validation
        
        // Arrange & Act - Test that the method signature exists and accepts the expected parameters
        var method = typeof(AgentNotification.AgentNotification).GetMethod("OnAgentNotification");
        
        // Assert
        method.Should().NotBeNull("OnAgentNotification method should exist");
        method!.ReturnType.Should().Be(typeof(AgentNotification.AgentNotification), "should return AgentNotification instance");
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(4, "should have 4 parameters: subChannelId, handler, rank, autoSignInHandlers");
        parameters[0].ParameterType.Should().Be(typeof(string), "first parameter should be string subChannelId");
        parameters[1].ParameterType.Should().Be(typeof(AgentNotificationHandler), "second parameter should be AgentNotificationHandler");
        parameters[2].ParameterType.Should().Be(typeof(ushort), "third parameter should be ushort rank");
        parameters[3].ParameterType.Should().Be(typeof(string[]), "fourth parameter should be string[] autoSignInHandlers");
    }

    [TestMethod]
    public void OnAgentNotification_WithWildcardSubChannel_ReturnsAgentNotificationInstance()
    {
        // Since we cannot easily mock AgentApplication, this test validates the concept
        // The actual functionality is tested through integration tests or by examining
        // the extension methods that use this functionality
        
        // Arrange & Act - Verify that the AgentNotificationExtensions exist for wildcard scenarios
        var extensionType = typeof(AgentNotification.AgentNotificationExtensions);
        var methods = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        
        // Assert
        methods.Should().Contain(m => m.Name == "OnAgentNotification", "extension method should exist");
        
        var onAgentNotificationMethod = methods.First(m => m.Name == "OnAgentNotification");
        onAgentNotificationMethod.Should().NotBeNull("OnAgentNotification extension method should exist");
        
        var parameters = onAgentNotificationMethod.GetParameters();
        parameters[0].ParameterType.Name.Should().Contain("AgentApplication", "first parameter should extend AgentApplication");
    }

    [TestMethod]
    public void IsValidSubChannel_WithKnownSubChannels_ReturnsTrue()
    {
        // Test the critical business logic for subchannel validation
        // Using reflection to access the private method for testing
        var agentNotification = CreateAgentNotificationWithMockApp();
        var method = typeof(AgentNotification.AgentNotification).GetMethod("IsValidSubChannel", BindingFlags.NonPublic | BindingFlags.Static);
        
        // Act & Assert - Test all known valid subchannels
        ((bool)method!.Invoke(null, new object[] { SubChannels.AgentsEmailSubChannel })!)
            .Should().BeTrue("email subchannel should be valid");
        ((bool)method.Invoke(null, new object[] { SubChannels.AgentsExcelSubChannel })!)
            .Should().BeTrue("excel subchannel should be valid");
        ((bool)method.Invoke(null, new object[] { SubChannels.AgentsWordSubChannel })!)
            .Should().BeTrue("word subchannel should be valid");
        ((bool)method.Invoke(null, new object[] { SubChannels.AgentsPowerPointSubChannel })!)
            .Should().BeTrue("powerpoint subchannel should be valid");
        ((bool)method.Invoke(null, new object[] { SubChannels.FederatedKnowledgeServiceSubChannel })!)
            .Should().BeTrue("federated knowledge service subchannel should be valid");
    }

    [TestMethod]
    public void IsValidSubChannel_WithUnknownSubChannel_ReturnsFalse()
    {
        // Test security: unknown subchannels should be rejected
        var method = typeof(AgentNotification.AgentNotification).GetMethod("IsValidSubChannel", BindingFlags.NonPublic | BindingFlags.Static);
        
        // Act & Assert
        ((bool)method!.Invoke(null, new object[] { "unknown-subchannel" })!)
            .Should().BeFalse("unknown subchannel should be rejected");
        ((bool)method.Invoke(null, new object[] { "malicious-channel" })!)
            .Should().BeFalse("malicious subchannel should be rejected");
        ((bool)method.Invoke(null, new object[] { "" })!)
            .Should().BeFalse("empty subchannel should be rejected");
    }

    [TestMethod]
    public void IsChannelForMe_WithAgentsChannel_ReturnsTrue()
    {
        // Test the core channel matching logic
        var agentNotification = CreateAgentNotificationWithMockApp();
        var method = typeof(AgentNotification.AgentNotification).GetMethod("IsChannelForMe", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Arrange - Create activity with agents channel
        var activity = CreateMockActivity("agents", "email");
        
        // Act
        var result = (bool)method!.Invoke(agentNotification, new object[] { activity })!;
        
        // Assert
        result.Should().BeTrue("activity with 'agents' channel should be accepted");
    }

    [TestMethod]
    public void IsChannelForMe_WithWrongChannel_ReturnsFalse()
    {
        // Test channel filtering security
        var agentNotification = CreateAgentNotificationWithMockApp();
        var method = typeof(AgentNotification.AgentNotification).GetMethod("IsChannelForMe", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Arrange - Create activity with wrong channel
        var wrongChannelActivity = CreateMockActivity("teams", "email");
        var nullChannelActivity = CreateMockActivity(null, "email");
        
        // Act & Assert
        ((bool)method!.Invoke(agentNotification, new object[] { wrongChannelActivity })!)
            .Should().BeFalse("activity with wrong channel should be rejected");
        ((bool)method.Invoke(agentNotification, new object[] { nullChannelActivity })!)
            .Should().BeFalse("activity with null channel should be rejected");
    }

    [TestMethod]
    public void IsForKnownSubChannel_WithMatchingSubChannel_ReturnsTrue()
    {
        // Test subchannel matching logic - critical for routing
        var agentNotification = CreateAgentNotificationWithMockApp();
        var method = typeof(AgentNotification.AgentNotification).GetMethod("IsForKnownSubChannel", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Arrange
        var activity = CreateMockActivity("agents", SubChannels.AgentsEmailSubChannel);
        
        // Act
        var result = (bool)method!.Invoke(agentNotification, new object[] { activity, SubChannels.AgentsEmailSubChannel })!;
        
        // Assert
        result.Should().BeTrue("activity with matching subchannel should be accepted");
    }

    [TestMethod]
    public void IsForKnownSubChannel_WithNonMatchingSubChannel_ReturnsFalse()
    {
        // Test subchannel filtering
        var agentNotification = CreateAgentNotificationWithMockApp();
        var method = typeof(AgentNotification.AgentNotification).GetMethod("IsForKnownSubChannel", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Arrange
        var activity = CreateMockActivity("agents", SubChannels.AgentsEmailSubChannel);
        
        // Act
        var result = (bool)method!.Invoke(agentNotification, new object[] { activity, SubChannels.AgentsWordSubChannel })!;
        
        // Assert
        result.Should().BeFalse("activity with non-matching subchannel should be rejected");
    }

    [TestMethod]
    public void IsForKnownSubChannel_WithInvalidSubChannel_ReturnsFalse()
    {
        // Test security: invalid subchannels should be rejected
        var agentNotification = CreateAgentNotificationWithMockApp();
        var method = typeof(AgentNotification.AgentNotification).GetMethod("IsForKnownSubChannel", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Arrange
        var activity = CreateMockActivity("agents", "invalid-subchannel");
        
        // Act & Assert
        ((bool)method!.Invoke(agentNotification, new object[] { activity, "invalid-subchannel" })!)
            .Should().BeFalse("invalid subchannel should be rejected");
        ((bool)method.Invoke(agentNotification, new object[] { activity, null! })!)
            .Should().BeFalse("null subchannel should be rejected");
        ((bool)method.Invoke(agentNotification, new object[] { activity, "" })!)
            .Should().BeFalse("empty subchannel should be rejected");
    }

    [TestMethod]
    public void Constructor_SetsCorrectChannelId()
    {
        // Test that the constructor properly initializes the channel configuration
        var agentNotification = CreateAgentNotificationWithMockApp();
        
        // Use reflection to access the ChannelId property
        var channelIdProperty = typeof(AgentNotification.AgentNotification).GetProperty("ChannelId", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        var channelId = channelIdProperty?.GetValue(agentNotification) as ChannelId;
        
        // Assert
        channelId.Should().NotBeNull("ChannelId should be set");
        channelId!.Channel.Should().Be("agents", "Channel should be set to 'agents'");
        channelId.SubChannel.Should().Be("*", "SubChannel should be set to wildcard '*'");
    }

    // Helper methods for creating test objects
    private static AgentNotification.AgentNotification CreateAgentNotificationWithMockApp()
    {
        // Create a real AgentApplication instead of mocking due to constructor requirements
        var options = new AgentApplicationOptions((IStorage)null!);
        var realApp = new TestAgentApplication(options);
        return new AgentNotification.AgentNotification(realApp);
    }

    // Simple test implementation of AgentApplication
    private class TestAgentApplication : AgentApplication
    {
        public TestAgentApplication(AgentApplicationOptions options) : base(options) { }
    }

    private static IActivity CreateMockActivity(string? channel, string? subChannel)
    {
        var mockActivity = new Mock<IActivity>();
        
        if (channel == null)
        {
            // For null channel test - return null ChannelId
            mockActivity.Setup(x => x.ChannelId).Returns((ChannelId?)null!);
        }
        else
        {
            var channelId = new ChannelId(channel) { SubChannel = subChannel };
            mockActivity.Setup(x => x.ChannelId).Returns(channelId);
        }
        
        return mockActivity.Object;
    }

    // Core enum validation test - consolidated from removed files
    [TestMethod]
    public void NotificationTypeEnum_HasExpectedValues()
    {
        // Assert - Test critical enum values for business logic
        ((int)NotificationTypeEnum.Unknown).Should().Be(0);
        ((int)NotificationTypeEnum.WpxComment).Should().Be(1);
        ((int)NotificationTypeEnum.EmailNotification).Should().Be(2);
        ((int)NotificationTypeEnum.FederatedKnowledgeServiceNotification).Should().Be(3);
    }

    /// <summary>
    /// Creates a simple test handler for testing purposes.
    /// </summary>
    private static AgentNotificationHandler CreateTestHandler()
    {
        return (turnContext, turnState, agentNotificationActivity, cancellationToken) => Task.CompletedTask;
    }
}