# Microsoft Agents A365 Notifications

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Notifications.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Notifications/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.Notifications.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Notifications/)

The Microsoft Agents A365 Notifications module provides a comprehensive framework for handling agent notification events in Microsoft 365 environments. This module enables agents to respond to various notification types including email notifications and document mentions.

## Overview

This module simplifies the handling of notification events within AI agents, allowing developers to create responsive agents that can:

- Receive and process email notifications
- Handle @-mentions in Word documents
- React to various Microsoft 365 events
- Integrate seamlessly with the Microsoft Agents SDK

## Features

- **OnAgentNotification**: Support to easily handle notification events (such as when agent receives an email, or when agent is @-mentioned in a Word document)
- **Multiple Notification Types**: Support for email notifications, document comments, and more
- **Event-Driven Architecture**: Asynchronous notification handling for scalable agent applications
- **Type-Safe Models**: Strongly-typed notification models for reliable event processing

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
            // Add your email notification handling logic here
            return;
            
        case NotificationTypeEnum.WpxComment:
            // Handle notification when the agent has been @-mentioned in a comment in a Word document
            // Add your document comment handling logic here
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

The SDK currently supports the following notification types:

- **EmailNotification**: Triggered when the agent receives an email
- **WpxComment**: Triggered when the agent is @-mentioned in a Word document comment

## Configuration

The notification handlers are configured during agent initialization using the `OnAgentNotification` method. You can specify route patterns, route ranks, and auto sign-in handlers as needed.

### Example Configuration

```csharp
// Register notification handler with wildcard pattern
this.OnAgentNotification("*", AgentNotificationActivityAsync, RouteRank.Last, autoSignInHandlers: autoSignInHandlers);

// Register specific notification type handlers
this.OnAgentNotification("email", HandleEmailNotificationAsync, RouteRank.First);
this.OnAgentNotification("comment", HandleCommentNotificationAsync, RouteRank.First);
```

## Package Structure

- **AgentNotification.cs**: Core notification handling functionality
- **Extensions/**: Extension methods for registering notification handlers
- **Models/**: Strongly-typed notification models and enums
- **Serialization/**: JSON serialization utilities for notification payloads

## Sample Applications

- **Semantic Kernel Multiturn**: Demonstrates notification handling with Semantic Kernel integration

## Useful Links

### Microsoft Agents A365 SDK

- [Microsoft Agents A365 Observability](../Observability/README.md) - Monitoring and tracing for agent applications
- [Microsoft Agents A365 Runtime](../Runtime/README.md) - Core runtime utilities for agents
- [Microsoft Agents A365 Tooling](../Tooling/README.md) - Developer tools and utilities
- [Microsoft Agents A365 DevTools](../DevTools/README.md) - Code analyzers and development tools

### Documentation

- [Microsoft Agents A365 Developer Documentation](<https://learn.microsoft.com/en-us/microsoft-agent-365/developer/>)

### Related Repositories

- [Agent365-python](<https://github.com/microsoft/Agent365-python>) - Python SDK for Microsoft Agents A365
- [Agent365-nodejs](<https://github.com/microsoft/Agent365-nodejs>) - Node.js SDK for Microsoft Agents A365
- [Agent365-Samples](<https://github.com/microsoft/Agent365-Samples>) - Sample applications and code examples

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
