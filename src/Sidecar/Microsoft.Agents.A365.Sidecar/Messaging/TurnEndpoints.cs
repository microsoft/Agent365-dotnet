// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Sidecar.Messaging;

namespace Microsoft.Agents.A365.Sidecar.Messaging;

/// <summary>
/// Extension methods for mapping outbound turn API endpoints.
/// </summary>
public static class TurnEndpoints
{
    /// <summary>
    /// Maps the outbound turn API endpoints that customers call to send replies.
    /// </summary>
    public static WebApplication MapTurnEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/turns/{turnId}").WithTags("Messaging");

        // POST /api/v1/turns/{turnId}/reply
        group.MapPost("/reply", async (string turnId, TurnResponse body, TurnManager turnManager, CancellationToken ct) =>
        {
            var botContext = turnManager.GetBotTurnContext(turnId);
            if (botContext == null)
            {
                return Results.NotFound(new { error = "Turn not found or already completed" });
            }

            var activity = Agents.Core.Models.MessageFactory.Text(body.Text ?? string.Empty);

            if (!string.IsNullOrEmpty(body.TextFormat))
            {
                activity.TextFormat = body.TextFormat;
            }

            if (body.Attachments?.Count > 0)
            {
                activity.Attachments = body.Attachments.Select(a => new Agents.Core.Models.Attachment
                {
                    ContentType = a.ContentType,
                    ContentUrl = a.ContentUrl,
                    Content = a.Content,
                    Name = a.Name,
                }).ToList();
            }

            await botContext.SendActivityAsync(activity, ct);
            return Results.Ok();
        }).WithName("Reply");

        // POST /api/v1/turns/{turnId}/typing
        group.MapPost("/typing", async (string turnId, TurnManager turnManager, CancellationToken ct) =>
        {
            var botContext = turnManager.GetBotTurnContext(turnId);
            if (botContext == null)
            {
                return Results.NotFound(new { error = "Turn not found or already completed" });
            }

            await botContext.SendActivityAsync(
                new Agents.Core.Models.Activity { Type = Agents.Core.Models.ActivityTypes.Typing },
                ct);
            return Results.Ok();
        }).WithName("Typing");

        // DELETE /api/v1/turns/{turnId}/activities/{activityId}
        group.MapDelete("/activities/{activityId}", async (string turnId, string activityId, TurnManager turnManager, CancellationToken ct) =>
        {
            var botContext = turnManager.GetBotTurnContext(turnId);
            if (botContext == null)
            {
                return Results.NotFound(new { error = "Turn not found or already completed" });
            }

            await botContext.DeleteActivityAsync(activityId, ct);
            return Results.Ok();
        }).WithName("DeleteActivity");

        return app;
    }
}
