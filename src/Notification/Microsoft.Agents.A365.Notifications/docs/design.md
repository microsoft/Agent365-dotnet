# Microsoft.Agents.A365.Notifications - Design Documentation

## Overview

The `Microsoft.Agents.A365.Notifications` package provides event-driven notification handling for Microsoft 365 applications. It enables agents to receive and respond to notifications from Microsoft 365 workloads including Outlook (email), Word, Excel, PowerPoint, and lifecycle events.

## Architecture

```
Microsoft.Agents.A365.Notifications
├── Public API
│   ├── AgentNotification                    # Main AgentExtension class
│   ├── AgentNotificationExtensions          # Extension methods
│   └── AgentNotificationHandler             # Handler delegate
├── Models/
│   ├── AgentNotificationActivity            # Activity wrapper
│   ├── EmailReference                       # Email notification entity
│   ├── EmailResponse                        # Email response entity
│   ├── WpxComment                           # Word/PowerPoint/Excel comment
│   ├── NotificationTypeEnum                 # Notification type enumeration
│   ├── SubChannels                          # Sub-channel constants
│   └── Events                               # Lifecycle event constants
├── Extensions/
│   └── AgentNotificationExtensions          # App extension methods
└── Serialization/
    ├── EmailReferenceConverter              # JSON converter
    ├── WpxCommentConverter                  # JSON converter
    └── SerializationInit                    # Serialization setup
```

## Key Components

### AgentNotification

**Source**: [AgentNotification.cs](../AgentNotification.cs)

The main `AgentExtension` class for routing notifications from Microsoft 365 applications.

```csharp
public class AgentNotification : AgentExtension
{
    private static readonly string ExtensionChannelId = "agents";
    private readonly AgentApplication _app;

    public AgentNotification(AgentApplication app)
    {
        _app = app;
        ChannelId = new ChannelId(ExtensionChannelId);
        ChannelId.SubChannel = "*";  // Match all sub-channels
    }

    // Register handlers for specific sub-channels
    public AgentNotification OnAgentNotification(
        string subChannelId,
        AgentNotificationHandler handler,
        ushort rank = RouteRank.Unspecified,
        string[] autoSignInHandlers = null!)
    {
        // Route selector matches channel and sub-channel
        RouteSelector routeSelector = (tc, ct) =>
            Task.FromResult(
                IsChannelForMe(tc.Activity) &&
                (subChannelId.Equals("*") || IsForKnownSubChannel(tc.Activity, subChannelId))
            );

        // Wrap activity in AgentNotificationActivity
        RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
        {
            var notification = new AgentNotificationActivity(turnContext.Activity);
            await handler(turnContext, turnState, notification, cancellationToken);
        };

        AddRoute(_app, routeSelector, routeHandler, false, rank, autoSignInHandlers);
        return this;
    }
}
```

### AgentNotificationHandler Delegate

```csharp
public delegate Task AgentNotificationHandler(
    ITurnContext turnContext,
    TurnState turnState,
    AgentNotificationActivity notification,
    CancellationToken cancellationToken);
```

### AgentNotificationExtensions

**Source**: [AgentNotification.cs](../AgentNotification.cs)

Extension methods for registering notification handlers on `AgentApplication`.

```csharp
// Register email notification handler
app.OnAgenticEmailNotification(async (turnContext, turnState, notification, ct) =>
{
    var emailRef = notification.GetEntity<EmailReference>();
    Console.WriteLine($"Received email: {emailRef.Id}");

    // Create and send response
    var reply = turnContext.Activity.CreateEmailResponseActivity("<html>...</html>");
    await turnContext.SendActivityAsync(reply, cancellationToken: ct);
});

// Register Word notification handler
app.OnAgenticWordNotification(async (turnContext, turnState, notification, ct) =>
{
    var comment = notification.GetEntity<WpxComment>();
    // Process Word comment...
});

// Register lifecycle notification handlers
app.OnAgenticUserIdentityCreatedNotification(async (context, state, notification, ct) =>
{
    // Handle new user identity...
});

app.OnAgenticUserWorkloadOnboardingNotification(async (context, state, notification, ct) =>
{
    // Handle workload onboarding...
});

app.OnAgenticUserDeletedNotification(async (context, state, notification, ct) =>
{
    // Handle user deletion...
});
```

### Sub-Channel Constants

**Source**: [SubChannels.cs](../Models/SubChannels.cs)

