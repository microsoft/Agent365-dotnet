# Microsoft.Agents.A365.Notifications

The Notifications package provides a framework for handling agent notification events in Microsoft 365 environments, including email notifications and document mentions.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Notifications
```

## Usage

### Create a Notification Handler

```csharp
using AgentNotification;
using AgentNotification.Extensions;
using AgentNotification.Models;

private async Task AgentNotificationActivityAsync(
    ITurnContext turnContext, 
    ITurnState turnState, 
    AgentNotificationActivity activity, 
    CancellationToken cancellationToken)
{
    switch (activity.NotificationType)
    {
        case NotificationTypeEnum.EmailNotification:
            // Handle notification when the agent has received email
            await HandleEmailNotificationAsync(activity, turnContext, cancellationToken);
            return;
            
        case NotificationTypeEnum.WpxComment:
            // Handle notification when the agent has been @-mentioned in a Word document comment
            await HandleDocumentCommentAsync(activity, turnContext, cancellationToken);
            return;
    }

    throw new NotImplementedException($"Notification type {activity.NotificationType} is not supported.");
}
```

### Register the Handler

```csharp
// Register notification handler in your Agent class that extends AgentApplication
this.OnAgentNotification("*", AgentNotificationActivityAsync, RouteRank.Last, autoSignInHandlers: autoSignInHandlers);
```

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
