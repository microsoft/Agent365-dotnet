# Microsoft.Agents.A365.Notifications

The Notifications package provides a framework for handling agent notification events in Microsoft 365 environments, including email notifications and document mentions.

## Overview

This package enables agents to respond to various notification types:

- Email notifications when the agent receives email
- @-mentions in Word documents
- Other Microsoft 365 notification events
- Seamless integration with the Microsoft Agents SDK

## Features

- **OnAgentNotification Handler**: Easy-to-use method for registering notification handlers
- **Multiple Notification Types**: Support for email, document comments, and more
- **Event-Driven Architecture**: Asynchronous notification handling for scalable applications
- **Type-Safe Models**: Strongly-typed notification models and enums
- **Flexible Routing**: Pattern-based routing with route ranking support

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Notifications
```

## Quick Start

In your Agent class that extends `AgentApplication`:

### 1. Add using directives

```csharp
using AgentNotification;
using AgentNotification.Extensions;
using AgentNotification.Models;
using Microsoft.Agents.A365.AgentsSdkExtensions;
using Microsoft.Agents.A365.AgentsSdkExtensions.Models;
```

### 2. Create a notification handler method

```csharp
private async Task AgentNotificationActivityAsync(
    ITurnContext turnContext, 
    ITurnState turnState, 
    AgentNotificationActivity activity, 
    CancellationToken cancellationToken)
{
    // Setup local service connection
    ServiceCollection serviceCollection = [
        new ServiceDescriptor(typeof(ITurnState), turnState),
        new ServiceDescriptor(typeof(ITurnContext), turnContext),
        new ServiceDescriptor(typeof(Kernel), _kernel),
    ];

    switch (activity.NotificationType)
    {
        case NotificationTypeEnum.EmailNotification:
            // Handle notification when the agent has received email
            await HandleEmailNotificationAsync(activity, turnContext, cancellationToken);
            return;
            
        case NotificationTypeEnum.WpxComment:
            // Handle notification when the agent has been @-mentioned in a comment in a Word document
            await HandleDocumentCommentAsync(activity, turnContext, cancellationToken);
            return;
    }

    throw new NotImplementedException($"Notification type {activity.NotificationType} is not supported.");
}
```

### 3. Register the notification handler

```csharp
// Register Agentic specific Activity routes. These will only be used if the incoming Activity is Agentic.
this.OnAgentNotification("*", AgentNotificationActivityAsync, RouteRank.Last, autoSignInHandlers: autoSignInHandlers);
```

## Notification Types

The package supports the following notification types through the `NotificationTypeEnum`:

### EmailNotification

Triggered when the agent receives an email. The notification includes:

- Sender information
- Subject line
- Email body/preview
- Timestamp
- Related metadata

Example handler:

```csharp
private async Task HandleEmailNotificationAsync(
    AgentNotificationActivity activity,
    ITurnContext turnContext,
    CancellationToken cancellationToken)
{
    // Extract email details
    var emailData = activity.Value; // Notification payload
    
    // Process the email notification
    _logger.LogInformation($"Received email notification from {emailData.Sender}");
    
    // Respond or take action
    await turnContext.SendActivityAsync(
        "I received your email and will process it shortly.",
        cancellationToken: cancellationToken);
}
```

### WpxComment

Triggered when the agent is @-mentioned in a Word document comment. The notification includes:

- Document information
- Comment text
- Mention location
- Commenter details

Example handler:

```csharp
private async Task HandleDocumentCommentAsync(
    AgentNotificationActivity activity,
    ITurnContext turnContext,
    CancellationToken cancellationToken)
{
    // Extract comment details
    var commentData = activity.Value;
    
    // Process the document comment
    _logger.LogInformation($"Mentioned in document: {commentData.DocumentName}");
    
    // Respond to the mention
    await turnContext.SendActivityAsync(
        $"I see you mentioned me in {commentData.DocumentName}. How can I help?",
        cancellationToken: cancellationToken);
}
```

## Configuration

### Basic Handler Registration

Register a single handler for all notification types:

```csharp
// Wildcard pattern - handles all notification types
this.OnAgentNotification("*", AgentNotificationActivityAsync, RouteRank.Last, autoSignInHandlers: autoSignInHandlers);
```

### Specific Notification Handlers

Register different handlers for different notification types:

```csharp
// Email-specific handler
this.OnAgentNotification("email", HandleEmailNotificationAsync, RouteRank.First);

