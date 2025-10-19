# Kairo Notifications SDK for .NET

## 🚀 Features

- **OnAgentNotification**: Support to easily handle notification events (such as when agent receives an email, or when agent is @-mentioned in a Word document).

## 🚀 Quick Start

In your Agent class that extends `AgentApplication`:
1. Add using directives (TODO: this needs to be cleaned up):
    ```csharp
    using AgentNotification;
    using AgentNotification.Extensions;
    using AgentNotification.Models;
    using Microsoft.Kairo.Sdk.AgentsSdkExtensions;
    using Microsoft.Kairo.Sdk.AgentsSdkExtensions.Models;
    ```
2. Create a method `AgentNotificationActivityAsync`:
    ```csharp
    private async Task AgentNotificationActivityAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity activity, CancellationToken cancellationToken)
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
                // handle notification when the agent has received email
                return;
            case NotificationTypeEnum.WpxComment:
                // handle notification when the agent has been @-mentioned in a comment in a Word document
                return;
        }

        throw new NotImplementedException();
    }
    ```
3. Register this method with Agents SDK, by calling `OnAgentNotification`:
    ```csharp
        // Register Agentic specific Activity routes.  These will only be used if the incoming Activity is Agentic.
        this.OnAgentNotification("*", AgentNotificationActivityAsync,RouteRank.Last,  autoSignInHandlers: autoSignInHandlers);
    ```

## 🛠️ Sample Application

- **Semantic Kernel Multiturn**: [`/dotnet/samples/semantic-kernel-multiturn/`](./dotnet/samples/semantic-kernel-multiturn/) - C# Semantic Kernel sample.