```csharp
public static class SubChannels
{
    public const string AgentsEmailSubChannel = "email";
    public const string AgentsWordSubChannel = "word";
    public const string AgentsExcelSubChannel = "excel";
    public const string AgentsPowerPointSubChannel = "powerpoint";
    public const string FederatedKnowledgeServiceSubChannel = "fks";
}
```

### Lifecycle Event Constants

**Source**: [Events.cs](../Models/Events.cs)

```csharp
public static class Events
{
    public const string AgentLifecycleEvent = "agent.lifecycle";
    public const string AgenticUserIdentityCreated = "agentic.user.identity.created";
    public const string AgenticUserWorkloadOnboardingUpdated = "agentic.user.workload.onboarding.updated";
    public const string AgenticUserDeleted = "agentic.user.deleted";
}
```

### EmailReference

**Source**: [EmailReference.cs](../Models/EmailReference.cs)

Entity representing an email notification.

```csharp
public class EmailReference : Entity
{
    public static readonly string EntityTypeName = "emailNotification";

    public EmailReference() : base(EntityTypeName) { }

    public string? Id { get; set; }
    public string? ConversationId { get; set; }
    public string? HtmlBody { get; set; }
}
```

### EmailResponse

Entity for creating email response activities.

```csharp
public class EmailResponse : Entity
{
    public static readonly string EntityTypeName = "emailResponse";

    public EmailResponse(string htmlBody) : base(EntityTypeName)
    {
        HtmlBody = htmlBody;
    }

    public string? HtmlBody { get; set; }
}
```

### WpxComment

**Source**: [WpxComment.cs](../Models/WpxComment.cs)

Entity representing a comment from Word, PowerPoint, or Excel.

```csharp
public class WpxComment : Entity
{
    public string? CommentId { get; set; }
    public string? DocumentId { get; set; }
    public string? Content { get; set; }
    public string? Author { get; set; }
    public DateTimeOffset? CreatedDateTime { get; set; }
}
```

### AgentNotificationActivity

**Source**: [AgentNotificationActivity.cs](../Models/AgentNotificationActivity.cs)

Wrapper for notification activities providing typed access to entities.

```csharp
public class AgentNotificationActivity
{
    private readonly IActivity _activity;

    public AgentNotificationActivity(IActivity activity)
    {
        _activity = activity;
    }

    public T? GetEntity<T>() where T : Entity
    {
        return _activity.Entities?
            .OfType<T>()
            .FirstOrDefault();
    }

    public IActivity Activity => _activity;
}
```

## Design Patterns

### Extension Pattern

Uses the Agents SDK extension pattern for routing:

```csharp
public static void OnAgenticEmailNotification(
    this AgentApplication app,
    AgentNotificationHandler routeHandler,
    ushort rank = 32767,
    string[] autoSignInHandlers = null!)
{
    app.RegisterExtension(new AgentNotification(app), a365 =>
    {
        a365.OnAgentNotification(SubChannels.AgentsEmailSubChannel, routeHandler, rank, autoSignInHandlers);
    });
}
```

### Route Selector Pattern

Uses route selectors to match incoming activities:

```csharp
RouteSelector routeSelector = (tc, ct) =>
    Task.FromResult(
        IsChannelForMe(tc.Activity) &&
        IsForKnownSubChannel(tc.Activity, subChannelId)
    );
```

### Entity Pattern

Notification data is carried as entities on the activity:

```csharp
// Extract entity from notification
var emailRef = notification.GetEntity<EmailReference>();

// Create response with entity
var reply = activity.CreateReply();
reply.Entities.Add(new EmailResponse(htmlBody));
```

### Fluent Builder Pattern

Handler registration supports method chaining:

```csharp
app.RegisterExtension(new AgentNotification(app), a365 =>
{
    a365.OnAgentNotification("email", emailHandler)
        .OnAgentNotification("word", wordHandler)
        .OnLifecycleNotification("*", lifecycleHandler);
});
```

## Data Flow

```
┌─────────────────────────────┐
│ Microsoft 365 Workload      │
│                             │
│ Outlook, Word, Excel,       │
│ PowerPoint, Lifecycle       │
└──────────────┬──────────────┘
               │
               ▼ Activity (channel: "agents")
┌─────────────────────────────┐
│ Microsoft Agents Platform   │
│                             │
│ Routes to agent based on    │
│ channel ID and sub-channel  │
└──────────────┬──────────────┘
               │
               ▼ ITurnContext
┌─────────────────────────────┐
│ AgentNotification Extension │
│                             │
│ 1. Check channel: "agents"  │
│ 2. Check sub-channel        │
│ 3. Match route selector     │
│ 4. Invoke handler           │
└──────────────┬──────────────┘
               │
               ▼ AgentNotificationHandler
┌─────────────────────────────┐
│ Application Handler         │
│                             │
│ 1. Extract entity           │
│ 2. Process notification     │
│ 3. Create response          │
│ 4. Send reply activity      │
└─────────────────────────────┘
```

