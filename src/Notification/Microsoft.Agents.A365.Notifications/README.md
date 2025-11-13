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

## 📋 **Telemetry**
 
Data Collection. The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
 
## Trademarks
 
*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
