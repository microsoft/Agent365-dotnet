// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Agents.A365.Notifications;

namespace Microsoft.Agents.A365.Sidecar.Notifications;

/// <summary>
/// Extension methods for mapping Notification API endpoints.
/// Notifications from M365 (email, Word, Excel, PowerPoint) are automatically
/// forwarded to the customer webhook as turn payloads. These endpoints provide
/// schema documentation and subscription management.
/// </summary>
public static class NotificationEndpoints
{
    /// <summary>
    /// Maps the Notification schema and configuration endpoints.
    /// </summary>
    public static WebApplication MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/notifications").WithTags("Notifications");

        // GET /api/v1/notifications/channels — List supported notification channels
        group.MapGet("/channels", () =>
        {
            return Results.Ok(new[]
            {
                new NotificationChannelInfo
                {
                    Id = SubChannels.AgentsEmailSubChannel,
                    Name = "Email",
                    Description = "Email notifications via agents:email sub-channel",
                },
                new NotificationChannelInfo
                {
                    Id = SubChannels.AgentsWordSubChannel,
                    Name = "Word",
                    Description = "Word document comment notifications via agents:word sub-channel",
                },
                new NotificationChannelInfo
                {
                    Id = SubChannels.AgentsExcelSubChannel,
                    Name = "Excel",
                    Description = "Excel comment notifications via agents:excel sub-channel",
                },
                new NotificationChannelInfo
                {
                    Id = SubChannels.AgentsPowerPointSubChannel,
                    Name = "PowerPoint",
                    Description = "PowerPoint comment notifications via agents:powerpoint sub-channel",
                },
            });
        }).WithName("ListNotificationChannels");

        // GET /api/v1/notifications/schemas — Return notification payload schemas
        group.MapGet("/schemas", () =>
        {
            return Results.Ok(new
            {
                description = "Notifications are delivered to your webhook as standard turn payloads with type='event'.",
                eventNames = new
                {
                    email = "The eventName field will be the sub-channel (e.g., 'agents:email')",
                    word = "agents:word",
                    excel = "agents:excel",
                    powerpoint = "agents:powerpoint",
                },
                payloadShape = new
                {
                    turnId = "string — unique turn identifier",
                    type = "event",
                    eventName = "string — sub-channel identifier",
                    eventValue = "object — notification-specific payload (EmailReference, WpxComment, etc.)",
                    conversationId = "string",
                    channelId = "agents",
                    from = new { id = "string", name = "string" },
                },
                models = new
                {
                    emailReference = new
                    {
                        internetMessageId = "string",
                        subject = "string",
                        htmlBody = "string",
                        from = "object { emailAddress: { name, address } }",
                    },
                    wpxComment = new
                    {
                        odataId = "string — OData ID for the document",
                        documentId = "string",
                        commentId = "string",
                        text = "string — comment text",
                    },
                },
            });
        }).WithName("GetNotificationSchemas");

        // GET /api/v1/notifications/status — Notification routing status
        group.MapGet("/status", (ILogger<Program> logger) =>
        {
            logger.LogDebug("Checking notification status");
            return Results.Ok(new
            {
                enabled = true,
                routing = "All notifications are forwarded to the configured customer webhook as event-type turns.",
                note = "No subscription management needed. The sidecar automatically routes all notification activities.",
            });
        }).WithName("GetNotificationStatus");

        return app;
    }
}

/// <summary>
/// Information about a supported notification channel.
/// </summary>
public sealed class NotificationChannelInfo
{
    /// <summary>
    /// Sub-channel identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable channel name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Channel description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