// Document comment-specific handler
this.OnAgentNotification("comment", HandleDocumentCommentAsync, RouteRank.First);

// Fallback handler for unmatched notifications
this.OnAgentNotification("*", HandleOtherNotificationsAsync, RouteRank.Last);
```

### Route Ranking

Control handler priority using `RouteRank`:

```csharp
// High priority - executed first
this.OnAgentNotification("urgent", HandleUrgentNotificationAsync, RouteRank.First);

// Normal priority - executed after First rank handlers
this.OnAgentNotification("*", HandleStandardNotificationAsync, RouteRank.Normal);

// Low priority - executed last
this.OnAgentNotification("*", LogNotificationAsync, RouteRank.Last);
```

### Auto Sign-In Handlers

Configure automatic sign-in for notification handlers:

```csharp
var autoSignInHandlers = new List<IAutoSignInHandler>
{
    new MyCustomSignInHandler()
};

this.OnAgentNotification("*", AgentNotificationActivityAsync, RouteRank.Last, autoSignInHandlers: autoSignInHandlers);
```

## Package Structure

### Core Components

- **AgentNotification.cs**: Core notification handling functionality and base classes
- **Constants.cs**: Notification-related constants and configuration values

### Extensions

- **Extensions/**: Extension methods for registering notification handlers with the agent framework
- Provides `OnAgentNotification` extension method for easy handler registration

### Models

- **Models/**: Strongly-typed notification models and enums
- `AgentNotificationActivity`: Represents a notification activity
- `NotificationTypeEnum`: Enum defining supported notification types
- Additional model classes for notification payloads

### Serialization

- **Serialization/**: JSON serialization utilities for notification payloads
- Handles deserialization of notification data from M365 services
- Custom converters for notification-specific types

## Advanced Usage

### Accessing Notification Metadata

```csharp
private async Task AgentNotificationActivityAsync(
    ITurnContext turnContext, 
    ITurnState turnState, 
    AgentNotificationActivity activity, 
    CancellationToken cancellationToken)
{
    // Access notification metadata
    var notificationType = activity.NotificationType;
    var timestamp = activity.Timestamp;
    var source = activity.Source;
    
    _logger.LogInformation(
        "Notification received: Type={Type}, Time={Time}, Source={Source}",
        notificationType, timestamp, source);
    
    // Process based on notification type
    switch (notificationType)
    {
        case NotificationTypeEnum.EmailNotification:
            await HandleEmailAsync(activity, turnContext, cancellationToken);
            break;
            
        case NotificationTypeEnum.WpxComment:
            await HandleCommentAsync(activity, turnContext, cancellationToken);
            break;
            
        default:
            _logger.LogWarning("Unknown notification type: {Type}", notificationType);
            break;
    }
}
```

### Error Handling

```csharp
private async Task AgentNotificationActivityAsync(
    ITurnContext turnContext, 
    ITurnState turnState, 
    AgentNotificationActivity activity, 
    CancellationToken cancellationToken)
{
    try
    {
        switch (activity.NotificationType)
        {
            case NotificationTypeEnum.EmailNotification:
                await HandleEmailNotificationAsync(activity, turnContext, cancellationToken);
                break;
                
            case NotificationTypeEnum.WpxComment:
                await HandleDocumentCommentAsync(activity, turnContext, cancellationToken);
                break;
                
            default:
                throw new NotSupportedException($"Notification type {activity.NotificationType} is not supported.");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing notification: {Type}", activity.NotificationType);
        
        await turnContext.SendActivityAsync(
            "I encountered an error processing your notification. Please try again later.",
            cancellationToken: cancellationToken);
    }
}
```

## Related Documentation

- [Notifications Module Overview](../README.md)
- [Microsoft Agents A365 Developer Documentation](https://learn.microsoft.com/en-us/microsoft-agent-365/developer/)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