## File Structure

```
src/Notification/Microsoft.Agents.A365.Notifications/
├── AgentNotification.cs                    # Main extension class
├── Models/
│   ├── AgentNotificationActivity.cs        # Activity wrapper
│   ├── EmailReference.cs                   # Email notification entity
│   ├── EmailResponse.cs                    # Email response entity
│   ├── WpxComment.cs                       # WPX comment entity
│   ├── NotificationTypeEnum.cs             # Notification types
│   ├── SubChannels.cs                      # Sub-channel constants
│   └── Events.cs                           # Lifecycle event constants
├── Extensions/
│   └── (Extension methods in AgentNotification.cs)
├── Serialization/
│   ├── EmailReferenceConverter.cs          # JSON converter
│   ├── WpxCommentConverter.cs              # JSON converter
│   └── SerializationInit.cs                # Serialization initialization
├── Microsoft.Agents.A365.Notifications.csproj
└── docs/
    └── design.md                           # This file
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Agents.Builder` | AgentApplication, TurnContext |

## Usage Examples

### Email Notification Handler

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAgents(options =>
{
    options.OnMessage(async (context, state, ct) =>
    {
        await context.SendActivityAsync("Message received", cancellationToken: ct);
    });
});

var app = builder.Build();

// Register email notification handler
app.OnAgenticEmailNotification(async (turnContext, turnState, notification, ct) =>
{
    var email = notification.GetEntity<EmailReference>();

    if (email != null)
    {
        // Process the email
        var response = await ProcessEmailAsync(email);

        // Create HTML response
        var htmlResponse = $"<html><body><p>{response}</p></body></html>";

        // Send reply
        var reply = turnContext.Activity.CreateEmailResponseActivity(htmlResponse);
        await turnContext.SendActivityAsync(reply, cancellationToken: ct);
    }
});

app.Run();
```

### Multi-Workload Handler

```csharp
app.OnAgenticEmailNotification(HandleEmail);
app.OnAgenticWordNotification(HandleWord);
app.OnAgenticExcelNotification(HandleExcel);
app.OnAgenticPowerPointNotification(HandlePowerPoint);

async Task HandleEmail(ITurnContext ctx, TurnState state, AgentNotificationActivity notification, CancellationToken ct)
{
    var email = notification.GetEntity<EmailReference>();
    // Handle email...
}

async Task HandleWord(ITurnContext ctx, TurnState state, AgentNotificationActivity notification, CancellationToken ct)
{
    var comment = notification.GetEntity<WpxComment>();
    // Handle Word comment...
}
```

### Lifecycle Event Handlers

```csharp
// Handle new user identity creation
app.OnAgenticUserIdentityCreatedNotification(async (ctx, state, notification, ct) =>
{
    _logger.LogInformation("New user identity created");
    // Initialize user-specific resources...
});

// Handle workload onboarding
app.OnAgenticUserWorkloadOnboardingNotification(async (ctx, state, notification, ct) =>
{
    _logger.LogInformation("User workload onboarding updated");
    // Configure workload-specific settings...
});

// Handle user deletion
app.OnAgenticUserDeletedNotification(async (ctx, state, notification, ct) =>
{
    _logger.LogInformation("User deleted");
    // Clean up user-specific resources...
});
```

### Generic Notification Handler

```csharp
// Handle all notifications from a specific sub-channel
app.OnAgentNotification(
    channelId: new ChannelId("agents") { SubChannel = "email" },
    routeHandler: async (ctx, state, notification, ct) =>
    {
        // Generic handling for email channel
    }
);

// Handle all notifications
app.RegisterExtension(new AgentNotification(app), a365 =>
{
    a365.OnAgentNotification("*", async (ctx, state, notification, ct) =>
    {
        _logger.LogInformation("Received notification: {Type}", ctx.Activity.Type);
    });
});
```

## External Resources

- [Microsoft Agent 365 Notifications](https://learn.microsoft.com/microsoft-agent-365/developer/notifications)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
- [Microsoft Graph Notifications](https://learn.microsoft.com/graph/webhooks)
